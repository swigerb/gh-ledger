using System.CommandLine;
using System.CommandLine.Parsing;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using CopilotLedger.Cli.Daemon;
using CopilotLedger.Cli.Install;
using CopilotLedger.Cli.Rates;
using CopilotLedger.Core.Git;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Storage;

namespace CopilotLedger.Cli.Reporting;

internal static class ReportCommands
{
    private static readonly JsonWriterOptions JsonOptions = new()
    {
        Indented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly TableColumn[] MoneyColumns =
    [
        new("Spans", true),
        new("Unpriced", true),
        new("AI credits", true),
        new("GitHub USD", true),
        new("BYOK USD", true),
        new("Local saved", true),
    ];

    public static IEnumerable<Command> Create(CliContext context)
    {
        yield return CreateStatus(context);
        yield return CreateBranch(context);
        yield return CreateAgents(context);
        yield return CreateTopPrompts(context);
    }

    private static Command CreateStatus(CliContext context)
    {
        Command command = new("status", "Show daemon, configuration, and recent spend.");
        Option<bool> json = JsonOption();
        command.Add(json);
        command.SetAction(
            (ParseResult result, CancellationToken cancellationToken) =>
                StatusAsync(context, result.GetValue(json), cancellationToken));
        return command;
    }

    private static Command CreateBranch(CliContext context)
    {
        Command command = new("branch", "Report spend by branch.");
        Option<string?> repository = RepositoryOption();
        Option<bool> allRepositories = new("--all-repos")
        {
            Description = "Report every repository.",
        };
        Option<string> since = SinceOption(context);
        Option<bool> json = JsonOption();
        command.Add(repository);
        command.Add(allRepositories);
        command.Add(since);
        command.Add(json);
        command.Validators.Add(result =>
        {
            if (result.GetValue(repository) is not null && result.GetValue(allRepositories))
            {
                result.AddError("Use either --repo or --all-repos, not both.");
            }
        });
        command.SetAction(
            (ParseResult result, CancellationToken cancellationToken) =>
                BranchAsync(
                    context,
                    result.GetValue(repository),
                    result.GetValue(allRepositories),
                    ParsePeriod(context, result.GetValue(since)),
                    result.GetValue(json),
                    cancellationToken));
        return command;
    }

    private static Command CreateAgents(CliContext context)
    {
        Command command = new("agents", "Report spend by agent.");
        Option<string?> repository = RepositoryOption();
        Option<string> since = SinceOption(context);
        Option<bool> json = JsonOption();
        command.Add(repository);
        command.Add(since);
        command.Add(json);
        command.SetAction(
            (ParseResult result, CancellationToken cancellationToken) =>
                RollupAsync(
                    context,
                    LedgerGrouping.Agent,
                    result.GetValue(repository),
                    ParsePeriod(context, result.GetValue(since)),
                    result.GetValue(json),
                    cancellationToken));
        return command;
    }

    private static Command CreateTopPrompts(CliContext context)
    {
        Command command = new("top-prompts", "Report the highest-cost prompt traces.");
        Option<string?> repository = RepositoryOption();
        Option<string> since = SinceOption(context);
        Option<int> limit = new("--limit")
        {
            Description = "Maximum number of prompts.",
            DefaultValueFactory = _ => 10,
        };
        limit.Validators.Add(result =>
        {
            int value = result.GetValueOrDefault<int>();
            if (value is < 1 or > 100)
            {
                result.AddError("--limit must be from 1 to 100.");
            }
        });
        Option<bool> json = JsonOption();
        command.Add(repository);
        command.Add(since);
        command.Add(limit);
        command.Add(json);
        command.SetAction(
            (ParseResult result, CancellationToken cancellationToken) =>
                TopPromptsAsync(
                    context,
                    result.GetValue(repository),
                    ParsePeriod(context, result.GetValue(since)),
                    result.GetValue(limit),
                    result.GetValue(json),
                    cancellationToken));
        return command;
    }

    private static async Task<int> StatusAsync(
        CliContext context,
        bool json,
        CancellationToken cancellationToken)
    {
        bool running = DaemonFiles.IsHeld(context);
        DaemonState? state = running ? DaemonFiles.TryReadState(context) : null;
        RatesBase rates = RateCardFiles.LoadBase(context);
        string? ratesVersion;
        string? ratesError = null;
        try
        {
            ratesVersion = RateCardFiles.Load(rates, RateCardFiles.OverlayPath(context)).RatesVersion;
        }
        catch (InvalidDataException ex)
        {
            ratesVersion = null;
            ratesError = ex.Message;
        }

        Func<string, string?> lookup = name =>
            context.Platform.GetUserEnvironmentVariable(name) ??
            context.Platform.GetEnvironmentVariable(name);
        List<string> warnings = [.. CopilotOtelEnvironment.GetWarnings(lookup)];
        bool enabled = IsOtelEnabled(lookup);
        bool extension = GhExtension.IsInstalled(context.Platform);
        string command = GhExtension.CommandName(context.Platform);
        InstallManifest? manifest = await InstallCommands.TryReadManifestAsync(
            InstallCommands.ManifestPath(context),
            cancellationToken);
        if (manifest is not null
            && context.Platform.ProcessPath is string executable
            && !string.Equals(
                manifest.ExecutablePath,
                executable,
                context.Platform.IsWindows ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            warnings.Add(
                $"CopilotLedger was installed from {manifest.ExecutablePath}; run {command} install to use this copy instead.");
        }

        if (rates.Warning is not null)
        {
            warnings.Add(rates.Warning);
        }

        SqliteLedgerStore store = new(context.DatabasePath, context.TimeProvider);
        await store.InitializeAsync(cancellationToken);
        (string Label, Period Period)[] periods =
        [
            ("Today", RequirePeriod(context, "today")),
            ("Last 7 days", RequirePeriod(context, "7d")),
            ("Last 30 days", RequirePeriod(context, "30d")),
        ];
        List<(string Label, Period Period, LedgerRollupRow Total)> totals = [];
        foreach ((string label, Period period) in periods)
        {
            IReadOnlyList<LedgerRollupRow> rows = await store.GetRollupAsync(
                LedgerGrouping.Repository,
                new LedgerFilter { From = period.From },
                cancellationToken);
            totals.Add((label, period, Sum(rows)));
        }

        if (json)
        {
            WriteStatusJson(
                context,
                running,
                state,
                ratesVersion,
                ratesError,
                enabled,
                warnings,
                totals,
                extension,
                rates.DownloadedJson is not null);
            return 0;
        }

        context.Out.WriteLine(extension
            ? $"gh-ledger {GhLedgerCli.Version} (GitHub CLI extension)"
            : $"gh-ledger {GhLedgerCli.Version}");
        context.Out.WriteLine($"Daemon:  {DaemonLine(running, state, context, command)}");
        context.Out.WriteLine($"Ledger:  {context.DatabasePath}");
        context.Out.WriteLine(
            $"Rates:   {(ratesError is not null ? $"invalid overlay: {ratesError}" : rates.DownloadedJson is null ? ratesVersion : $"{ratesVersion} (downloaded)")}");
        context.Out.WriteLine(
            $"Copilot: {(enabled
                ? CopilotEnabledLine(lookup)
                : $"OTel export not enabled; run: {command} install")}");
        foreach (string warning in warnings)
        {
            context.Out.WriteLine($"Warning: {warning}");
        }

        context.Out.WriteLine();
        TableWriter.Write(
            context.Out,
            [new("Period", false), .. MoneyColumns],
            totals.Select(item =>
                (IReadOnlyList<string>)[item.Label, .. MoneyCells(item.Total)]).ToArray());
        return 0;
    }

    private static async Task<int> BranchAsync(
        CliContext context,
        string? requestedRepository,
        bool allRepositories,
        Period period,
        bool json,
        CancellationToken cancellationToken)
    {
        string? repository = requestedRepository;
        if (!allRepositories && repository is null)
        {
            GitWorkspaceContext? git =
                new FileSystemGitContextResolver().Resolve(context.WorkingDirectory);
            repository = git?.RepositorySlug ?? git?.RepositoryRoot;
        }

        return await RollupRowsAsync(
            context,
            LedgerGrouping.Branch,
            repository,
            allRepositories: allRepositories || repository is null,
            period,
            json,
            cancellationToken);
    }

    private static async Task<int> RollupAsync(
        CliContext context,
        LedgerGrouping grouping,
        string? repository,
        Period period,
        bool json,
        CancellationToken cancellationToken) =>
        await RollupRowsAsync(
            context,
            grouping,
            repository,
            allRepositories: false,
            period,
            json,
            cancellationToken);

    private static async Task<int> RollupRowsAsync(
        CliContext context,
        LedgerGrouping grouping,
        string? repository,
        bool allRepositories,
        Period period,
        bool json,
        CancellationToken cancellationToken)
    {
        SqliteLedgerStore store = new(context.DatabasePath, context.TimeProvider);
        await store.InitializeAsync(cancellationToken);
        IReadOnlyList<LedgerRollupRow> rows = await store.GetRollupAsync(
            grouping,
            new LedgerFilter
            {
                From = period.From,
                Repository = repository,
            },
            cancellationToken);
        string scope = repository ?? "all repositories";

        if (json)
        {
            WriteRollupJson(context, period, repository, grouping, rows);
            return 0;
        }

        string title = grouping switch
        {
            LedgerGrouping.Branch => $"Branches in {scope} ({period.Label})",
            LedgerGrouping.Agent => $"Agents in {scope} ({period.Label})",
            _ => throw new InvalidOperationException(),
        };
        if (rows.Count == 0)
        {
            context.Out.WriteLine($"No spend recorded in {scope} ({period.Label}).");
            return 0;
        }

        context.Out.WriteLine(title);
        context.Out.WriteLine();
        TableColumn[] leading = grouping == LedgerGrouping.Branch && allRepositories
            ? [new("Repository", false), new("Branch", false)]
            : grouping == LedgerGrouping.Branch
                ? [new("Branch", false)]
                : [new("Agent", false)];
        IReadOnlyList<IReadOnlyList<string>> tableRows = rows.Select(row =>
        {
            string[] identifiers = grouping == LedgerGrouping.Branch && allRepositories
                ? [row.Repository ?? "(none)", row.Branch ?? "(none)"]
                : grouping == LedgerGrouping.Branch
                    ? [row.Branch ?? "(none)"]
                    : [row.Agent ?? "(none)"];
            return (IReadOnlyList<string>)[.. identifiers, .. MoneyCells(row)];
        }).ToArray();
        TableWriter.Write(context.Out, [.. leading, .. MoneyColumns], tableRows);
        return 0;
    }

    private static async Task<int> TopPromptsAsync(
        CliContext context,
        string? repository,
        Period period,
        int limit,
        bool json,
        CancellationToken cancellationToken)
    {
        SqliteLedgerStore store = new(context.DatabasePath, context.TimeProvider);
        await store.InitializeAsync(cancellationToken);
        IReadOnlyList<PromptChainRow> rows = await store.GetTopPromptChainsAsync(
            new LedgerFilter
            {
                From = period.From,
                Repository = repository,
            },
            limit,
            cancellationToken);
        string scope = repository ?? "all repositories";
        if (json)
        {
            WriteTopPromptsJson(context, period, repository, rows);
            return 0;
        }

        if (rows.Count == 0)
        {
            context.Out.WriteLine($"No prompts recorded in {scope} ({period.Label}).");
            return 0;
        }

        string prompt = rows.Count == 1 ? "prompt" : "prompts";
        context.Out.WriteLine(
            $"Top {rows.Count} {prompt} by cost in {scope} ({period.Label})");
        context.Out.WriteLine();
        TableColumn[] columns =
        [
            new("#", true),
            new("Started", false),
            new("Repository", false),
            new("Agents", false),
            new("Models", false),
            new("Spans", true),
            new("AI credits", true),
            new("GitHub USD", true),
            new("BYOK USD", true),
            new("Local saved", true),
            new("Trace", false),
        ];
        IReadOnlyList<IReadOnlyList<string>> tableRows = rows.Select((row, index) =>
            (IReadOnlyList<string>)
            [
                (index + 1).ToString(CultureInfo.InvariantCulture),
                LedgerFormat.LocalTime(row.StartTime, context.TimeProvider.LocalTimeZone),
                row.Repository,
                Joined(row.Agents),
                Joined(row.Models),
                LedgerFormat.Count(row.SpanCount),
                LedgerFormat.Credits(row.AiCredits),
                LedgerFormat.Usd(row.AiCreditsUsd),
                LedgerFormat.Usd(row.FoundryByokUsd),
                LedgerFormat.Usd(row.LocalSavedUsd),
                row.TraceId,
            ]).ToArray();
        TableWriter.Write(context.Out, columns, tableRows);
        return 0;
    }

    private static void WriteStatusJson(
        CliContext context,
        bool running,
        DaemonState? state,
        string? ratesVersion,
        string? ratesError,
        bool enabled,
        IReadOnlyList<string> warnings,
        IReadOnlyList<(string Label, Period Period, LedgerRollupRow Total)> totals,
        bool extension,
        bool downloadedRates)
    {
        using MemoryStream stream = new();
        using Utf8JsonWriter writer = new(stream, JsonOptions);
        writer.WriteStartObject();
        writer.WriteString("version", GhLedgerCli.Version);
        writer.WriteBoolean("ghExtension", extension);
        writer.WritePropertyName("daemon");
        writer.WriteStartObject();
        writer.WriteBoolean("running", running);
        if (state is null)
        {
            writer.WriteNull("processId");
            writer.WriteNull("endpoint");
            writer.WriteNull("startedAt");
        }
        else
        {
            writer.WriteNumber("processId", state.ProcessId);
            writer.WriteString("endpoint", state.Endpoint);
            LedgerFormat.WriteTime(writer, "startedAt", state.StartedAt);
        }

        writer.WriteEndObject();
        writer.WriteString("databasePath", context.DatabasePath);
        if (ratesVersion is null)
        {
            writer.WriteNull("ratesVersion");
        }
        else
        {
            writer.WriteString("ratesVersion", ratesVersion);
        }

        writer.WriteString("ratesSource", downloadedRates ? "downloaded" : "built-in");
        if (ratesError is null)
        {
            writer.WriteNull("ratesError");
        }
        else
        {
            writer.WriteString("ratesError", ratesError);
        }

        writer.WriteBoolean("copilotOtelEnabled", enabled);
        writer.WriteStartArray("warnings");
        foreach (string warning in warnings)
        {
            writer.WriteStringValue(warning);
        }

        writer.WriteEndArray();
        writer.WriteStartArray("periods");
        foreach ((string _, Period period, LedgerRollupRow total) in totals)
        {
            writer.WriteStartObject();
            writer.WriteString("label", period.Label);
            LedgerFormat.WriteTime(writer, "from", period.From);
            LedgerFormat.WriteTotals(writer, total);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
        context.Out.WriteLine(LedgerFormat.FinishJson(writer, stream));
    }

    private static void WriteRollupJson(
        CliContext context,
        Period period,
        string? repository,
        LedgerGrouping grouping,
        IReadOnlyList<LedgerRollupRow> rows)
    {
        using MemoryStream stream = new();
        using Utf8JsonWriter writer = new(stream, JsonOptions);
        writer.WriteStartObject();
        writer.WriteString("period", period.Label);
        LedgerFormat.WriteTime(writer, "from", period.From);
        if (repository is null)
        {
            writer.WriteNull("repository");
        }
        else
        {
            writer.WriteString("repository", repository);
        }

        writer.WriteStartArray("rows");
        foreach (LedgerRollupRow row in rows)
        {
            writer.WriteStartObject();
            if (grouping == LedgerGrouping.Branch)
            {
                writer.WriteString("repository", row.Repository);
                writer.WriteString("branch", row.Branch);
            }
            else
            {
                writer.WriteString("agent", row.Agent);
            }

            LedgerFormat.WriteTotals(writer, row);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
        context.Out.WriteLine(LedgerFormat.FinishJson(writer, stream));
    }

    private static void WriteTopPromptsJson(
        CliContext context,
        Period period,
        string? repository,
        IReadOnlyList<PromptChainRow> rows)
    {
        using MemoryStream stream = new();
        using Utf8JsonWriter writer = new(stream, JsonOptions);
        writer.WriteStartObject();
        writer.WriteString("period", period.Label);
        LedgerFormat.WriteTime(writer, "from", period.From);
        if (repository is null)
        {
            writer.WriteNull("repository");
        }
        else
        {
            writer.WriteString("repository", repository);
        }

        writer.WriteStartArray("rows");
        for (int index = 0; index < rows.Count; index++)
        {
            PromptChainRow row = rows[index];
            writer.WriteStartObject();
            writer.WriteNumber("rank", index + 1);
            writer.WriteString("traceId", row.TraceId);
            if (row.ConversationId is null)
            {
                writer.WriteNull("conversationId");
            }
            else
            {
                writer.WriteString("conversationId", row.ConversationId);
            }

            LedgerFormat.WriteTime(writer, "startTime", row.StartTime);
            LedgerFormat.WriteTime(writer, "endTime", row.EndTime);
            writer.WriteString("repository", row.Repository);
            writer.WriteString("branch", row.Branch);
            writer.WriteStartArray("agents");
            foreach (string agent in row.Agents)
            {
                writer.WriteStringValue(agent);
            }

            writer.WriteEndArray();
            writer.WriteStartArray("models");
            foreach (string model in row.Models)
            {
                writer.WriteStringValue(model);
            }

            writer.WriteEndArray();
            LedgerFormat.WriteTotals(writer, row);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
        context.Out.WriteLine(LedgerFormat.FinishJson(writer, stream));
    }

    private static LedgerRollupRow Sum(IReadOnlyList<LedgerRollupRow> rows) =>
        new()
        {
            SpanCount = rows.Sum(row => row.SpanCount),
            UnpricedSpanCount = rows.Sum(row => row.UnpricedSpanCount),
            InputTokens = rows.Sum(row => row.InputTokens),
            OutputTokens = rows.Sum(row => row.OutputTokens),
            CacheReadInputTokens = rows.Sum(row => row.CacheReadInputTokens),
            CacheWriteInputTokens = rows.Sum(row => row.CacheWriteInputTokens),
            CacheReportedSpanCount = rows.Sum(row => row.CacheReportedSpanCount),
            ReportedInputTokens = rows.Sum(row => row.ReportedInputTokens),
            ReportedCacheReadInputTokens = rows.Sum(row => row.ReportedCacheReadInputTokens),
            ReportedUncachedInputTokens = rows.Sum(row => row.ReportedUncachedInputTokens),
            AiCredits = rows.Sum(row => row.AiCredits),
            AiCreditsUsd = rows.Sum(row => row.AiCreditsUsd),
            FoundryByokUsd = rows.Sum(row => row.FoundryByokUsd),
            LocalSavedUsd = rows.Sum(row => row.LocalSavedUsd),
        };

    private static string[] MoneyCells(LedgerRollupRow row) =>
    [
        LedgerFormat.Count(row.SpanCount),
        LedgerFormat.Count(row.UnpricedSpanCount),
        LedgerFormat.Credits(row.AiCredits),
        LedgerFormat.Usd(row.AiCreditsUsd),
        LedgerFormat.Usd(row.FoundryByokUsd),
        LedgerFormat.Usd(row.LocalSavedUsd),
    ];

    private static string Joined(IReadOnlyList<string> values) =>
        values.Count == 0 ? "-" : string.Join(", ", values);

    private static bool IsOtelEnabled(Func<string, string?> lookup)
    {
        string? enabled = lookup("COPILOT_OTEL_ENABLED");
        if (enabled?.Trim().Equals("true", StringComparison.OrdinalIgnoreCase) == true)
        {
            return true;
        }

        string? endpoint = lookup("OTEL_EXPORTER_OTLP_ENDPOINT");
        return endpoint is not null && CopilotOtelEnvironment.IsLocalEndpoint(endpoint);
    }

    private static string CopilotEnabledLine(Func<string, string?> lookup)
    {
        string? enabled = lookup("COPILOT_OTEL_ENABLED");
        return enabled?.Trim().Equals("true", StringComparison.OrdinalIgnoreCase) == true
            ? "OTel export enabled (COPILOT_OTEL_ENABLED=true)"
            : $"OTel export enabled (OTEL_EXPORTER_OTLP_ENDPOINT={lookup("OTEL_EXPORTER_OTLP_ENDPOINT")!.Trim()})";
    }

    private static string DaemonLine(
        bool running,
        DaemonState? state,
        CliContext context,
        string command)
    {
        if (!running)
        {
            return $"not running; start it with: {command} daemon start";
        }

        return state is null
            ? "running"
            : $"running (pid {state.ProcessId}) on {state.Endpoint} since " +
              LedgerFormat.LocalTime(state.StartedAt, context.TimeProvider.LocalTimeZone);
    }

    private static Option<bool> JsonOption() =>
        new("--json") { Description = "Write JSON instead of a table." };

    private static Option<string?> RepositoryOption() =>
        new("--repo") { Description = "Limit the report to this repository key." };

    private static Option<string> SinceOption(CliContext context)
    {
        Option<string> option = new("--since")
        {
            Description = "Period: today, all, Nd, or yyyy-MM-dd.",
            DefaultValueFactory = _ => "30d",
        };
        option.Validators.Add(result =>
        {
            string value = result.GetValueOrDefault<string>() ?? string.Empty;
            if (!Period.TryParse(value, context, out _))
            {
                result.AddError(
                    $"Invalid --since value {value}. Use today, all, <N>d (1-3650), or yyyy-MM-dd.");
            }
        });
        return option;
    }

    private static Period ParsePeriod(CliContext context, string? value) =>
        Period.TryParse(value, context, out Period period)
            ? period
            : throw new InvalidOperationException();

    private static Period RequirePeriod(CliContext context, string value) =>
        ParsePeriod(context, value);
}
