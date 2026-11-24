using System.Text.Json;
using CopilotLedger.Cli;
using CopilotLedger.Cli.Receipts;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Core.Telemetry;
using CopilotLedger.Storage;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Cli;

public sealed class ReceiptCommandTests
{
    private const string FeatureFile = ".github/copilot-ledger/receipts/feature-ledger-0a69c755.md";

    private const string FeatureReceipt = """
        <!-- copilot-ledger-receipt -->
        ### gh-ledger receipt for `feature/ledger`

        - **GitHub AI Credits:** 342.00 credits ($3.42) across 3 turns, 1 unpriced: `claude-opus-5.5` 286.00, `mai-code-2` 56.00, `gpt-unknown` 0.00
        - **Foundry BYOK:** $0.32 across 1 turn: `gpt-6.1-sol` $0.32
        - **Local offload:** 412,000 tokens across 1 turn ($0.00 billed • $0.23 cloud spend avoided): `qwen3.8-27b` 412,000
        - **Not priced:** 1 turn: `mystery-1`
        - **API-equivalent (retail):** $2.86 at API list prices • 5 turns unpriced
        - **Circuit breaker:** 2 trips • max single-prompt burn: 317.50 credits

        <details>
        <summary>Details and data</summary>

        | Billing | Model | Turns | Unpriced | Input tokens | Output tokens | Cost | API-equivalent | Cloud spend avoided |
        | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
        | GitHub AI Credits | `claude-opus-5.5` | 1 | 0 | 1,200,000 | 50,000 | 286.00 credits ($2.86) | $2.86 | $0.00 |
        | GitHub AI Credits | `mai-code-2` | 1 | 0 | 1,000,000 | 100,000 | 56.00 credits ($0.56) | Unpriced | $0.00 |
        | Foundry BYOK | `gpt-6.1-sol` | 1 | 0 | 100,000 | 10,000 | $0.32 | Unpriced | $0.00 |
        | Local | `qwen3.8-27b` | 1 | 0 | 400,000 | 12,000 | $0.00 | Unpriced | $0.23 |
        | GitHub AI Credits | `gpt-unknown` | 1 | 1 | 1,000 | 100 | 0.00 credits ($0.00) | Unpriced | $0.00 |
        | Not priced | `mystery-1` | 1 | 1 | 2,000 | 200 | $0.00 | Unpriced | $0.00 |

        Repository `swigerb/copilot-ledger` • 3 prompts • commit `0123456789ab` • generated 2026-11-21 15:00 UTC by gh-ledger <version>

        To check these totals, run `gh ledger branch --since all --json` in the repository and compare the `feature/ledger` row.

        ```json
        {
          "schemaVersion": 1,
          "repository": "swigerb/copilot-ledger",
          "branch": "feature/ledger",
          "commit": "0123456789abcdef0123456789abcdef01234567",
          "generatedAt": "2026-11-21T15:00:00.0000000+00:00",
          "generator": "gh-ledger <version>",
          "totals": {
            "spans": 6,
            "unpricedSpans": 2,
            "inputTokens": 2703000,
            "outputTokens": 172300,
            "cacheReadInputTokens": 1200000,
            "cacheWriteInputTokens": 100000,
            "aiCredits": 342,
            "aiCreditsUsd": 3.42,
            "foundryByokUsd": 0.315,
            "localSavedUsd": 0.23,
            "apiEquivalentUsd": 2.86,
            "apiEquivalentUnpricedSpans": 5,
            "apiEquivalentEstimatedSpans": 0
          },
          "prompts": 3,
          "maxPromptCredits": 317.5,
          "breakerTrips": 2,
          "models": [
            {
              "channel": "github_ai_credits",
              "model": "claude-opus-5.5",
              "spans": 1,
              "unpricedSpans": 0,
              "inputTokens": 1200000,
              "outputTokens": 50000,
              "cacheReadInputTokens": 800000,
              "cacheWriteInputTokens": 100000,
              "aiCredits": 286,
              "aiCreditsUsd": 2.86,
              "foundryByokUsd": 0,
              "localSavedUsd": 0,
              "apiEquivalentUsd": 2.86,
              "apiEquivalentUnpricedSpans": 0,
              "apiEquivalentEstimatedSpans": 0
            },
            {
              "channel": "github_ai_credits",
              "model": "mai-code-2",
              "spans": 1,
              "unpricedSpans": 0,
              "inputTokens": 1000000,
              "outputTokens": 100000,
              "cacheReadInputTokens": 400000,
              "cacheWriteInputTokens": 0,
              "aiCredits": 56,
              "aiCreditsUsd": 0.56,
              "foundryByokUsd": 0,
              "localSavedUsd": 0,
              "apiEquivalentUsd": null,
              "apiEquivalentUnpricedSpans": 1,
              "apiEquivalentEstimatedSpans": 0
            },
            {
              "channel": "foundry_byok",
              "model": "gpt-6.1-sol",
              "spans": 1,
              "unpricedSpans": 0,
              "inputTokens": 100000,
              "outputTokens": 10000,
              "cacheReadInputTokens": 0,
              "cacheWriteInputTokens": 0,
              "aiCredits": 0,
              "aiCreditsUsd": 0,
              "foundryByokUsd": 0.315,
              "localSavedUsd": 0,
              "apiEquivalentUsd": null,
              "apiEquivalentUnpricedSpans": 1,
              "apiEquivalentEstimatedSpans": 0
            },
            {
              "channel": "local",
              "model": "qwen3.8-27b",
              "spans": 1,
              "unpricedSpans": 0,
              "inputTokens": 400000,
              "outputTokens": 12000,
              "cacheReadInputTokens": 0,
              "cacheWriteInputTokens": 0,
              "aiCredits": 0,
              "aiCreditsUsd": 0,
              "foundryByokUsd": 0,
              "localSavedUsd": 0.23,
              "apiEquivalentUsd": null,
              "apiEquivalentUnpricedSpans": 1,
              "apiEquivalentEstimatedSpans": 0
            },
            {
              "channel": "github_ai_credits",
              "model": "gpt-unknown",
              "spans": 1,
              "unpricedSpans": 1,
              "inputTokens": 1000,
              "outputTokens": 100,
              "cacheReadInputTokens": 0,
              "cacheWriteInputTokens": 0,
              "aiCredits": 0,
              "aiCreditsUsd": 0,
              "foundryByokUsd": 0,
              "localSavedUsd": 0,
              "apiEquivalentUsd": null,
              "apiEquivalentUnpricedSpans": 1,
              "apiEquivalentEstimatedSpans": 0
            },
            {
              "channel": "unpriced",
              "model": "mystery-1",
              "spans": 1,
              "unpricedSpans": 1,
              "inputTokens": 2000,
              "outputTokens": 200,
              "cacheReadInputTokens": 0,
              "cacheWriteInputTokens": 0,
              "aiCredits": 0,
              "aiCreditsUsd": 0,
              "foundryByokUsd": 0,
              "localSavedUsd": 0,
              "apiEquivalentUsd": null,
              "apiEquivalentUnpricedSpans": 1,
              "apiEquivalentEstimatedSpans": 0
            }
          ]
        }
        ```

        </details>

        """;

