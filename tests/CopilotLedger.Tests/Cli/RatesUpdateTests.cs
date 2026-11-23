using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using CopilotLedger.Cli;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Core.Telemetry;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Cli;

public sealed class RatesUpdateTests
{
    [Fact]
    public async Task RU1_NewerPublishedRatesAreSavedAndUsed()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = new(temp.Path);
        string published = PublishedRates.WithVersion("2026-12-01");
        FakeHttpHandler handler = new(_ => FakeHttpHandler.Bytes(PublishedRates.Utf8(published)));

        CliRun run = await harness.RunWithHttpAsync(handler, "rates", "update");

        Assert.Equal(0, run.ExitCode);
        Assert.Equal("Updated rates from 2026-11-23 to 2026-12-01.\n", run.Out);
        Assert.Equal(string.Empty, run.Error);
        Assert.Equal(
            [new HttpCall("GET", PublishedRates.Url, $"gh-ledger/{GhLedgerCli.Version}")],
            handler.Calls);
        string downloaded = Path.Combine(temp.Path, "data", "rates.downloaded.json");
        Assert.Equal(published, await FileRetry.ReadAllTextAsync(downloaded));
        Assert.Equal([downloaded], Directory.GetFiles(Path.Combine(temp.Path, "data")));

        CliRun validate = await harness.RunAsync("rates", "validate");
        Assert.Equal(0, validate.ExitCode);
        Assert.Equal(
            $"No rates overlay at {Path.Combine(temp.Path, "data", "rates.json")}; using the downloaded rates (2026-12-01).\n",
            validate.Out);
        CliRun status = await harness.RunAsync("status");
        Assert.Equal(0, status.ExitCode);
        Assert.Equal("Rates:   2026-12-01 (downloaded)", status.Out.Split('\n')[3]);
        CliRun json = await harness.RunAsync("status", "--json");
        Assert.Equal(0, json.ExitCode);
        using JsonDocument document = JsonDocument.Parse(json.Out);
        Assert.Equal("2026-12-01", document.RootElement.GetProperty("ratesVersion").GetString());
        Assert.Equal("downloaded", document.RootElement.GetProperty("ratesSource").GetString());
    }

    public static TheoryData<string, string> NotNewer => new()
    {
        { "2026-11-23", "Rates are up to date (2026-11-23).\n" },
        { "2026-10-01", "The published rates (2026-10-01) are older than the rates in use (2026-11-23); nothing changed.\n" },
    };

    [Theory]
    [MemberData(nameof(NotNewer))]
    public async Task RU2_RatesThatAreNotNewerAreNotSaved(string version, string output)
    {
        using TempDirectory temp = new();
        await using CliHarness harness = new(temp.Path);
        FakeHttpHandler handler = new(_ => FakeHttpHandler.Bytes(
            PublishedRates.Utf8(PublishedRates.WithVersion(version))));

        CliRun run = await harness.RunWithHttpAsync(handler, "rates", "update");

        Assert.Equal(0, run.ExitCode);
        Assert.Equal(output, run.Out);
        Assert.Equal(string.Empty, run.Error);
        Assert.False(File.Exists(Path.Combine(temp.Path, "data", "rates.downloaded.json")));
    }

    public static TheoryData<string, string> Failures => new()
    {
        { "offline", $"Could not download rates from {PublishedRates.Url}: No such host is known. (github.com:443)\n" },
        { "404", $"Could not download rates from {PublishedRates.LegacyUrl}: HTTP 404.\n" },
        { "timeout", $"Could not download rates from {PublishedRates.Url}: no response within 30 seconds.\n" },
        { "not-utf8", "The downloaded rates are invalid: the file isn't UTF-8.\n" },
        { "not-json", "The downloaded rates are invalid: line 1, column 2: invalid JSON\n" },
        { "incomplete", "The downloaded rates are invalid: source: is required\n" },
        { "not-a-date", "The downloaded rates are invalid: ratesVersion 2026-12 isn't a yyyy-MM-dd date.\n" },
    };

    [Theory]
    [MemberData(nameof(Failures))]
    public async Task RU3_FailuresReportExactErrorsAndSaveNothing(string failure, string error)
    {
        using TempDirectory temp = new();
        await using CliHarness harness = new(temp.Path);
        FakeHttpHandler handler = new(_ => failure switch
        {
            "offline" => throw new HttpRequestException("No such host is known. (github.com:443)"),
            "404" => new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new ByteArrayContent(PublishedRates.Utf8("{\"error\":\"Not Found\"}")),
            },
            "timeout" => throw new TaskCanceledException(),
            "not-utf8" => FakeHttpHandler.Bytes([0xC3, 0x28]),
            "not-json" => FakeHttpHandler.Bytes(PublishedRates.Utf8("{")),
            "incomplete" => FakeHttpHandler.Bytes(PublishedRates.Utf8("{ \"schemaVersion\": 1, \"ratesVersion\": \"2026-12-01\" }")),
            "not-a-date" => FakeHttpHandler.Bytes(PublishedRates.Utf8(PublishedRates.WithVersion("2026-12"))),
            _ => throw new InvalidOperationException(failure),
        });

        CliRun run = await harness.RunWithHttpAsync(handler, "rates", "update");

        Assert.Equal(1, run.ExitCode);
        Assert.Equal(string.Empty, run.Out);
        Assert.Equal(error, run.Error);
        Assert.False(File.Exists(Path.Combine(temp.Path, "data", "rates.downloaded.json")));
    }

    [Fact]
    public async Task RU4_OverlayConflictKeepsTheCurrentRates()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = new(temp.Path);
        string overlay = Path.Combine(temp.Path, "data", "rates.json");
        Directory.CreateDirectory(Path.GetDirectoryName(overlay)!);
        await File.WriteAllTextAsync(overlay, PublishedRates.GptPreviewOverlay);
        await FileRetry.ReadAllTextAsync(overlay);
        Assert.Equal(0, (await harness.RunAsync("rates", "validate")).ExitCode);
        FakeHttpHandler handler = new(_ => FakeHttpHandler.Bytes(
            PublishedRates.Utf8(PublishedRates.WithGptNew("2026-12-01"))));

        CliRun run = await harness.RunWithHttpAsync(handler, "rates", "update");

        Assert.Equal(1, run.ExitCode);
        Assert.Equal(string.Empty, run.Out);
        Assert.Equal(
            $"The downloaded rates (2026-12-01) don't work with your rates overlay: {overlay}: githubAiCredits.models[0].id: duplicate model identity in the merged rates: gpt-preview (built-in model gpt-new)\n",
            run.Error);
        Assert.False(File.Exists(Path.Combine(temp.Path, "data", "rates.downloaded.json")));
    }

    [Fact]
    public async Task RU5_LaterRatesReplaceEarlierDownloads()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = new(temp.Path);
        string downloaded = Path.Combine(temp.Path, "data", "rates.downloaded.json");
        Directory.CreateDirectory(Path.GetDirectoryName(downloaded)!);
        await File.WriteAllTextAsync(downloaded, PublishedRates.WithVersion("2026-12-01"));
        await FileRetry.ReadAllTextAsync(downloaded);

        CliRun same = await harness.RunWithHttpAsync(
            new FakeHttpHandler(_ => FakeHttpHandler.Bytes(PublishedRates.Utf8(PublishedRates.WithVersion("2026-12-01")))),
            "rates",
            "update");
        CliRun later = await harness.RunWithHttpAsync(
            new FakeHttpHandler(_ => FakeHttpHandler.Bytes(PublishedRates.Utf8(PublishedRates.WithVersion("2026-12-15")))),
            "rates",
            "update");

        Assert.Equal(0, same.ExitCode);
        Assert.Equal("Rates are up to date (2026-12-01).\n", same.Out);
        Assert.Equal(0, later.ExitCode);
        Assert.Equal("Updated rates from 2026-12-01 to 2026-12-15.\n", later.Out);
        Assert.Equal(PublishedRates.WithVersion("2026-12-15"), await FileRetry.ReadAllTextAsync(downloaded));
    }

    [Fact]
    public async Task RU6_UnusableDownloadsAreIgnoredWithAWarning()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = new(temp.Path);
        string downloaded = Path.Combine(temp.Path, "data", "rates.downloaded.json");
        Directory.CreateDirectory(Path.GetDirectoryName(downloaded)!);
        await File.WriteAllTextAsync(downloaded, "not json");
        await FileRetry.ReadAllTextAsync(downloaded);
        string warning = $"Warning: ignored {downloaded}: line 1, column 2: invalid JSON\n";

        CliRun validate = await harness.RunAsync("rates", "validate");
        CliRun status = await harness.RunAsync("status", "--json");
        CliRun update = await harness.RunWithHttpAsync(
            new FakeHttpHandler(_ => FakeHttpHandler.Bytes(PublishedRates.Utf8(PublishedRates.WithVersion("2026-12-01")))),
            "rates",
            "update");

        Assert.Equal(0, validate.ExitCode);
        Assert.Equal(
            warning + $"No rates overlay at {Path.Combine(temp.Path, "data", "rates.json")}; using the built-in rates (2026-11-23).\n",
            validate.Out);
        Assert.Equal(0, status.ExitCode);
        using JsonDocument document = JsonDocument.Parse(status.Out);
        Assert.Equal("2026-11-23", document.RootElement.GetProperty("ratesVersion").GetString());
        Assert.Equal("built-in", document.RootElement.GetProperty("ratesSource").GetString());
        Assert.Equal(
            [warning[9..^1]],
            document.RootElement.GetProperty("warnings").EnumerateArray().Select(item => item.GetString()));
        Assert.Equal(0, update.ExitCode);
        Assert.Equal(warning + "Updated rates from 2026-11-23 to 2026-12-01.\n", update.Out);
    }

    [Fact]
    public async Task RU7_DownloadsNotLaterThanTheBuiltInRatesAreIgnoredSilently()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = new(temp.Path);
        string downloaded = Path.Combine(temp.Path, "data", "rates.downloaded.json");
        Directory.CreateDirectory(Path.GetDirectoryName(downloaded)!);
        await File.WriteAllTextAsync(downloaded, PublishedRates.WithVersion("2026-11-01"));
        await FileRetry.ReadAllTextAsync(downloaded);

        CliRun validate = await harness.RunAsync("rates", "validate");

        Assert.Equal(0, validate.ExitCode);
        Assert.Equal(
            $"No rates overlay at {Path.Combine(temp.Path, "data", "rates.json")}; using the built-in rates (2026-11-23).\n",
            validate.Out);
    }

    [Fact]
    public async Task RU8_RunningDaemonGetsARestartHintAndPricesWithDownloadedRates()
    {
        using TempDirectory temp = new();
        string repo = GitFixture.Create(temp.Path);
        await using CliHarness harness = new(temp.Path);
        string downloaded = Path.Combine(temp.Path, "data", "rates.downloaded.json");
        Directory.CreateDirectory(Path.GetDirectoryName(downloaded)!);
        await File.WriteAllTextAsync(downloaded, PublishedRates.DoubledOpus("2026-12-01"));
        await FileRetry.ReadAllTextAsync(downloaded);
        await harness.StartInProcessDaemonAsync();

        CliRun update = await harness.RunWithHttpAsync(
            new FakeHttpHandler(_ => FakeHttpHandler.Bytes(PublishedRates.Utf8(PublishedRates.WithVersion("2026-12-15")))),
            "rates",
            "update");
        using HttpClient client = harness.CreateDaemonClient();
        using StringContent content = new(
            BreakerTraces.TraceVJson(repo),
            new MediaTypeHeaderValue("application/json"));
        using HttpResponseMessage response = await client.PostAsync("/v1/traces", content);
        CliRun branch = await harness.RunInAsync(repo, "branch", "--since", "all", "--json");

        Assert.Equal(0, update.ExitCode);
        Assert.Equal(
            "Updated rates from 2026-12-01 to 2026-12-15.\n" +
            "Restart the daemon to price new spans with them: gh-ledger daemon stop, then gh-ledger daemon start.\n",
            update.Out);
        Assert.Equal(200, (int)response.StatusCode);
        Assert.Equal(0, branch.ExitCode);
        using JsonDocument document = JsonDocument.Parse(branch.Out);
        JsonElement row = Assert.Single(document.RootElement.GetProperty("rows").EnumerateArray());
        Assert.Equal(3, row.GetProperty("spans").GetInt64());
        Assert.Equal(360m, row.GetProperty("aiCredits").GetDecimal());
        Assert.Equal(3.6m, row.GetProperty("aiCreditsUsd").GetDecimal());
    }

    [Fact]
    public async Task RU9_DaemonLogsIgnoredDownloadsAndStillStarts()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = new(
            temp.Path,
            timeProvider: new ManualTimeProvider(
                DateTimeOffset.Parse("2026-11-21T15:00:00Z", CultureInfo.InvariantCulture)));
        string downloaded = Path.Combine(temp.Path, "data", "rates.downloaded.json");
        Directory.CreateDirectory(Path.GetDirectoryName(downloaded)!);
        await File.WriteAllTextAsync(downloaded, "not json");
        await FileRetry.ReadAllTextAsync(downloaded);

        await harness.StartInProcessDaemonAsync();
        CliRun stop = await harness.RunAsync("daemon", "stop");
        Assert.Equal(0, (await harness.InProcessDaemon!).ExitCode);

        Assert.Equal(0, stop.ExitCode);
        Assert.StartsWith(
            $"2026-11-21T15:00:00.0000000+00:00 Warning: ignored {downloaded}: line 1, column 2: invalid JSON\n",
            harness.DaemonOutput);
    }

    [Fact]
    public void RU10_BuiltInRatesVersionIsADate()
    {
        Assert.True(DateOnly.TryParseExact(
            JsonRateCardCalculator.LoadEmbedded().RatesVersion,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out _));
    }

    public static TheoryData<string, int, string, string> LegacyReleases => new()
    {
        { "2026-11-20", 0, "The published rates (2026-11-20) are older than the rates in use (2026-11-25); nothing changed.\n", "" },
        { "2026-12-01", 1, "", "The published rates (2026-12-01) are schemaVersion 1, without API-equivalent prices; this gh-ledger needs schemaVersion 2. Nothing changed.\n" },
    };

    // A release before rates.v2.json attaches only schemaVersion 1 rates.json, which may not replace the rates in use.
    [Theory]
    [MemberData(nameof(LegacyReleases))]
    public async Task RU11_AReleaseWithoutVersion2RatesFallsBackToItsRatesJson(
        string version,
        int exitCode,
        string output,
        string error)
    {
        using TempDirectory temp = new();
        await using CliHarness harness = new(temp.Path);
        string downloaded = Path.Combine(temp.Path, "data", "rates.downloaded.json");
        Directory.CreateDirectory(Path.GetDirectoryName(downloaded)!);
        await File.WriteAllTextAsync(downloaded, PublishedRates.WithVersion("2026-11-25"));
        byte[] before = await FileRetry.ReadAllBytesAsync(downloaded);
        FakeHttpHandler handler = new(request => request.RequestUri!.AbsoluteUri == PublishedRates.Url
            ? new HttpResponseMessage(HttpStatusCode.NotFound)
            : FakeHttpHandler.Bytes(PublishedRates.Utf8(PublishedRates.SchemaVersion1(version))));

        CliRun run = await harness.RunWithHttpAsync(handler, "rates", "update");

        Assert.Equal(exitCode, run.ExitCode);
        Assert.Equal(output, run.Out);
        Assert.Equal(error, run.Error);
        Assert.Equal(
            [
                new HttpCall("GET", PublishedRates.Url, $"gh-ledger/{GhLedgerCli.Version}"),
                new HttpCall("GET", PublishedRates.LegacyUrl, $"gh-ledger/{GhLedgerCli.Version}"),
            ],
            handler.Calls);
        Assert.Equal(before, await FileRetry.ReadAllBytesAsync(downloaded));
    }

    // gh-ledger 0.4.1 rejects schemaVersion 2 the same way: "The downloaded rates are invalid: schemaVersion: must be 1".
    [Fact]
    public async Task RU12_RatesWithAnUnsupportedSchemaVersionFailClosed()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = new(temp.Path);
        string downloaded = Path.Combine(temp.Path, "data", "rates.downloaded.json");
        Directory.CreateDirectory(Path.GetDirectoryName(downloaded)!);
        await File.WriteAllTextAsync(downloaded, PublishedRates.WithVersion("2026-11-25"));
        byte[] before = await FileRetry.ReadAllBytesAsync(downloaded);
        string future = PublishedRates.WithVersion("2026-12-01").Replace(
            "\"schemaVersion\": 2,",
            "\"schemaVersion\": 3,",
            StringComparison.Ordinal);

        CliRun run = await harness.RunWithHttpAsync(
            new FakeHttpHandler(_ => FakeHttpHandler.Bytes(PublishedRates.Utf8(future))),
            "rates",
            "update");

        Assert.Equal(1, run.ExitCode);
        Assert.Equal(string.Empty, run.Out);
        Assert.Equal("The downloaded rates are invalid: schemaVersion: must be 1 or 2\n", run.Error);
        Assert.Equal(before, await FileRetry.ReadAllBytesAsync(downloaded));
    }

    [Fact]
    public async Task RU13_LaterSchemaVersion1DownloadsAreIgnoredWithAWarning()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = new(temp.Path);
        string downloaded = Path.Combine(temp.Path, "data", "rates.downloaded.json");
        Directory.CreateDirectory(Path.GetDirectoryName(downloaded)!);
        await File.WriteAllTextAsync(downloaded, PublishedRates.SchemaVersion1("2026-12-01"));
        await FileRetry.ReadAllTextAsync(downloaded);
        string warning =
            $"Warning: ignored {downloaded}: its schemaVersion 1 rates have no API-equivalent prices; run gh-ledger rates update to replace them.\n";

        CliRun validate = await harness.RunAsync("rates", "validate");
        CliRun status = await harness.RunAsync("status", "--json");
        CliRun update = await harness.RunWithHttpAsync(
            new FakeHttpHandler(_ => FakeHttpHandler.Bytes(PublishedRates.Utf8(PublishedRates.WithVersion("2026-12-01")))),
            "rates",
            "update");

        Assert.Equal(0, validate.ExitCode);
        Assert.Equal(
            warning + $"No rates overlay at {Path.Combine(temp.Path, "data", "rates.json")}; using the built-in rates (2026-11-23).\n",
            validate.Out);
        using JsonDocument document = JsonDocument.Parse(status.Out);
        Assert.Equal("2026-11-23", document.RootElement.GetProperty("ratesVersion").GetString());
        Assert.Equal("built-in", document.RootElement.GetProperty("ratesSource").GetString());
        Assert.Equal(
            [warning[9..^1]],
            document.RootElement.GetProperty("warnings").EnumerateArray().Select(item => item.GetString()));
        Assert.Equal(0, update.ExitCode);
        Assert.Equal(warning + "Updated rates from 2026-11-23 to 2026-12-01.\n", update.Out);
        Assert.Equal(PublishedRates.WithVersion("2026-12-01"), await FileRetry.ReadAllTextAsync(downloaded));
    }

    // The Release workflow publishes these rates as rates.json for gh-ledger 0.4.x and earlier.
    [Fact]
    public void RU14_SchemaVersion1RatesKeepEveryCreditPrice()
    {
        string legacy = PublishedRates.SchemaVersion1("2026-11-23");
        using JsonDocument document = JsonDocument.Parse(legacy);
        JsonRateCardCalculator v1 = JsonRateCardCalculator.Parse(legacy);
        JsonRateCardCalculator v2 = JsonRateCardCalculator.LoadEmbedded();
        string[] models = JsonNode.Parse(legacy)!["githubAiCredits"]!["models"]!.AsArray()
            .Select(model => model!["id"]!.GetValue<string>())
            .ToArray();

        Assert.Equal(
            ["schemaVersion", "ratesVersion", "source", "notes", "githubAiCredits", "foundryByok", "localOffload"],
            document.RootElement.EnumerateObject().Select(property => property.Name));
        Assert.Equal(1, v1.SchemaVersion);
        Assert.Equal("2026-11-23", v1.RatesVersion);
        Assert.Equal(34, models.Length);
        foreach (string model in models)
        {
            NormalizedGenAiSpan span = TestSpans.Chat(
                requestModel: model,
                inputTokens: 300_000,
                cacheReadInputTokens: 100_000,
                cacheWriteInputTokens: 10_000,
                outputTokens: 5_000);
            CreditCalculationResult legacyResult = v1.Calculate(span);
            CreditCalculationResult result = v2.Calculate(span);
            Assert.Equal(result.UsdCost, legacyResult.UsdCost);
            Assert.Equal(result.AiCredits, legacyResult.AiCredits);
            Assert.Equal(ApiEquivalentStatus.Unpriced, legacyResult.ApiEquivalent.Status);
        }
    }
}
