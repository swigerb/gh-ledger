using System.Net;
using System.Text.Json;
using CopilotLedger.Cli;
using CopilotLedger.Storage;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Cli;

// audit --local-llm and --show-llm-payload, against FakeLlmHandler: no socket is opened. LA8 is the only test that waits on
// real time (1 second).
public sealed class LocalLlmTests
{
    private const string Url = "http://localhost:11434/v1";

    public static TheoryData<string[], string> InvalidOptions => new()
    {
        { ["--local-llm", "https://api.openai.com/v1", "--local-llm-model", "m"], "Invalid --local-llm value: use localhost, an address in 127.0.0.0/8, or [::1]; other hosts aren't allowed." },
        { ["--local-llm", Url, "--local-llm-model", ""], "--local-llm-model can't be blank or longer than 128 characters." },
        { ["--local-llm", Url, "--local-llm-model", new string('m', 129)], "--local-llm-model can't be blank or longer than 128 characters." },
        { ["--local-llm", Url, "--local-llm-model", "m", "--local-llm-timeout", "0"], "--local-llm-timeout must be from 1 to 600." },
        { ["--local-llm", Url, "--local-llm-model", "m", "--local-llm-timeout", "601"], "--local-llm-timeout must be from 1 to 600." },
        { ["--local-llm", Url], "--local-llm requires --local-llm-model." },
        { ["--local-llm-model", "m"], "--local-llm-model requires --local-llm." },
        { ["--local-llm-timeout", "5"], "--local-llm-timeout requires --local-llm." },
        { ["--show-llm-payload"], "--show-llm-payload requires --local-llm." },
        { ["--local-llm", Url, "--local-llm-model", "m", "--json"], "--local-llm can't be used with --json." },
        { ["--local-llm", Url, "--local-llm-model", "m", "--show-llm-payload", "--fail-on", "high"], "--fail-on can't be used with --show-llm-payload." },
    };

    [Theory]
    [MemberData(nameof(InvalidOptions))]
    public async Task LA1_LocalModelOptions_AreValidatedBeforeAnythingIsRead(string[] options, string message)
    {
        using TempDirectory temp = new();
        await using CliHarness harness = Harness(temp.Path);
        int created = 0;

        CliRun run = await harness.RunWithLocalLlmAsync(() => { created++; return Answering("x"); }, ["audit", .. options]);

        Assert.Equal(1, run.ExitCode);
        Assert.Contains(message, run.Error);
        Assert.DoesNotContain("api.openai.com", run.Error);
        Assert.Equal(0, created);
        Assert.False(Directory.Exists(harness.Context.DataDirectory));
    }

    [Fact]
    public async Task LA2_AdditionalHostsInTheRates_NeverAllowAnotherHost()
    {
        using TempDirectory temp = new();
        await AuditSeeds.CreateAsync(temp.Path);
        await using CliHarness harness = Harness(temp.Path);
        File.WriteAllText(
            harness.Context.RatesOverlayPath,
            """{ "schemaVersion": 1, "localOffload": { "additionalHosts": ["10.0.0.5"] } }""");

        CliRun run = await harness.RunWithLocalLlmAsync(
            () => Answering("x"),
            "audit", "--local-llm", "http://10.0.0.5:11434/v1", "--local-llm-model", "llama3.3");

        Assert.Equal(1, run.ExitCode);
        Assert.Contains("Invalid --local-llm value: use localhost, an address in 127.0.0.0/8, or [::1]; other hosts aren't allowed.", run.Error);
    }

    [Fact]
    public async Task LA3_TheAnswer_IsSanitizedAndShownAfterTheTable()
    {
        using TempDirectory temp = new();
        await AuditSeeds.CreateAsync(temp.Path);
        await using CliHarness harness = Harness(temp.Path);
        FakeLlmHandler handler = Answering("1. Fix \u001B[31mthe bash\u001B[0m failures.\r\n2. Try mai-code-2.\u202E\n\n");

        CliRun run = await harness.RunWithLocalLlmAsync(() => handler, "audit", "--local-llm", Url, "--local-llm-model", "llama3.3");

        Assert.Equal(0, run.ExitCode);
        Assert.Equal("", run.Error);
        Assert.Equal(
            AuditOutputs.Table + "\n\nLocal model suggestions (llama3.3, advisory, not stored)\n1. Fix the bash failures.\n2. Try mai-code-2.\n",
            run.Out);
        Assert.Single(handler.Calls);
    }

