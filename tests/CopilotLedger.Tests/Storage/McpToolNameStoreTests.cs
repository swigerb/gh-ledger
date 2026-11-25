using CopilotLedger.Core.Import;
using CopilotLedger.Storage;
using CopilotLedger.Tests.TestSupport;
using Microsoft.Data.Sqlite;

namespace CopilotLedger.Tests.Storage;

public sealed class McpToolNameStoreTests
{
    private const string LedgerHash =
        "fe14010b4fe83303852f0467c919ef9a7ca089b91e96e3aad7d426dd87079297/eb20c46435d60871da0eb97ce23417c04835e272725c59a3a649c7ce8e521d97";

    private const string ExecJs =
        "10be6beef496b89dae799c37bd04e5eba2a6264a0a4d605f5661b50d7dc4d1ea/8453dc19fda259c463d7aaffb07becf26b7ce2cfc690a2b0a957e201653dc85a";

    private const string R1 = "11111111-1111-4111-8111-111111111111";

    [Fact]
    public async Task MT1_ANewLedger_HasTheTable()
    {
        using TempDirectory temp = new();
        string path = Path.Combine(temp.Path, "ledger.db");

        await new SqliteLedgerStore(path).InitializeAsync();

        Assert.Equal(
            ["hashed_name|TEXT|1|1", "tool_name|TEXT|0|1"],
            await LedgerSql.RowsAsync(
                path,
                """
                SELECT name || '|' || type || '|' || pk || '|' || "notnull"
                  FROM pragma_table_info('mcp_tool_names')
                 ORDER BY cid;
                """));
        Assert.Equal(
            "table|2|1|1",
            await LedgerSql.ScalarAsync(
                path,
                "SELECT type || '|' || ncol || '|' || wr || '|' || strict FROM pragma_table_list('mcp_tool_names');"));
        Assert.Equal(7L, await LedgerSql.ScalarAsync(path, "PRAGMA user_version;"));
    }

    [Fact]
    public async Task MT2_AFormat7LedgerWithoutTheTable_GainsIt()
    {
        using TempDirectory temp = new();
        string path = Path.Combine(temp.Path, "ledger.db");
        SqliteLedgerStore store = new(path);
        await store.InitializeAsync();
        await store.RecordAsync([ImportEntries.Live("c-1", null)]);
        SqliteConnection.ClearAllPools();
        await LedgerSql.ExecuteAsync(path, "DROP TABLE mcp_tool_names;");
        SqliteConnection.ClearAllPools();

        await new SqliteLedgerStore(path).InitializeAsync();

        Assert.Equal(1L, await TableCount(path));
        Assert.Equal(7L, await LedgerSql.ScalarAsync(path, "PRAGMA user_version;"));
        Assert.Equal(1L, await LedgerSql.ScalarAsync(path, "SELECT COUNT(*) FROM spans;"));
    }

    [Fact]
    public async Task MT3_AFormat6Ledger_UpgradesAndGainsTheTable()
    {
        using TempDirectory temp = new();
        string path = Path.Combine(temp.Path, "ledger.db");
        SqliteLedgerStore store = new(path);
        await store.InitializeAsync();
        await store.RecordAsync([ImportEntries.Live("c-1", null)]);
        SqliteConnection.ClearAllPools();
        await LedgerSql.ExecuteAsync(
            path,
            """
            DROP TABLE mcp_tool_names;
            DROP INDEX ix_spans_conversation_live;
            DROP INDEX ix_span_links_import_span;
            ALTER TABLE spans DROP COLUMN list_cost_usd_pico;
            ALTER TABLE spans DROP COLUMN charge_source;
            ALTER TABLE import_sessions DROP COLUMN import_revision;
            PRAGMA user_version = 6;
            """);
        SqliteConnection.ClearAllPools();

        await new SqliteLedgerStore(path).InitializeAsync();

        Assert.Equal(7L, await LedgerSql.ScalarAsync(path, "PRAGMA user_version;"));
        Assert.Equal(1L, await TableCount(path));
        Assert.Equal(1L, await LedgerSql.ScalarAsync(path, "SELECT COUNT(*) FROM spans;"));
        Assert.Equal(
            "rate_card",
            await LedgerSql.ScalarAsync(path, "SELECT charge_source FROM spans;"));
    }

    [Fact]
    public async Task MT4_ConcurrentOpens_CreateOneTable()
    {
        using TempDirectory temp = new();
        string path = Path.Combine(temp.Path, "ledger.db");

        await Task.WhenAll(
            new SqliteLedgerStore(path).InitializeAsync(),
            new SqliteLedgerStore(path).InitializeAsync());
        await new SqliteLedgerStore(path).InitializeAsync();

        Assert.Equal(1L, await TableCount(path));
        Assert.Equal(7L, await LedgerSql.ScalarAsync(path, "PRAGMA user_version;"));
    }

