using CopilotLedger.Storage;
using CopilotLedger.Tests.TestSupport;
using Microsoft.Data.Sqlite;

namespace CopilotLedger.Tests.Cli;

public sealed class RatesBackfillTests
{
    private const string Legacy =
        "UPDATE spans SET (api_equivalent_usd_pico, api_equivalent_status, api_rate_card_model_id, api_rates_version, api_rate_card_sha256) = (NULL, NULL, NULL, NULL, NULL);";

    private const string Results =
        "SELECT span_id, api_equivalent_usd_pico, api_equivalent_status, api_rate_card_model_id, api_rates_version FROM spans ORDER BY span_id;";

    [Fact]
    public async Task RB1_DryRunCountsTheLegacySpansAndWritesNothing()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = await LegacyHarnessAsync(temp.Path);
        string[] before = await RowsAsync(temp.Path, "SELECT * FROM spans ORDER BY id;");

        CliRun run = await harness.RunAsync("rates", "backfill-api-equivalent", "--dry-run");

        Assert.Equal(0, run.ExitCode);
        Assert.Equal(
            """
            5 spans have no API-equivalent result. With rates 2026-11-23:
              Priced:          2 ($3.16)
              Unpriced:        3
              Not applicable:  0
            Dry run: nothing was written.

            """.ReplaceLineEndings("\n"),
            run.Out);
        Assert.Equal(string.Empty, run.Error);
        Assert.Equal(before, await RowsAsync(temp.Path, "SELECT * FROM spans ORDER BY id;"));
    }

    [Fact]
    public async Task RB2_BackfillRecordsEachResultOnce()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = await LegacyHarnessAsync(temp.Path);

        CliRun run = await harness.RunAsync("rates", "backfill-api-equivalent");
        CliRun again = await harness.RunAsync("rates", "backfill-api-equivalent");

        Assert.Equal(0, run.ExitCode);
        Assert.Equal(
            """
            5 spans have no API-equivalent result. With rates 2026-11-23:
              Priced:          2 ($3.16)
              Unpriced:        3
              Not applicable:  0
            Recorded 5 results. Billed AI credits, BYOK USD, and local savings didn't change.

            """.ReplaceLineEndings("\n"),
            run.Out);
        Assert.Equal(
            [
                "1111111111111111|2860000000000|priced|claude-opus-5.5|2026-11-23",
                "2222222222222221|null|unpriced|null|2026-11-23",
                "2222222222222222|null|unpriced|null|2026-11-23",
                "3333333333333333|300000000000|priced|gpt-6.1-sol|2026-11-23",
                "4444444444444444|null|unpriced|null|2026-11-23",
            ],
            await RowsAsync(temp.Path, Results));
        Assert.Equal(0, again.ExitCode);
        Assert.Equal("Every span has an API-equivalent result; nothing to backfill.\n", again.Out);
    }

    // The backfill prices with the rates the daemon would use, so an invalid overlay stops it before it writes.
    [Fact]
    public async Task RB3_AnInvalidOverlayStopsTheBackfill()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = await LegacyHarnessAsync(temp.Path);
        string overlay = Path.Combine(temp.Path, "data", "rates.json");
        await File.WriteAllTextAsync(overlay, """{ "schemaVersion": 3 }""");

        CliRun run = await harness.RunAsync("rates", "backfill-api-equivalent");

        Assert.Equal(1, run.ExitCode);
        Assert.Equal(string.Empty, run.Out);
        Assert.Equal($"Invalid rates overlay: {overlay}: schemaVersion: must be 1 or 2\n", run.Error);
        Assert.Equal(
            5L,
            long.Parse(
                (await RowsAsync(temp.Path, "SELECT COUNT(*) FROM spans WHERE api_equivalent_status IS NULL;"))[0],
                System.Globalization.CultureInfo.InvariantCulture));
    }

    // The seeded ledger of ReportCommandTests, as recorded before schema version 5.
    private static async Task<CliHarness> LegacyHarnessAsync(string root)
    {
        CliHarness harness = new(root);
        SqliteLedgerStore store = new(Path.Combine(root, "data", "ledger.db"), harness.TimeProvider);
        await store.InitializeAsync();
        await LedgerSeeds.SeedChainsAsync(store, Path.Combine(root, "repo"), Path.Combine(root, "api"));
        await RowsAsync(root, Legacy);
        return harness;
    }

    // Each row as its values, NULL as "null", separated by '|'.
    private static async Task<string[]> RowsAsync(string root, string sql)
    {
        await using SqliteConnection connection = new($"Data Source={Path.Combine(root, "data", "ledger.db")}");
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        await using SqliteDataReader reader = await command.ExecuteReaderAsync();
        List<string> rows = [];
        while (await reader.ReadAsync())
        {
            rows.Add(string.Join(
                '|',
                Enumerable.Range(0, reader.FieldCount)
                    .Select(index => reader.IsDBNull(index)
                        ? "null"
                        : Convert.ToString(reader.GetValue(index), System.Globalization.CultureInfo.InvariantCulture))));
        }

        return [.. rows];
    }
}