    [Fact]
    public async Task LA4_TheRequest_GoesToThePinnedAddressWithTheNameInHostAndNoCredentials()
    {
        using TempDirectory temp = new();
        await AuditSeeds.CreateAsync(temp.Path);
        await using CliHarness harness = Harness(temp.Path);
        FakeLlmHandler handler = Answering("ok");

        await harness.RunWithLocalLlmAsync(() => handler, "audit", "--local-llm", Url, "--local-llm-model", "llama3.3");
        CliRun payload = await harness.RunWithLocalLlmAsync(
            () => throw new InvalidOperationException(),
            "audit", "--local-llm", Url, "--local-llm-model", "llama3.3", "--show-llm-payload");

        LlmCall call = Assert.Single(handler.Calls);
        Assert.Equal("POST", call.Method);
        Assert.Equal("http://127.0.0.1:11434/v1/chat/completions", call.Uri);
        Assert.Equal("localhost:11434", call.Host);
        Assert.Equal("application/json", call.ContentType);
        Assert.Null(call.Authorization);
        Assert.Equal("application/json", call.Accept);
        Assert.Equal($"gh-ledger/{GhLedgerCli.Version}", call.UserAgent);
        Assert.Equal(payload.Out, call.Body + "\n");
    }

    [Fact]
    public async Task LA5_ShowLlmPayload_PrintsTheBodyAndSendsNothing()
    {
        using TempDirectory temp = new();
        await AuditSeeds.CreateAsync(temp.Path);
        await using CliHarness harness = Harness(temp.Path);
        int created = 0;

        CliRun run = await harness.RunWithLocalLlmAsync(
            () => { created++; return Answering("x"); },
            "audit", "--local-llm", Url, "--local-llm-model", "llama3.3", "--show-llm-payload", "--min-severity", "warning");

        Assert.Equal((0, ""), (run.ExitCode, run.Error));
        Assert.Equal(0, created);
        Assert.EndsWith("}\n", run.Out);
        Assert.Single(run.Out.Split('\n', StringSplitOptions.RemoveEmptyEntries));
        using JsonDocument body = JsonDocument.Parse(run.Out);
        JsonElement root = body.RootElement;
        Assert.Equal(["model", "temperature", "stream", "messages"], root.EnumerateObject().Select(property => property.Name));
        Assert.Equal("llama3.3", root.GetProperty("model").GetString());
        Assert.Equal(0, root.GetProperty("temperature").GetInt32());
        Assert.False(root.GetProperty("stream").GetBoolean());
        JsonElement[] messages = [.. root.GetProperty("messages").EnumerateArray()];
        Assert.Equal(["system", "user"], messages.Select(message => message.GetProperty("role").GetString()));
        Assert.StartsWith("You review a cost audit from gh-ledger", messages[0].GetProperty("content").GetString());
        using JsonDocument summary = JsonDocument.Parse(messages[1].GetProperty("content").GetString()!);
        JsonElement content = summary.RootElement;
        Assert.Equal("gh-ledger audit", content.GetProperty("source").GetString());
        Assert.Equal("last 7 days", content.GetProperty("period").GetString());
        Assert.Equal("2026-11-15T05:00:00.0000000+00:00", content.GetProperty("windowStart").GetString());
        Assert.Equal(4, content.GetProperty("findingsTotal").GetInt32());
        Assert.Equal(0, content.GetProperty("findingsOmitted").GetInt32());
        Assert.Equal(
            ["failed_tool_retry", "premium_model_trivial_turn", "cache_miss_streak"],
            content.GetProperty("findings").EnumerateArray().Select(finding => finding.GetProperty("ruleId").GetString()));
    }