    private const string EmptyReceipt = """
        <!-- copilot-ledger-receipt -->
        ### gh-ledger receipt for `fix/@octocat-#7`

        - **GitHub AI Credits:** none
        - **Foundry BYOK:** none
        - **Local offload:** none
        - **API-equivalent (retail):** none
        - **Circuit breaker:** 0 trips • max single-prompt burn: 0.00 credits

        <details>
        <summary>Details and data</summary>

        No spans are recorded for this branch.

        Repository `swigerb/copilot-ledger` • 0 prompts • commit `0123456789ab` • generated 2026-11-21 15:00 UTC by gh-ledger <version>

        To check these totals, run `gh ledger branch --since all --json` in the repository and compare the `fix/@octocat-#7` row.

        ```json
        {
          "schemaVersion": 1,
          "repository": "swigerb/copilot-ledger",
          "branch": "fix/@octocat-#7",
          "commit": "0123456789abcdef0123456789abcdef01234567",
          "generatedAt": "2026-11-21T15:00:00.0000000+00:00",
          "generator": "gh-ledger <version>",
          "totals": {
            "spans": 0,
            "unpricedSpans": 0,
            "inputTokens": 0,
            "outputTokens": 0,
            "cacheReadInputTokens": 0,
            "cacheWriteInputTokens": 0,
            "aiCredits": 0,
            "aiCreditsUsd": 0,
            "foundryByokUsd": 0,
            "localSavedUsd": 0,
            "apiEquivalentUsd": 0,
            "apiEquivalentUnpricedSpans": 0,
            "apiEquivalentEstimatedSpans": 0
          },
          "prompts": 0,
          "maxPromptCredits": 0,
          "breakerTrips": 0,
          "models": []
        }
        ```

        </details>

        """;

