using System.Text.Json;
using System.Text.RegularExpressions;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Core.Telemetry;
using CopilotLedger.Storage;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Cli;

// Export writes exactly the fields that docs/cli.md lists between its export-allowlist markers, in that order.
public sealed partial class ExportAllowlistTests
{
    private const string Bundle = "copilot-ledger-report.zip";

    [Fact]
    public async Task EL1_EveryObjectWrittenIsDocumentedAndEveryRowIsUsed()
    {
        Dictionary<(string File, string Path), string[]> documented = Documented();
        HashSet<(string File, string Path)> used = [];

        foreach (bool includeLocalPaths in new[] { false, true })
        {
            using TempDirectory temp = new();
            SqliteLedgerStore store = await ExportSeeds.CreateAsync(temp.Path);

            // A request whose input dwarfs its output, so the audit file has a finding.
            NormalizedGenAiSpan bloated = TestSpans.Chat(
                startTime: ExportSeeds.GeneratedAt.AddHours(-1),
                requestModel: "claude-opus-5.5",
                conversationId: "conv-bloat",
                inputTokens: 60_000,
                outputTokens: 100);
            await store.RecordAsync([new LedgerEntry(bloated, JsonRateCardCalculator.LoadEmbedded().Calculate(bloated), null)]);
            foreach ((string file, JsonDocument document) in await ExportAsync(temp.Path, includeLocalPaths))
            {
                using (document)
                {
                    foreach ((string path, string[] names) in Objects(document.RootElement, "."))
                    {
                        Assert.True(documented.TryGetValue((file, path), out string[]? fields), $"{file} {path} isn't documented.");
                        Assert.Equal(includeLocalPaths ? fields : fields.Where(field => field != "repositoryRoot"), names);
                        used.Add((file, path));
                    }
                }
            }
        }

        Assert.Equal(documented.Keys.Order(), used.Order());
    }

    [Fact]
    public async Task EL2_SpanRecordsHaveTheDocumentedFieldsInOrder()
    {
        string[] documented = Documented()[("telemetry/spans.json", "spans[]")];

        foreach (bool includeLocalPaths in new[] { false, true })
        {
            using TempDirectory temp = new();
            await ExportSeeds.CreateAsync(temp.Path);
            IReadOnlyList<(string File, JsonDocument Document)> files = await ExportAsync(temp.Path, includeLocalPaths);
            JsonElement first = files.Single(file => file.File == "telemetry/spans.json").Document.RootElement.GetProperty("spans")[0];
            string[] names = [.. first.EnumerateObject().Select(property => property.Name)];
            Assert.Equal(includeLocalPaths ? documented : documented.Where(field => field != "repositoryRoot"), names);
            Assert.Equal(includeLocalPaths, names.Contains("repositoryRoot"));
            foreach ((_, JsonDocument document) in files)
            {
                document.Dispose();
            }
        }

        Assert.Equal("repository", documented[Array.IndexOf(documented, "repositoryRoot") - 1]);
    }

    [Fact]
    public async Task EL3_TheManifestAndItsExampleMatchTheAllowlist()
    {
        Dictionary<(string File, string Path), string[]> documented = Documented();
        using TempDirectory temp = new();
        await ExportSeeds.CreateAsync(temp.Path);

        IReadOnlyList<(string File, JsonDocument Document)> files = await ExportAsync(temp.Path, false);
        JsonElement manifest = files.Single(file => file.File == "manifest.json").Document.RootElement;

        Assert.Equal(documented[("manifest.json", ".")], manifest.EnumerateObject().Select(property => property.Name));
        foreach (string path in new[] { "filter", "timeZone", "options", "privacy", "coverage", "coverage.toolHistory", "coverage.timeline" })
        {
            JsonElement value = path.Split('.').Aggregate(manifest, (element, name) => element.GetProperty(name));
            Assert.Equal(documented[("manifest.json", path)], value.EnumerateObject().Select(property => property.Name));
        }

        string cli = File.ReadAllText(RepositoryFile("docs", "cli.md")).ReplaceLineEndings("\n");
        string example = ManifestExample().Match(cli[cli.IndexOf("### The bundle", StringComparison.Ordinal)..]).Groups[1].Value;
        using JsonDocument parsed = JsonDocument.Parse(example);
        Assert.Equal(documented[("manifest.json", ".")], parsed.RootElement.EnumerateObject().Select(property => property.Name));
        foreach ((_, JsonDocument document) in files)
        {
            document.Dispose();
        }
    }

