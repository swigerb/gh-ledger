namespace CopilotLedger.Cli.Export;

// The report's stylesheet, embedded in the assembly, with "\n" line endings whatever the checkout used.
internal static class ExportAssets
{
    public static string Css { get; } = Read("CopilotLedger.Cli.Export.Assets.report.css");

    private static string Read(string name)
    {
        using Stream stream = typeof(ExportAssets).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"The resource {name} is missing.");
        using StreamReader reader = new(stream);
        return reader.ReadToEnd().ReplaceLineEndings("\n");
    }
}