    [Fact]
    public async Task LA6_ARedirect_IsNotFollowed()
    {
        using TempDirectory temp = new();
        await AuditSeeds.CreateAsync(temp.Path);
        await using CliHarness harness = Harness(temp.Path);
        FakeLlmHandler handler = FakeLlmHandler.Returning(() =>
        {
            HttpResponseMessage response = new(HttpStatusCode.Found);
            response.Headers.Location = new Uri("https://api.openai.com/v1/chat/completions");
            return response;
        });

        CliRun run = await harness.RunWithLocalLlmAsync(() => handler, "audit", "--local-llm", Url, "--local-llm-model", "llama3.3");

        Assert.Equal(0, run.ExitCode);
        Assert.Equal(AuditOutputs.Table + "\n", run.Out);
        Assert.Equal($"Warning: the local model at {Url} answered HTTP 302; no suggestions.\n", run.Error);
        Assert.Single(handler.Calls);
    }

    [Fact]
    public async Task LA7_AServerError_IsAWarning()
    {
        using TempDirectory temp = new();
        await AuditSeeds.CreateAsync(temp.Path);
        await using CliHarness harness = Harness(temp.Path);

        CliRun run = await harness.RunWithLocalLlmAsync(
            () => FakeLlmHandler.Returning(() => FakeLlmHandler.Json("{\"error\":\"model not found\"}", HttpStatusCode.InternalServerError)),
            "audit", "--local-llm", "http://127.0.0.1:5273/v1", "--local-llm-model", "phi-5");

        Assert.Equal(0, run.ExitCode);
        Assert.Equal(AuditOutputs.Table + "\n", run.Out);
        Assert.Equal("Warning: the local model at http://127.0.0.1:5273/v1 answered HTTP 500; no suggestions.\n", run.Error);
    }

    // Waits on real time: the only such test.
    [Fact]
    public async Task LA8_ASlowModel_TimesOutAfterTheTimeout()
    {
        using TempDirectory temp = new();
        await AuditSeeds.CreateAsync(temp.Path);
        await using CliHarness harness = Harness(temp.Path);
        FakeLlmHandler handler = new(async (_, token) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return FakeLlmHandler.Answer("late");
        });

        CliRun run = await harness.RunWithLocalLlmAsync(
            () => handler,
            "audit", "--local-llm", Url, "--local-llm-model", "llama3.3", "--local-llm-timeout", "1");