    [Fact]
    public async Task MT5_LearnedNames_KeepTheOrdinallySmallestName()
    {
        using TempDirectory temp = new();
        string path = Path.Combine(temp.Path, "ledger.db");
        SqliteLedgerStore store = new(path);
        await store.InitializeAsync();
        LearnedToolName[] a =
        [
            new(ExecJs, "computer-use-exec_js"),
            new(LedgerHash, "ledger-get_branch_ledger"),
        ];
        string[] initial =
        [
            ExecJs + "|computer-use-exec_js",
            LedgerHash + "|ledger-get_branch_ledger",
        ];

        await store.RecordMcpToolNamesAsync(a);
        Assert.Equal(initial, await Rows(path));
        await store.RecordMcpToolNamesAsync([new(ExecJs, "zzz")]);
        Assert.Equal(initial, await Rows(path));
        await store.RecordMcpToolNamesAsync([new(ExecJs, "computer-use/exec_js")]);
        Assert.Equal(initial, await Rows(path));
        await store.RecordMcpToolNamesAsync(a);
        Assert.Equal(initial, await Rows(path));
        await store.RecordMcpToolNamesAsync([new(ExecJs, "computer-use-exec")]);

        Assert.Equal(
            [
                ExecJs + "|computer-use-exec",
                LedgerHash + "|ledger-get_branch_ledger",
            ],
            await Rows(path));
    }

    public static TheoryData<string, string> InvalidNameCases => new()
    {
        { "powershell", "x" },
        { ExecJs.ToUpperInvariant(), "x" },
        { ExecJs, "" },
        { ExecJs, new string('x', 257) },
    };

    [Theory]
    [MemberData(nameof(InvalidNameCases))]
    public async Task MT6_AnInvalidName_WritesNothing(string hashedName, string toolName)
    {
        using TempDirectory temp = new();
        string path = Path.Combine(temp.Path, "ledger.db");
        SqliteLedgerStore store = new(path);
        await store.InitializeAsync();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            store.RecordMcpToolNamesAsync(
                [
                    new(LedgerHash, "ledger-get_branch_ledger"),
                    new(hashedName, toolName),
                ]));

        Assert.Equal(
            0L,
            await LedgerSql.ScalarAsync(path, "SELECT COUNT(*) FROM mcp_tool_names;"));
    }

    [Fact]
    public async Task MT7_TheExportSnapshot_ReadsTheNames()
    {
        using TempDirectory temp = new();
        string path = Path.Combine(temp.Path, "ledger.db");
        SqliteLedgerStore store = new(path);
        await store.InitializeAsync();
        await using (LedgerExportSnapshot snapshot = await store.OpenExportSnapshotAsync())
        {
            Assert.Empty(await snapshot.GetMcpToolNamesAsync());
        }

        await store.RecordMcpToolNamesAsync(
            [
                new(ExecJs, "computer-use-exec_js"),
                new(LedgerHash, "ledger-get_branch_ledger"),
            ]);
        await using (LedgerExportSnapshot snapshot = await store.OpenExportSnapshotAsync())
        {
            IReadOnlyDictionary<string, string> names = await snapshot.GetMcpToolNamesAsync();

            Assert.Equal(2, names.Count);
            Assert.Equal("computer-use-exec_js", names[ExecJs]);
            Assert.Equal("ledger-get_branch_ledger", names[LedgerHash]);
        }
    }

    [Fact]
    public async Task MT8_ADryRunImport_NeverCreatesTheTable()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = new(temp.Path);
        new CopilotSessionLogs("s-a")
            .Usage("u-1", R1)
            .Write(harness.Context.CopilotHome);
        await new SqliteLedgerStore(
                harness.Context.DatabasePath,
                harness.TimeProvider)
            .InitializeAsync();
        SqliteConnection.ClearAllPools();
        await LedgerSql.ExecuteAsync(
            harness.Context.DatabasePath,
            "DROP TABLE mcp_tool_names;");
        SqliteConnection.ClearAllPools();

        CliRun dryRun = await harness.RunAsync("import", "--all", "--dry-run", "--json");

        Assert.Equal(0, dryRun.ExitCode);
        Assert.Equal(0L, await TableCount(harness.Context.DatabasePath));

        CliRun import = await harness.RunAsync("import", "--all", "--json");

        Assert.Equal(0, import.ExitCode);
        Assert.Equal(1L, await TableCount(harness.Context.DatabasePath));
    }

    private static Task<IReadOnlyList<string>> Rows(string path) =>
        LedgerSql.RowsAsync(
            path,
            "SELECT hashed_name || '|' || tool_name FROM mcp_tool_names ORDER BY hashed_name;");

    private static async Task<long> TableCount(string path) =>
        (long)(await LedgerSql.ScalarAsync(
            path,
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'mcp_tool_names';"))!;
}
