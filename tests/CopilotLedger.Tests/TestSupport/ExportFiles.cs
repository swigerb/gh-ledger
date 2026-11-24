using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace CopilotLedger.Tests.TestSupport;

// Reads what gh ledger export wrote.
public static partial class ExportFiles
{
    // The report's text. Fails when it has a byte order mark, a carriage return, or invalid UTF-8.
    public static string ReadReport(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        Assert.False(bytes is [0xEF, 0xBB, 0xBF, ..], "The report starts with a byte order mark.");
        string html = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes);
        Assert.DoesNotContain('\r', html);
        return html;
    }

    // The page as XML, with the contents of its style and script elements removed. Fails when it isn't well formed.
    public static XDocument Parse(string html)
    {
        using XmlReader reader = XmlReader.Create(
            new StringReader(StyleOrScript().Replace(html, "$1$2")),
            new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore });
        return XDocument.Load(reader);
    }

    // ReadReport, then Parse.
    public static XDocument Load(string path) => Parse(ReadReport(path));

    // The element with this id.
    public static XElement ById(XDocument page, string id) =>
        page.Descendants().Single(element => (string?)element.Attribute("id") == id);

    // The header's description of term.
    public static string Meta(XDocument page, string term) =>
        page.Descendants("dt").Single(dt => dt.Value == term).ElementsAfterSelf("dd").First().Value;

    // The report's one ledger-data block, parsed.
    public static JsonDocument LedgerData(string html)
    {
        const string Start = "<script type=\"application/json\" id=\"ledger-data\">";
        int start = html.IndexOf(Start, StringComparison.Ordinal);
        Assert.True(start >= 0, "The report has no ledger-data block.");
        Assert.Equal(start, html.LastIndexOf(Start, StringComparison.Ordinal));
        start += Start.Length;
        return JsonDocument.Parse(html[start..html.IndexOf("</script>", start, StringComparison.Ordinal)]);
    }

    // The bundle's entries in archive order, decompressed.
    public static IReadOnlyList<(string Name, byte[] Bytes)> ReadZip(string path)
    {
        using ZipArchive zip = ZipFile.OpenRead(path);
        return
        [
            .. zip.Entries.Select(entry =>
            {
                using Stream stream = entry.Open();
                using MemoryStream bytes = new();
                stream.CopyTo(bytes);
                return (entry.FullName, bytes.ToArray());
            }),
        ];
    }

    // Each entry's local file header, in central-directory order: its name, DOS time, DOS date, and compression method.
    public static IReadOnlyList<(string Name, int Time, int Date, int Method)> LocalHeaders(string path)
    {
        byte[] zip = File.ReadAllBytes(path);
        int end = zip.Length - 22;
        Assert.Equal(0x06054b50u, BinaryPrimitives.ReadUInt32LittleEndian(zip.AsSpan(end)));
        int count = BinaryPrimitives.ReadUInt16LittleEndian(zip.AsSpan(end + 10));
        int offset = (int)BinaryPrimitives.ReadUInt32LittleEndian(zip.AsSpan(end + 16));
        List<(string, int, int, int)> headers = [];
        for (int index = 0; index < count; index++)
        {
            Assert.Equal(0x02014b50u, BinaryPrimitives.ReadUInt32LittleEndian(zip.AsSpan(offset)));
            int local = (int)BinaryPrimitives.ReadUInt32LittleEndian(zip.AsSpan(offset + 42));
            Assert.Equal(0x04034b50u, BinaryPrimitives.ReadUInt32LittleEndian(zip.AsSpan(local)));
            headers.Add((
                Encoding.UTF8.GetString(zip, local + 30, BinaryPrimitives.ReadUInt16LittleEndian(zip.AsSpan(local + 26))),
                BinaryPrimitives.ReadUInt16LittleEndian(zip.AsSpan(local + 10)),
                BinaryPrimitives.ReadUInt16LittleEndian(zip.AsSpan(local + 12)),
                BinaryPrimitives.ReadUInt16LittleEndian(zip.AsSpan(local + 8))));
            offset += 46
                + BinaryPrimitives.ReadUInt16LittleEndian(zip.AsSpan(offset + 28))
                + BinaryPrimitives.ReadUInt16LittleEndian(zip.AsSpan(offset + 30))
                + BinaryPrimitives.ReadUInt16LittleEndian(zip.AsSpan(offset + 32));
        }

        return headers;
    }

    [GeneratedRegex(@"(<(?:style|script)\b[^>]*>).*?(</(?:style|script)>)", RegexOptions.Singleline)]
    private static partial Regex StyleOrScript();
}