        Assert.Equal(0, run.ExitCode);
        Assert.Equal(AuditOutputs.Table + "\n", run.Out);
        Assert.Equal($"Warning: the local model at {Url} didn't answer within 1 second; no suggestions.\n", run.Error);
    }

    [Fact]
    public async Task LA9_ACanceledRequest_IsReportedAsATimeout()
    {
        using TempDirectory temp = new();
        await AuditSeeds.CreateAsync(temp.Path);
        await using CliHarness harness = Harness(temp.Path);

        CliRun run = await harness.RunWithLocalLlmAsync(
            () => new FakeLlmHandler((_, _) => throw new TaskCanceledException()),
            "audit", "--local-llm", Url, "--local-llm-model", "llama3.3");

        Assert.Equal(0, run.ExitCode);
        Assert.Equal($"Warning: the local model at {Url} didn't answer within 60 seconds; no suggestions.\n", run.Error);
    }

    public static TheoryData<string> MalformedAnswers => new()
    {
        "not json",
        """{"choices":[]}""",
        """{"choices":[{"message":{"role":"assistant","content":null}}]}""",
        """{"choices":[{"message":{"role":"assistant","content":[{"type":"text","text":"hi"}]}}]}""",
        """{"choices":[{"message":{"role":"assistant","content":"\ud800x"}}]}""",
    };

    [Theory]
    [MemberData(nameof(MalformedAnswers))]
    public async Task LA10_AnAnswerWithoutText_IsAWarning(string answer)
    {
        using TempDirectory temp = new();
        await AuditSeeds.CreateAsync(temp.Path);
        await using CliHarness harness = Harness(temp.Path);

        CliRun run = await harness.RunWithLocalLlmAsync(
            () => FakeLlmHandler.Returning(() => FakeLlmHandler.Json(answer)),
            "audit", "--local-llm", Url, "--local-llm-model", "llama3.3");

        Assert.Equal(0, run.ExitCode);
        Assert.Equal(AuditOutputs.Table + "\n", run.Out);
        Assert.Equal(
            $"Warning: the local model at {Url} answered without text in choices[0].message.content; no suggestions.\n",
            run.Error);
    }

    [Fact]
    public async Task LA11_AnUnreachableModel_IsAWarningWithoutTheErrorsText()
    {
        using TempDirectory temp = new();
        await AuditSeeds.CreateAsync(temp.Path);
        await using CliHarness harness = Harness(temp.Path);
        const string canary = "canary-7f3e9a";

        CliRun run = await harness.RunWithLocalLlmAsync(
            () => new FakeLlmHandler((_, _) => throw new HttpRequestException($"connection refused {canary}")),
            "audit", "--local-llm", Url, "--local-llm-model", "llama3.3");

        Assert.Equal(0, run.ExitCode);
        Assert.Equal($"Warning: couldn't reach the local model at {Url}; no suggestions.\n", run.Error);
        Assert.DoesNotContain(canary, run.Out + run.Error);
    }

    [Fact]
    public async Task LA12_ThePayload_HasAliasesAndNoIdentifyingValues()
    {
        using TempDirectory temp = new();
        await AuditSeeds.CreateAsync(temp.Path);
        await using CliHarness harness = Harness(temp.Path);

        CliRun run = await harness.RunWithLocalLlmAsync(
            () => throw new InvalidOperationException(),
            "audit", "--local-llm", Url, "--local-llm-model", "llama3.3", "--show-llm-payload");

        string[] secrets =
        [
            AuditSeeds.R, AuditSeeds.P, AuditSeeds.M, AuditSeeds.B, AuditSeeds.SpanId(AuditSeeds.R, 'c', 1), "conv-r", "conv-p", "conv-m",
            "conv-b", "conv-i", "conv-x", "swigerb", "copilot-ledger", "feature/ledger", "\\\"main\\\"", temp.Path,
            temp.Path.Replace("\\", "\\\\", StringComparison.Ordinal), GitFixture.Sha1, "github.com", "pullRequest", "traceId",
            "conversationId", "localhost", "11434",
        ];
        Assert.All(secrets, secret => Assert.DoesNotContain(secret, run.Out));
        using JsonDocument body = JsonDocument.Parse(run.Out);
        using JsonDocument summary = JsonDocument.Parse(body.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!);
        Assert.Equal(
            ["repo-1 branch-1 explore bash", "repo-1 branch-2 (none) -", "repo-1 branch-1 (none) -", "repo-1 branch-1 (none) -"],
            summary.RootElement.GetProperty("findings").EnumerateArray().Select(finding =>
            {
                JsonElement scope = finding.GetProperty("scope");
                return $"{scope.GetProperty("repository").GetString()} {scope.GetProperty("branch").GetString()} " +
                    $"{scope.GetProperty("agent").GetString()} {scope.GetProperty("tool").GetString() ?? "-"}";
            }));
    }

    [Fact]
    public async Task LA13_ThePayload_FitsIn32KiBAndCountsWhatItLeftOut()
    {
        using TempDirectory temp = new();
        SqliteLedgerStore store = await AuditSeeds.CreateAsync(temp.Path);
        AuditSeeds.LiveTrace bloat = new("b1b1b1b1b1b1b1b1b1b1b1b1b1b1b1b1", "conv-bloat", null);
        for (int k = 0; k < 150; k++)
        {
            DateTimeOffset start = AuditSeeds.At("19:00:00").AddSeconds(20 * k);
            string agent = bloat.Agent(k, start);
            bloat.Request(1 + k, agent, start, "claude-sonnet-5.5", 60_000, 50_000, 100);
        }

        await bloat.RecordAsync(store);
        await using CliHarness harness = Harness(temp.Path);

        CliRun run = await harness.RunWithLocalLlmAsync(
            () => throw new InvalidOperationException(),
            "audit", "--local-llm", Url, "--local-llm-model", "llama3.3", "--show-llm-payload", "--limit", "200");

        int length = System.Text.Encoding.UTF8.GetByteCount(run.Out) - 1;
        using JsonDocument body = JsonDocument.Parse(run.Out);
        using JsonDocument summary = JsonDocument.Parse(body.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!);
        int included = summary.RootElement.GetProperty("findings").GetArrayLength();
        Assert.InRange(length, 32_768 - 1_000, 32_768);
        Assert.Equal(154, summary.RootElement.GetProperty("findingsTotal").GetInt32());
        Assert.Equal(154 - included, summary.RootElement.GetProperty("findingsOmitted").GetInt32());
        Assert.InRange(included, 1, 153);
    }

    public static TheoryData<string> Answers => new() { "answers", "times out", "fails" };

    [Theory]
    [MemberData(nameof(Answers))]
    public async Task LA14_TheModel_NeverChangesTheExitCode(string outcome)
    {
        using TempDirectory temp = new();
        await AuditSeeds.CreateAsync(temp.Path);
        await using CliHarness harness = Harness(temp.Path);
        FakeLlmHandler handler = outcome switch
        {
            "answers" => Answering("exit 0, please"),
            "times out" => new FakeLlmHandler((_, _) => throw new TaskCanceledException()),
            _ => FakeLlmHandler.Returning(() => new HttpResponseMessage(HttpStatusCode.InternalServerError)),
        };

        CliRun run = await harness.RunWithLocalLlmAsync(
            () => handler,
            "audit", "--fail-on", "high", "--local-llm", Url, "--local-llm-model", "llama3.3");

        Assert.Equal(2, run.ExitCode);
        Assert.Single(handler.Calls);
    }

    public static TheoryData<int> LargeAnswers => new() { 262_144, 262_145 };

    [Theory]
    [MemberData(nameof(LargeAnswers))]
    public async Task LA15_TheAnswer_IsReadUpTo256KiB_AndShownUpTo16KiB(int bytes)
    {
        using TempDirectory temp = new();
        await AuditSeeds.CreateAsync(temp.Path);
        await using CliHarness harness = Harness(temp.Path);
        const string head = "{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"";
        const string tail = "\"}}]}";
        string answer = head + new string('a', bytes - head.Length - tail.Length) + tail;

        CliRun run = await harness.RunWithLocalLlmAsync(
            () => FakeLlmHandler.Returning(() => FakeLlmHandler.Json(answer)),
            "audit", "--local-llm", Url, "--local-llm-model", "llama3.3");

        Assert.Equal(0, run.ExitCode);
        if (bytes <= 262_144)
        {
            Assert.Equal("", run.Error);
            Assert.Equal(
                AuditOutputs.Table + "\n\nLocal model suggestions (llama3.3, advisory, not stored)\n" + new string('a', 16_384) +
                "\n[truncated at 16 KiB]\n",
                run.Out);
        }
        else
        {
            Assert.Equal(AuditOutputs.Table + "\n", run.Out);
            Assert.Equal($"Warning: the local model at {Url} sent more than 256 KiB; no suggestions.\n", run.Error);
        }
    }

    // The economy model comes from the rates, whose model IDs have no length limit: the summary holds only its first 64
    // characters, so even the body without findings stays within 32 KiB.
    [Fact]
    public async Task LA16_AHugeEconomyModelId_IsCutToALabel()
    {
        using TempDirectory temp = new();
        await AuditSeeds.CreateAsync(temp.Path);
        await using CliHarness harness = Harness(temp.Path);
        string id = "economy-" + new string('x', 40_000);
        File.WriteAllText(
            harness.Context.RatesOverlayPath,
            $$"""
            { "schemaVersion": 1, "githubAiCredits": { "models": [ { "id": "{{id}}", "windows": [ { "tiers": [ { "name": "default", "inputPer1M": 1, "outputPer1M": 5 } ] } ] } ] }, "localOffload": { "baselineModel": "{{id}}" } }
            """);

        CliRun run = await harness.RunWithLocalLlmAsync(
            () => throw new InvalidOperationException(),
            "audit", "--local-llm", Url, "--local-llm-model", "llama3.3", "--show-llm-payload");

        Assert.Equal((0, ""), (run.ExitCode, run.Error));
        Assert.InRange(System.Text.Encoding.UTF8.GetByteCount(run.Out) - 1, 1, 32_768);
        using JsonDocument body = JsonDocument.Parse(run.Out);
        using JsonDocument summary = JsonDocument.Parse(body.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!);
        Assert.Equal(id[..64], summary.RootElement.GetProperty("economyModel").GetString());
        Assert.Equal(4, summary.RootElement.GetProperty("findings").GetArrayLength());
    }

    private static CliHarness Harness(string root) =>
        new(root, new FakeCliPlatform(), new ManualTimeProvider(AuditSeeds.GeneratedAt, ManualTimeProvider.UtcMinus5));

    private static FakeLlmHandler Answering(string content) => FakeLlmHandler.Returning(() => FakeLlmHandler.Answer(content));
}
