using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using CopilotLedger.Storage;
using CopilotLedger.Tests.TestSupport;
using Microsoft.Data.Sqlite;

namespace CopilotLedger.Tests.Cli;

// What gh ledger import may keep: only the columns that docs/cli.md lists between its import-allowlist markers, and nothing
// from a content-bearing field of a session log.
public sealed partial class ImportPrivacyTests
{
    private const string S1 = "11111111-1111-4111-8111-111111111111";
    private const string S2 = "22222222-2222-4222-8222-222222222222";
    private const string S3 = "33333333-3333-4333-8333-333333333333";
    private const string S4 = "44444444-4444-4444-8444-444444444444";
    private const string Thread = "019a1b2c-3d4e-7f50-8a6b-7c8d9e0f1a2b";

    private static readonly DateTimeOffset Now = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);

    // The columns that imported rows (spans), imported tool calls (span_links), and import's own tables leave empty.
    private static readonly Dictionary<string, string[]> LeftEmpty = new(StringComparer.Ordinal)
    {
        ["spans"] = ["agent_span_id", "parent_span_id", "pull_request_number", "server_address", "server_port"],
        ["span_links"] = ["git_branch", "git_commit_sha", "git_repository_slug", "parent_span_id"],
        ["import_sessions"] = [],
        ["import_record_aliases"] = [],
    };

    private static readonly string[] Canaries =
    [
        CopilotSessionLogs.Canary,
        Canary("PROMPT"),
        Canary("RESPONSE"),
        Canary("ARGS"),
        Canary("RESULT"),
        Canary("PATH"),
        Canary("ERRMSG"),
        Canary("TOKEN"),
    ];

    [Fact]
    public async Task IP1_NoCanaryReachesTheLedgerOrTheOutput()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = Harness(temp);
        WriteFixtures(temp, harness);

        CliRun claude = await harness.RunAsync("import", "--all", "--source", "claude");
        CliRun dryRun = await harness.RunAsync("import", "--all", "--source", "copilot", "--source", "codex", "--dry-run");
        CliRun text = await harness.RunAsync("import", "--all", "--source", "copilot", "--source", "codex");
        CliRun json = await harness.RunAsync("import", "--all", "--source", "copilot", "--source", "codex", "--json");

        Assert.Equal((1, 2, 2, 2), (claude.ExitCode, dryRun.ExitCode, text.ExitCode, json.ExitCode));
        Assert.Contains("Recorded 6 requests: ", text.Out);
        Assert.Contains("Warning: Copilot session s-canary, line ", text.Error);
        Assert.Equal(6L, await LedgerSql.ScalarAsync(harness.Context.DatabasePath, "SELECT COUNT(*) FROM spans;"));
        Assert.Equal(3L, await LedgerSql.ScalarAsync(harness.Context.DatabasePath, "SELECT COUNT(*) FROM span_links;"));
        SqliteConnection.ClearAllPools();
        List<(string Name, byte[] Bytes)> stored = [];
        foreach (string suffix in new[] { "", "-wal", "-shm" })
        {
            string path = harness.Context.DatabasePath + suffix;
            if (File.Exists(path))
            {
                await using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using MemoryStream copy = new();
                await stream.CopyToAsync(copy);
                stored.Add(("ledger.db" + suffix, copy.ToArray()));
            }
        }

        Assert.Contains(stored, file => file.Name == "ledger.db");
        string daemonLog = Path.Combine(harness.Context.DataDirectory, "daemon.log");
        if (File.Exists(daemonLog))
        {
            stored.Add(("daemon.log", await File.ReadAllBytesAsync(daemonLog)));
        }

        string output = string.Concat(
            claude.Out, claude.Error, dryRun.Out, dryRun.Error, text.Out, text.Error, json.Out, json.Error);
        foreach (string canary in Canaries)
        {
            Assert.DoesNotContain(canary, output, StringComparison.Ordinal);
            byte[] bytes = Encoding.UTF8.GetBytes(canary);
            foreach ((string name, byte[] content) in stored)
            {
                Assert.True(content.AsSpan().IndexOf(bytes) < 0, $"{canary} is in {name}.");
            }
        }
    }

    [Fact]
    public async Task IP2_TheSchemaIsTheDocumentedColumnsAndTheOnesImportLeavesEmpty()
    {
        using TempDirectory temp = new();
        string database = Path.Combine(temp.Path, "ledger.db");
        await new SqliteLedgerStore(database).InitializeAsync();
        Dictionary<string, SortedSet<string>> documented = DocumentedColumns();

        Assert.Equal(LeftEmpty.Keys.Order(StringComparer.Ordinal), documented.Keys.Order(StringComparer.Ordinal));
        foreach ((string table, string[] empty) in LeftEmpty)
        {
            Assert.Empty(documented[table].Intersect(empty));
            Assert.Equal(
                documented[table].Union(empty).Order(StringComparer.Ordinal),
                await LedgerSql.RowsAsync(database, $"SELECT name FROM pragma_table_info('{table}') ORDER BY name;"));
        }
    }

    [Fact]
    public async Task IP3_ImportFillsExactlyTheDocumentedColumns()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = Harness(temp);
        WriteFixtures(temp, harness);
        CliRun run = await harness.RunAsync("import", "--all", "--source", "copilot", "--source", "codex");
        SqliteLedgerStore store = new(harness.Context.DatabasePath, harness.TimeProvider);
        await store.InitializeAsync();
        await store.RecordAsync([ImportEntries.Live("s-repo", S4)]);
        Dictionary<string, SortedSet<string>> documented = DocumentedColumns();

        Assert.Equal(2, run.ExitCode);
        foreach ((string table, string imported) in new[]
                 {
                     ("spans", "ingest_source <> 'otlp'"),
                     ("span_links", "owner_span_row_id IS NOT NULL"),
                     ("import_sessions", "1"),
                     ("import_record_aliases", "1"),
                 })
        {
            SortedSet<string> filled = new(StringComparer.Ordinal);
            foreach (string column in await LedgerSql.RowsAsync(
                         harness.Context.DatabasePath,
                         $"SELECT name FROM pragma_table_info('{table}');"))
            {
                if ((long)(await LedgerSql.ScalarAsync(
                        harness.Context.DatabasePath,
                        $"SELECT COUNT(*) FROM {table} WHERE {imported} AND \"{column}\" IS NOT NULL;"))! > 0)
                {
                    filled.Add(column);
                }
            }

            Assert.Equal(documented[table], filled);
        }
    }

    // While the ledger can't record a canary-bearing live span, after an import, the daemon's log names the failure and keeps
    // nothing of the span. The in-process daemon writes to DaemonOutput what it writes to daemon.log when it's started.
    [Fact]
    public async Task IP4_TheDaemonLogKeepsNoCanaryWhenItCantRecordASpan()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = Harness(temp);
        WriteFixtures(temp, harness);
        CliRun import = await harness.RunAsync("import", "--all", "--source", "copilot", "--source", "codex");
        await harness.StartInProcessDaemonAsync();
        await LedgerSql.ExecuteAsync(
            harness.Context.DatabasePath,
            "CREATE TRIGGER ip4_reject BEFORE INSERT ON spans BEGIN SELECT RAISE(ABORT, 'ip4 rejects spans'); END;");
        string trace = OtlpFixtures.CopilotChatTraceJson
            .Replace("0f8fad5b-d9cb-469f-a165-70867728950e", Canary("PROMPT"), StringComparison.Ordinal)
            .Replace("\"stringValue\": \"stop\"", $"\"stringValue\": \"{Canary("RESPONSE")}\"", StringComparison.Ordinal);
        HttpStatusCode status;
        using (HttpClient client = harness.CreateDaemonClient())
        using (ByteArrayContent content = new(Encoding.UTF8.GetBytes(trace)))
        {
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            using HttpResponseMessage response = await client.PostAsync("/v1/traces", content);
            status = response.StatusCode;
        }

        CliRun stop = await harness.RunAsync("daemon", "stop");
        CliRun daemon = await harness.InProcessDaemon!;
        string daemonLog = Path.Combine(harness.Context.DataDirectory, "daemon.log");
        string log = harness.DaemonOutput + (File.Exists(daemonLog) ? await File.ReadAllTextAsync(daemonLog) : "");

        Assert.Equal(2, import.ExitCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, status);
        Assert.Equal((0, 0), (stop.ExitCode, daemon.ExitCode));
        Assert.Contains(" Warning: could not record spans: SqliteException: ", log);
        Assert.Contains("ip4 rejects spans", log);
        foreach (string canary in Canaries)
        {
            Assert.DoesNotContain(canary, log, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task IP_AM1_AssistantMessageCanariesReachNoLedgerOrOutput()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = Harness(temp);
        CopilotSessionLogs log = new CopilotSessionLogs("s-assistant").AssistantMessage("api-A");
        string message = log.LastId;
        log.Usage("u1", S1, numToolCalls: 1)
            .ToolStart("t1", "bash", parentId: message)
            .ToolComplete("t1", true, exitCode: 0)
            .Write(harness.Context.CopilotHome);

        CliRun text = await harness.RunAsync("import", "--all", "--source", "copilot");
        CliRun json = await harness.RunAsync("import", "--all", "--source", "copilot", "--json");
        SqliteConnection.ClearAllPools();
        string output = text.Out + text.Error + json.Out + json.Error;
        byte[] canary = Encoding.UTF8.GetBytes(CopilotSessionLogs.Canary);

        Assert.Equal((0, 0), (text.ExitCode, json.ExitCode));
        Assert.DoesNotContain(CopilotSessionLogs.Canary, output, StringComparison.Ordinal);
        foreach (string suffix in new[] { "", "-wal", "-shm" })
        {
            string path = harness.Context.DatabasePath + suffix;
            if (File.Exists(path))
            {
                byte[] ledger = await FileRetry.ReadAllBytesAsync(path);
                Assert.True(ledger.AsSpan().IndexOf(canary) < 0);
            }
        }

        Assert.Equal(1L, await LedgerSql.ScalarAsync(harness.Context.DatabasePath, "SELECT COUNT(*) FROM spans;"));
        Assert.Equal(1L, await LedgerSql.ScalarAsync(harness.Context.DatabasePath, "SELECT COUNT(*) FROM span_links;"));
    }

    private static string Canary(string kind) => $"CANARY-{kind}-7c1d";

    private static CliHarness Harness(TempDirectory temp) => new(temp.Path, new FakeCliPlatform(), new ManualTimeProvider(Now));

    // A Copilot session with every kind of record, sub-agent, and tool call, and canaries in content-bearing fields, in
    // lines that aren't events, and in a working directory that's read but never kept; one in a repository whose remote URL
    // has a credential; and a Codex rollout with canaries in its content and its remote URL.
    private static void WriteFixtures(TempDirectory temp, CliHarness harness)
    {
        string cwd = Path.Combine(temp.Path, Canary("PATH"), "work");
        new CopilotSessionLogs("s-canary", "1.0.114", context: new JsonObject { ["cwd"] = cwd })
            .Event("user.message", new JsonObject { ["content"] = Canary("PROMPT"), ["transformedContent"] = Canary("PROMPT") })
            .Raw("{\"data\":{\"content\":\"" + Canary("PROMPT") + "\"},\"type\":\"user.message\"}")
            .Usage(
                "u1",
                S1,
                responseModel: "claude-haiku-4-5-20251001",
                numToolCalls: 2,
                change: usage => usage[Canary("ARGS")] = Canary("ARGS"))
            .Event("assistant.message", new JsonObject { ["content"] = Canary("RESPONSE"), ["reasoningText"] = Canary("RESPONSE") })
            .ToolStart("toolu-1", "bash")
            .ToolComplete("toolu-1", true, exitCode: 0)
            .Event(
                "tool.execution_start",
                new JsonObject
                {
                    ["toolCallId"] = "toolu-2",
                    ["toolName"] = "view",
                    ["arguments"] = new JsonObject { ["path"] = Canary("PATH"), ["command"] = Canary("ARGS") },
                })
            .Event(
                "tool.execution_complete",
                new JsonObject
                {
                    ["toolCallId"] = "toolu-2",
                    ["success"] = false,
                    ["result"] = new JsonObject { ["content"] = Canary("RESULT") },
                    ["error"] = new JsonObject { ["code"] = "failure", ["message"] = Canary("ERRMSG") },
                })
            .SubagentStarted("agent-1", "explore", "toolu-9")
            .Usage("u2", S2, agentId: "agent-1", parentToolCallId: "toolu-9")
            .ToolStart("toolu-3", "grep", agentId: "agent-1")
            .ToolComplete("toolu-3", false, agentId: "agent-1")
            .Usage("u3", null, model: "qwen3.8-27b", providerType: "ollama")
            .Compaction(
                S3,
                change: data =>
                {
                    data["summaryContent"] = Canary("RESPONSE");
                    data["checkpointPath"] = Canary("PATH");
                    data["error"] = Canary("ERRMSG");
                    data["failureReason"] = Canary("ERRMSG");
                })
            .Raw("{\"type\":\"user.message\",\"data\":{\"content\":\"" + Canary("PROMPT") + "\"")
            .Raw("{\"type\":\"" + Canary("ERRMSG") + "\",\"data\":{\"" + Canary("ARGS") + "\":1}}")
            .Write(harness.Context.CopilotHome);
        string repo = GitFixture.Create(temp.Path);
        new CopilotSessionLogs(
                "s-repo",
                context: CopilotSessionLogs.Context(
                    repo,
                    repo,
                    "feature/import",
                    $"https://user:{Canary("TOKEN")}@github.com/octo-org/octo-repo.git",
                    GitFixture.Sha2))
            .UserMessage()
            .Usage("u4", S4)
            .Write(harness.Context.CopilotHome);
        new CodexRollouts(
                Thread,
                agentRole: "reviewer",
                cwd: cwd,
                git: new JsonObject
                {
                    ["commit_hash"] = GitFixture.Sha1,
                    ["branch"] = "main",
                    ["repository_url"] = $"https://{Canary("TOKEN")}@github.com/octo-org/octo-repo.git",
                })
            .TurnContext("turn-1", "gpt-6.1-sol")
            .UserMessage()
            .Line(
                "response_item",
                new JsonObject { ["type"] = "function_call", ["name"] = "shell", ["arguments"] = Canary("ARGS"), ["call_id"] = "call-1" })
            .Line(
                "response_item",
                new JsonObject { ["type"] = "function_call_output", ["call_id"] = "call-1", ["output"] = Canary("RESULT") })
            .Line("event_msg", new JsonObject { ["type"] = "agent_message", ["message"] = Canary("RESPONSE") })
            .Line("event_msg", new JsonObject { ["type"] = "error", ["message"] = Canary("ERRMSG") })
            .Usage("resp-1", "turn-1", change: payload => payload[Canary("ARGS")] = Canary("PROMPT"))
            .Write(harness.Context.CodexHome);
    }

    // The table.column names in the first column of docs/cli.md's import allowlist, by table.
    private static Dictionary<string, SortedSet<string>> DocumentedColumns()
    {
        string[] lines = File.ReadAllLines(RepositoryFile("docs", "cli.md"));
        int start = Array.IndexOf(lines, "<!-- import-allowlist -->");
        int end = Array.IndexOf(lines, "<!-- /import-allowlist -->");
        Assert.True(start >= 0 && end > start);
        Dictionary<string, SortedSet<string>> columns = new(StringComparer.Ordinal);
        foreach (string line in lines[(start + 1)..end].Where(line => line.StartsWith("| `", StringComparison.Ordinal)))
        {
            foreach (Match match in ColumnName().Matches(line.Split('|')[1]))
            {
                string table = match.Groups[1].Value;
                if (!columns.TryGetValue(table, out SortedSet<string>? names))
                {
                    names = new SortedSet<string>(StringComparer.Ordinal);
                    columns.Add(table, names);
                }

                Assert.True(names.Add(match.Groups[2].Value), $"{match.Value} is listed twice.");
            }
        }

        return columns;
    }

    // A file of the repository, found from the test's output directory: the tests copy only some docs there.
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

    [GeneratedRegex(@"`([a-z_]+)\.([a-z0-9_]+)`")]
    private static partial Regex ColumnName();
}
