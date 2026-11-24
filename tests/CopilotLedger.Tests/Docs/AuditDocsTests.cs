using System.Text.Json;
using System.Text.RegularExpressions;
using CopilotLedger.Core.Audit;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Docs;

// docs/cli.md's audit tables match the code: the recommendations, the local model payload's fields, and the rules.
public sealed partial class AuditDocsTests
{
    [Fact]
    public void AD1_TheRecommendationsTable_IsAuditRecommendations()
    {
        string[] rows = Table("audit-recommendations", "| Key | Recommendation |");

        Assert.Equal(
            AuditRecommendations.Keys.Select(key => $"| `{key}` | {AuditRecommendations.Text(key)} |"),
            rows);
    }

    [Fact]
    public async Task AD2_ThePayloadAllowlist_IsWhatShowLlmPayloadWrites()
    {
        using TempDirectory temp = new();
        await AuditSeeds.CreateAsync(temp.Path);
        CliHarness cli = new(temp.Path, new FakeCliPlatform(), new ManualTimeProvider(AuditSeeds.GeneratedAt, ManualTimeProvider.UtcMinus5));

        CliRun run = await cli.RunAsync(
            "audit", "--local-llm", "http://127.0.0.1:11434/v1", "--local-llm-model", "llama3.3", "--show-llm-payload");

        using JsonDocument body = JsonDocument.Parse(run.Out);
        using JsonDocument summary = JsonDocument.Parse(body.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!);
        string[] written =
        [
            .. Objects(body.RootElement, ".").Select(item => $"body {item.Path} {string.Join(", ", item.Names)}"),
            .. Objects(summary.RootElement, ".").Select(item => $"summary {item.Path} {string.Join(", ", item.Names)}"),
        ];
        string[] documented =
        [
            .. Table("audit-payload-allowlist", "| Document | Object | Fields |").Select(row =>
            {
                string[] cells = row.Trim('|', ' ').Split(" | ");
                return $"{Ticked(cells[0]).Single()} {Ticked(cells[1]).Single()} {string.Join(", ", Ticked(cells[2]))}";
            }),
        ];
        Assert.Equal(documented, written.Distinct(StringComparer.Ordinal));
    }

    [Fact]
    public void AD3_TheRulesTable_ListsEveryRuleInOrder()
    {
        string[] rows = Table("audit-rules", "| Rule | Finds | Warning | High | Estimated impact |");

        Assert.Equal(AuditRuleIds.All, rows.Select(row => Ticked(row.Trim('|', ' ').Split(" | ")[0]).Single()));
    }

    // The rows of the table between <!-- name --> and <!-- /name --> in docs/cli.md, after its header and separator.
    private static string[] Table(string name, string header)
    {
        string[] lines = File.ReadAllLines(RepositoryFile("docs", "cli.md"));
        int start = Array.IndexOf(lines, $"<!-- {name} -->");
        int end = Array.IndexOf(lines, $"<!-- /{name} -->");
        Assert.True(start >= 0 && end > start + 2, name);
        Assert.Equal(header, lines[start + 1]);
        return lines[(start + 3)..end];
    }

    // Each object in element, with its path ("." at the top, names joined by ".", "[]" after an array's name), in document
    // order.
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

    private static IEnumerable<string> Ticked(string cell) => Code().Matches(cell).Select(match => match.Groups[1].Value);

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
}