    // Exports the fixture with a bundle and returns each JSON document: the bundle's files and the report's ledger-data block.
    private static async Task<IReadOnlyList<(string File, JsonDocument Document)>> ExportAsync(string root, bool includeLocalPaths)
    {
        CliHarness cli = new(root, new FakeCliPlatform(), new ManualTimeProvider(ExportSeeds.GeneratedAt, ManualTimeProvider.UtcMinus5));
        CliRun run = await cli.RunAsync(includeLocalPaths
            ? ["export", "--all-repos", "--bundle", "--include-local-paths"]
            : ["export", "--all-repos", "--bundle"]);
        Assert.Equal((0, string.Empty), (run.ExitCode, run.Error));
        IReadOnlyList<(string Name, byte[] Bytes)> entries = ExportFiles.ReadZip(Path.Combine(root, Bundle));
        return
        [
            .. entries.Select(entry => entry.Name == "report.html"
                ? (entry.Name, ExportFiles.LedgerData(System.Text.Encoding.UTF8.GetString(entry.Bytes)))
                : (entry.Name, JsonDocument.Parse(entry.Bytes))),
        ];
    }

    // Each object in element and its property names, with its path: "." for the top level, names joined by "." for nested
    // objects, and "[]" after an array's name for its objects.
    private static IEnumerable<(string Path, string[] Names)> Objects(JsonElement element, string path)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            yield return (path, [.. element.EnumerateObject().Select(property => property.Name)]);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                foreach ((string, string[]) nested in Objects(property.Value, path == "." ? property.Name : $"{path}.{property.Name}"))
                {
                    yield return nested;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
            {
                foreach ((string, string[]) nested in Objects(item, path + "[]"))
                {
                    yield return nested;
                }
            }
        }
    }

    // The allowlist: each (file, object) and its fields in order. A row may name several files.
    private static Dictionary<(string File, string Path), string[]> Documented()
    {
        string[] lines = File.ReadAllLines(RepositoryFile("docs", "cli.md"));
        int start = Array.IndexOf(lines, "<!-- export-allowlist -->");
        int end = Array.IndexOf(lines, "<!-- /export-allowlist -->");
        Assert.True(start >= 0 && end > start + 2);
        Assert.Equal("| File | Object | Fields |", lines[start + 1]);
        Dictionary<(string, string), string[]> rows = [];
        foreach (string line in lines[(start + 3)..end])
        {
            string[] cells = line.Trim('|', ' ').Split(" | ");
            Assert.Equal(3, cells.Length);
            string path = Assert.Single(Ticked(cells[1]));
            foreach (string file in Ticked(cells[0]))
            {
                Assert.True(rows.TryAdd((file, path), [.. Ticked(cells[2])]), $"{file} {path} is documented twice.");
            }
        }

        return rows;
    }

    private static IEnumerable<string> Ticked(string cell) =>
        Code().Matches(cell).Select(match => match.Groups[1].Value);

    // A file of the repository, found from the test's output directory.
    private static string RepositoryFile(params string[] parts)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CopilotLedger.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine([directory.FullName, .. parts]);
    }

    [GeneratedRegex("`([^`]+)`")]
    private static partial Regex Code();

    [GeneratedRegex("```json\n(.*?)\n```", RegexOptions.Singleline)]
    private static partial Regex ManifestExample();
}