    private static ManualTimeProvider Time => new(
        DateTimeOffset.Parse("2026-11-21T15:00:00Z"),
        ManualTimeProvider.UtcMinus5);

    [Fact]
    public async Task EP1_ExportWritesTheExactReceiptFile()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = await SeededHarnessAsync(temp.Path);
        string repo = Path.Combine(temp.Path, "repo");

        CliRun run = await harness.RunInAsync(Path.Combine(repo, "src"), "export-pr");

        Assert.Equal(0, run.ExitCode);
        Assert.Equal(
            $"Wrote {FeatureFile} for feature/ledger.\n" +
            "Commit it on the branch and push; the PR receipt action posts it on the pull request.\n",
            run.Out);
        Assert.Equal(string.Empty, run.Error);
        string[] files = Directory.GetFiles(Path.Combine(repo, ".github", "copilot-ledger", "receipts"));
        Assert.Equal([Path.Combine(repo, ".github", "copilot-ledger", "receipts", "feature-ledger-0a69c755.md")], files);
        byte[] bytes = await FileRetry.ReadAllBytesAsync(files[0]);
        Assert.NotEqual(0xEF, bytes[0]);
        Assert.Equal(Expected(FeatureReceipt), System.Text.Encoding.UTF8.GetString(bytes));
    }

    [Fact]
    public async Task EP2_StdoutWritesTheMarkdownAndNoFile()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = await SeededHarnessAsync(temp.Path);
        string repo = Path.Combine(temp.Path, "repo");

        CliRun run = await harness.RunInAsync(repo, "export-pr", "--stdout");

        Assert.Equal(0, run.ExitCode);
        Assert.Equal(Expected(FeatureReceipt), run.Out);
        Assert.Equal(string.Empty, run.Error);
        Assert.False(Directory.Exists(Path.Combine(repo, ".github")));
    }

    [Fact]
    public async Task EP3_JsonIsTheReceiptDataBlock()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = await SeededHarnessAsync(temp.Path);
        string repo = Path.Combine(temp.Path, "repo");

        CliRun run = await harness.RunInAsync(repo, "export-pr", "--json");

        Assert.Equal(0, run.ExitCode);
        string receipt = Expected(FeatureReceipt);
        int start = receipt.IndexOf("```json\n", StringComparison.Ordinal) + "```json\n".Length;
        int end = receipt.IndexOf("\n```\n", start, StringComparison.Ordinal);
        Assert.Equal(receipt[start..end] + "\n", run.Out);
        Assert.Equal(string.Empty, run.Error);
        Assert.False(Directory.Exists(Path.Combine(repo, ".github")));
    }

    [Fact]
    public async Task EP4_TotalsReconcileWithBranchJson()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = await SeededHarnessAsync(temp.Path);
        string repo = Path.Combine(temp.Path, "repo");

        CliRun receipt = await harness.RunInAsync(repo, "export-pr", "--json");
        CliRun branch = await harness.RunInAsync(repo, "branch", "--since", "all", "--json");

        Assert.Equal(0, receipt.ExitCode);
        Assert.Equal(0, branch.ExitCode);
        using JsonDocument receiptJson = JsonDocument.Parse(receipt.Out);
        using JsonDocument branchJson = JsonDocument.Parse(branch.Out);
        JsonElement totals = receiptJson.RootElement.GetProperty("totals");
        JsonElement row = Assert.Single(
            branchJson.RootElement.GetProperty("rows").EnumerateArray(),
            item => item.GetProperty("branch").GetString() == "feature/ledger");
        Assert.Equal("swigerb/copilot-ledger", row.GetProperty("repository").GetString());
        foreach (string name in new[] { "spans", "unpricedSpans", "inputTokens", "outputTokens", "cacheReadInputTokens", "cacheWriteInputTokens" })
        {
            Assert.Equal(row.GetProperty(name).GetInt64(), totals.GetProperty(name).GetInt64());
        }

        foreach (string name in new[] { "aiCredits", "aiCreditsUsd", "foundryByokUsd", "localSavedUsd" })
        {
            Assert.Equal(row.GetProperty(name).GetDecimal(), totals.GetProperty(name).GetDecimal());
        }

        Assert.Equal(342m, totals.GetProperty("aiCredits").GetDecimal());
        Assert.Equal(3.42m, totals.GetProperty("aiCreditsUsd").GetDecimal());
        Assert.Equal(0.315m, totals.GetProperty("foundryByokUsd").GetDecimal());
        Assert.Equal(0.23m, totals.GetProperty("localSavedUsd").GetDecimal());
        decimal modelCredits = receiptJson.RootElement.GetProperty("models").EnumerateArray()
            .Sum(item => item.GetProperty("aiCredits").GetDecimal());
        Assert.Equal(342m, modelCredits);
    }

    [Fact]
    public async Task EP5_BranchWithoutSpendGetsAnEmptyReceipt()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = await SeededHarnessAsync(temp.Path);
        string repo = Path.Combine(temp.Path, "repo");
        string git = Path.Combine(repo, ".git");
        Directory.CreateDirectory(Path.Combine(git, "refs", "heads", "fix"));
        File.WriteAllText(Path.Combine(git, "HEAD"), "ref: refs/heads/fix/@octocat-#7\n");
        File.WriteAllText(Path.Combine(git, "refs", "heads", "fix", "@octocat-#7"), GitFixture.Sha1 + "\n");
        await FileRetry.ReadAllTextAsync(Path.Combine(git, "HEAD"));
        await FileRetry.ReadAllTextAsync(Path.Combine(git, "refs", "heads", "fix", "@octocat-#7"));

        CliRun run = await harness.RunInAsync(repo, "export-pr");

        Assert.Equal(0, run.ExitCode);
        Assert.Equal(
            "Wrote .github/copilot-ledger/receipts/fix--octocat--7-98e51822.md for fix/@octocat-#7.\n" +
            "Commit it on the branch and push; the PR receipt action posts it on the pull request.\n",
            run.Out);
        Assert.Equal(
            Expected(EmptyReceipt),
            await FileRetry.ReadAllTextAsync(Path.Combine(repo, ".github", "copilot-ledger", "receipts", "fix--octocat--7-98e51822.md")));
    }

    public static TheoryData<string, string> GitProblems => new()
    {
        { "no-repository", "<work> isn't in a Git repository.\n" },
        { "detached", "HEAD is detached. Check out the pull request's branch, then run export-pr again.\n" },
        { "unborn", "feature/ledger has no commits yet.\n" },
        { "no-origin", "export-pr needs a GitHub remote named origin, and <repo> has none.\n" },
    };

    // Assumes no directory above the temp directory has a .git entry.
    [Theory]
    [MemberData(nameof(GitProblems))]
    public async Task EP6_GitProblemsFailWithoutWriting(string problem, string error)
    {
        using TempDirectory temp = new();
        string work = Path.Combine(temp.Path, "work");
        Directory.CreateDirectory(work);
        string repo = GitFixture.Create(temp.Path);
        string git = Path.Combine(repo, ".git");
        switch (problem)
        {
            case "detached":
                File.WriteAllText(Path.Combine(git, "HEAD"), GitFixture.Sha1 + "\n");
                await FileRetry.ReadAllTextAsync(Path.Combine(git, "HEAD"));
                break;
            case "unborn":
                File.Delete(Path.Combine(git, "refs", "heads", "feature", "ledger"));
                break;
            case "no-origin":
                File.WriteAllText(Path.Combine(git, "config"), "[core]\n\tbare = false\n");
                await FileRetry.ReadAllTextAsync(Path.Combine(git, "config"));
                break;
        }

        await using CliHarness harness = new(temp.Path, new FakeCliPlatform(), Time);

        CliRun run = await harness.RunInAsync(problem == "no-repository" ? work : repo, "export-pr");

        Assert.Equal(1, run.ExitCode);
        Assert.Equal(string.Empty, run.Out);
        Assert.Equal(error.Replace("<work>", work).Replace("<repo>", repo), run.Error);
        Assert.False(Directory.Exists(Path.Combine(repo, ".github")));
    }

    [Fact]
    public async Task EP7_StdoutAndJsonTogetherFail()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = await SeededHarnessAsync(temp.Path);

        CliRun run = await harness.RunInAsync(
            Path.Combine(temp.Path, "repo"),
            "export-pr",
            "--stdout",
            "--json");

        Assert.Equal(1, run.ExitCode);
        Assert.StartsWith("Use either --stdout or --json, not both.\n", run.Error);
        Assert.False(Directory.Exists(Path.Combine(temp.Path, "repo", ".github")));
    }

    public static TheoryData<string, string> FileNames => new()
    {
        { "feature/ledger", "feature-ledger-0a69c755.md" },
        { "fix/@octocat-#7", "fix--octocat--7-98e51822.md" },
        { "Feature/Ledger", "Feature-Ledger-a4b486e4.md" },
        { "release/v1.2_rc-1", "release-v1.2_rc-1-7a79d7e7.md" },
        { "topic/\u00FCn\u00EFcode", "topic--n-code-ae54b3cf.md" },
        { new string('a', 70), new string('a', 64) + "-6bd5e503.md" },
        { "con", "con-1143da2b.md" },
    };

    [Theory]
    [MemberData(nameof(FileNames))]
    public void EP8_ReceiptFileNames(string branch, string fileName)
    {
        Assert.Equal(fileName, ReceiptMarkdown.FileName(branch));
    }

    public static TheoryData<string, string> CodeSpans => new()
    {
        { "feature/ledger", "`feature/ledger`" },
        { "@octocat", "`@octocat`" },
        { "a`b", "``a`b``" },
        { "`tick", "`` `tick ``" },
        { " lead", "`  lead `" },
        { "a``b```c", "````a``b```c````" },
        { "x\ny\u2028z", "`x\uFFFDy\uFFFDz`" },
    };

    [Theory]
    [MemberData(nameof(CodeSpans))]
    public void EP9_CodeSpansShowValuesLiterally(string value, string span)
    {
        Assert.Equal(span, ReceiptMarkdown.Code(value));
    }

    // The model's 22,000-character name appears three times: in the summary, the table, and the JSON data.
    [Fact]
    public async Task EP10_ReceiptsOverTheSizeLimitAreRefused()
    {
        using TempDirectory temp = new();
        GitFixture.Create(temp.Path);
        await using CliHarness harness = new(temp.Path, new FakeCliPlatform(), Time);
        SqliteLedgerStore store = new(Path.Combine(temp.Path, "data", "ledger.db"), harness.TimeProvider);
        await store.InitializeAsync();
        NormalizedGenAiSpan span = TestSpans.Chat(
            requestModel: new string('m', 22_000),
            inputTokens: 1_000,
            outputTokens: 100);
        await store.RecordAsync(
        [
            new LedgerEntry(span, JsonRateCardCalculator.LoadEmbedded().Calculate(span), GitFixture.Feature(temp.Path)),
        ]);
        string repo = Path.Combine(temp.Path, "repo");
        const string Error =
            "The receipt for feature/ledger is 68090 bytes; a receipt can be at most 65536 bytes.\n";

        CliRun file = await harness.RunInAsync(repo, "export-pr");
        CliRun stdout = await harness.RunInAsync(repo, "export-pr", "--stdout");
        CliRun json = await harness.RunInAsync(repo, "export-pr", "--json");

        Assert.Equal(1, file.ExitCode);
        Assert.Equal(string.Empty, file.Out);
        Assert.Equal(Error, file.Error);
        Assert.Equal(1, stdout.ExitCode);
        Assert.Equal(string.Empty, stdout.Out);
        Assert.Equal(Error, stdout.Error);
        Assert.Equal(0, json.ExitCode);
        Assert.Equal(string.Empty, json.Error);
        Assert.False(Directory.Exists(Path.Combine(repo, ".github")));
    }

    [Fact]
    public async Task EP11_ApiEquivalentTotalsReconcileWithBranchJson()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = await SeededHarnessAsync(temp.Path);
        string repo = Path.Combine(temp.Path, "repo");

        CliRun receipt = await harness.RunInAsync(repo, "export-pr", "--json");
        CliRun branch = await harness.RunInAsync(repo, "branch", "--since", "all", "--json");

        using JsonDocument receiptJson = JsonDocument.Parse(receipt.Out);
        using JsonDocument branchJson = JsonDocument.Parse(branch.Out);
        JsonElement totals = receiptJson.RootElement.GetProperty("totals");
        JsonElement row = Assert.Single(
            branchJson.RootElement.GetProperty("rows").EnumerateArray(),
            item => item.GetProperty("branch").GetString() == "feature/ledger");
        foreach (string name in new[] { "apiEquivalentUsd", "apiEquivalentUnpricedSpans", "apiEquivalentEstimatedSpans" })
        {
            Assert.Equal(row.GetProperty(name).GetRawText(), totals.GetProperty(name).GetRawText());
        }

        JsonElement[] models = receiptJson.RootElement.GetProperty("models").EnumerateArray().ToArray();
        Assert.Equal(
            totals.GetProperty("apiEquivalentUsd").GetDecimal(),
            models.Where(item => item.GetProperty("apiEquivalentUsd").ValueKind != JsonValueKind.Null)
                .Sum(item => item.GetProperty("apiEquivalentUsd").GetDecimal()));
        Assert.Equal(
            totals.GetProperty("apiEquivalentUnpricedSpans").GetInt64(),
            models.Sum(item => item.GetProperty("apiEquivalentUnpricedSpans").GetInt64()));
    }

    // A branch whose turns all lack a list price says Unpriced, never $0.00.
    [Fact]
    public async Task EP12_ReceiptsWithoutApiEquivalentPricesSayUnpriced()
    {
        using TempDirectory temp = new();
        GitFixture.Create(temp.Path);
        await using CliHarness harness = new(temp.Path, new FakeCliPlatform(), Time);
        SqliteLedgerStore store = new(Path.Combine(temp.Path, "data", "ledger.db"), harness.TimeProvider);
        await store.InitializeAsync();
        NormalizedGenAiSpan span = TestSpans.Chat(requestModel: "mai-code-2", inputTokens: 1_000, outputTokens: 100);
        await store.RecordAsync(
        [
            new LedgerEntry(span, JsonRateCardCalculator.LoadEmbedded().Calculate(span), GitFixture.Feature(temp.Path)),
        ]);
        string repo = Path.Combine(temp.Path, "repo");

        CliRun markdown = await harness.RunInAsync(repo, "export-pr", "--stdout");
        CliRun json = await harness.RunInAsync(repo, "export-pr", "--json");

        Assert.Equal(0, markdown.ExitCode);
        Assert.Contains(
            "\n- **API-equivalent (retail):** Unpriced (1 turn without an API-equivalent price)\n",
            markdown.Out);
        Assert.Contains(
            "| GitHub AI Credits | `mai-code-2` | 1 | 0 | 1,000 | 100 | 0.08 credits ($0.00) | Unpriced | $0.00 |\n",
            markdown.Out);
        using JsonDocument document = JsonDocument.Parse(json.Out);
        Assert.Equal(
            JsonValueKind.Null,
            document.RootElement.GetProperty("totals").GetProperty("apiEquivalentUsd").ValueKind);
    }

    private static string Expected(string template) =>
        template.ReplaceLineEndings("\n").Replace("<version>", GhLedgerCli.Version, StringComparison.Ordinal);

    private static async Task<CliHarness> SeededHarnessAsync(string root)
    {
        GitFixture.Create(root);
        CliHarness harness = new(root, new FakeCliPlatform(), Time);
        SqliteLedgerStore store = new(Path.Combine(root, "data", "ledger.db"), harness.TimeProvider);
        await store.InitializeAsync();
        await ReceiptSeeds.SeedAsync(store, root);
        return harness;
    }
}
