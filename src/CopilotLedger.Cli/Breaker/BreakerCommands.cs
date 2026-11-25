using System.CommandLine;
using System.CommandLine.Parsing;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using CopilotLedger.Cli.Install;
using CopilotLedger.Cli.Reporting;
using CopilotLedger.Core.Breaker;
using CopilotLedger.Core.Git;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Otlp.Breaker;
using CopilotLedger.Storage;

namespace CopilotLedger.Cli.Breaker;

internal static class BreakerCommands
{
    private static readonly JsonWriterOptions JsonOptions = new()
    {
        Indented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static IEnumerable<Command> Create(CliContext context)
    {
        Command breaker = new(
            "breaker",
            "Show and control the runaway-loop circuit breaker.");
        breaker.Add(CreateBreakerStatus(context));
        breaker.Add(CreateBreakerReset(context));
        breaker.Add(CreateBreakerConfig(context));
        yield return breaker;

        Command budget = new(
            "budget",
            "Set soft AI-credit budgets for branches.");
        budget.Add(CreateBudgetSet(context));
        budget.Add(CreateBudgetClear(context));
        budget.Add(CreateBudgetList(context));
        yield return budget;
    }

    private static Command CreateBreakerStatus(CliContext context)
    {
        Command command = new(
            "status",
            "Show the breaker's state, rules, and open trips.");
        Option<bool> json = new("--json") { Description = "Output JSON." };
        command.Add(json);
        command.SetAction(
            (ParseResult result, CancellationToken cancellationToken) =>
                BreakerStatusAsync(context, result.GetValue(json), cancellationToken));
        return command;
    }

    private static Command CreateBreakerReset(CliContext context)
    {
        Command command = new("reset", "Close open trips.");
        Option<long?> id = new("--id") { Description = "Trip ID." };
        Option<bool> all = new("--all") { Description = "Close every open trip." };
        Option<string?> repository = new("--repo") { Description = "Repository key." };
        Option<string?> branch = new("--branch") { Description = "Branch name." };
        command.Add(id);
        command.Add(all);
        command.Add(repository);
        command.Add(branch);
        command.Validators.Add(result =>
        {
            string? repo = result.GetValue(repository);
            string? branchName = result.GetValue(branch);
            if ((result.GetValue(id).HasValue ? 1 : 0)
                + (result.GetValue(all) ? 1 : 0)
                + (repo is not null || branchName is not null ? 1 : 0) > 1)
            {
                result.AddError("Use only one of --id, --all, or --repo with --branch.");
            }

            if ((repo is null) != (branchName is null))
            {
                result.AddError("--repo and --branch must be used together.");
            }
        });
        command.SetAction(
            (ParseResult result, CancellationToken cancellationToken) =>
                BreakerResetAsync(
                    context,
                    result.GetValue(id),
                    result.GetValue(all),
                    result.GetValue(repository),
                    result.GetValue(branch),
                    cancellationToken));
        return command;
    }

    private static Command CreateBreakerConfig(CliContext context)
    {
        Command command = new("config", "Show or change the breaker's rules.");
        Option<string?> velocityCredits = new("--velocity-credits")
        {
            Description = "Credit-velocity threshold.",
        };
        Option<int?> velocityMinutes = new("--velocity-minutes")
        {
            Description = "Credit-velocity window in minutes.",
        };
        Option<int?> churnFailures = new("--churn-failures")
        {
            Description = "Consecutive failed tool calls.",
        };
        Option<string?> flagFile = new("--flag-file")
        {
            Description = "Whether to write the flag file.",
        };
        Option<string?> notifications = new("--notifications")
        {
            Description = "Whether to show desktop notifications.",
        };
        velocityCredits.Validators.Add(result =>
        {
            if (!TryParseCreditRange(
                result.GetValue(velocityCredits),
                minimum: 1m,
                out _))
            {
                result.AddError(
                    "--velocity-credits must be a number from 1 to 1000000 with at most 2 decimal places.");
            }
        });
        velocityMinutes.Validators.Add(result =>
        {
            int? value = result.GetValue(velocityMinutes);
            if (value is not null and (< 1 or > 1440))
            {
                result.AddError("--velocity-minutes must be from 1 to 1440.");
            }
        });
        churnFailures.Validators.Add(result =>
        {
            int? value = result.GetValue(churnFailures);
            if (value is not null and (< 2 or > 100))
            {
                result.AddError("--churn-failures must be from 2 to 100.");
            }
        });
        flagFile.Validators.Add(result =>
        {
            string? value = result.GetValue(flagFile);
            if (value is not null && value is not "on" and not "off")
            {
                result.AddError("--flag-file must be on or off.");
            }
        });
        notifications.Validators.Add(result =>
        {
            string? value = result.GetValue(notifications);
            if (value is not null && value is not "on" and not "off")
            {
                result.AddError("--notifications must be on or off.");
            }
        });
        command.Add(velocityCredits);
        command.Add(velocityMinutes);
        command.Add(churnFailures);
        command.Add(flagFile);
        command.Add(notifications);
        command.SetAction(
            (ParseResult result, CancellationToken cancellationToken) =>
                BreakerConfigAsync(
                    context,
                    result.GetValue(velocityCredits),
                    result.GetValue(velocityMinutes),
                    result.GetValue(churnFailures),
                    result.GetValue(flagFile),
                    result.GetValue(notifications),
                    cancellationToken));
        return command;
    }

    private static Command CreateBudgetSet(CliContext context)
    {
        Command command = new("set", "Set the current branch's budget.");
        Argument<string> credits = new("credits") { Description = "Budget in AI credits." };
        Option<string?> repository = new("--repo") { Description = "Repository key." };
        Option<string?> branch = new("--branch") { Description = "Branch name." };
        credits.Validators.Add(result =>
        {
            if (!TryParseCreditRange(
                result.GetValueOrDefault<string>(),
                minimum: decimal.Zero,
                out _,
                minimumInclusive: false))
            {
                result.AddError(
                    "Credits must be a number greater than 0 and at most 1000000 with at most 2 decimal places.");
            }
        });
        AddScopeValidator(command, repository, branch);
        command.Add(credits);
        command.Add(repository);
        command.Add(branch);
        command.SetAction(
            (ParseResult result, CancellationToken cancellationToken) =>
                BudgetSetAsync(
                    context,
                    result.GetValue(credits)!,
                    result.GetValue(repository),
                    result.GetValue(branch),
                    cancellationToken));
        return command;
    }

    private static Command CreateBudgetClear(CliContext context)
    {
        Command command = new("clear", "Remove the current branch's budget.");
        Option<string?> repository = new("--repo") { Description = "Repository key." };
        Option<string?> branch = new("--branch") { Description = "Branch name." };
        AddScopeValidator(command, repository, branch);
        command.Add(repository);
        command.Add(branch);
        command.SetAction(
            (ParseResult result, CancellationToken cancellationToken) =>
                BudgetClearAsync(
                    context,
                    result.GetValue(repository),
                    result.GetValue(branch),
                    cancellationToken));
        return command;
    }

    private static Command CreateBudgetList(CliContext context)
    {
        Command command = new("list", "List branch budgets and their spend.");
        Option<bool> json = new("--json") { Description = "Output JSON." };
        command.Add(json);
        command.SetAction(
            (ParseResult result, CancellationToken cancellationToken) =>
                BudgetListAsync(context, result.GetValue(json), cancellationToken));
        return command;
    }

    private static void AddScopeValidator(
        Command command,
        Option<string?> repository,
        Option<string?> branch) =>
        command.Validators.Add(result =>
        {
            if ((result.GetValue(repository) is null)
                != (result.GetValue(branch) is null))
            {
                result.AddError("--repo and --branch must be used together.");
            }
        });

    private static async Task<int> BreakerStatusAsync(
        CliContext context,
        bool json,
        CancellationToken cancellationToken)
    {
        SqliteLedgerStore store = await OpenStoreAsync(context, cancellationToken);
        BreakerSettings settings = await store.GetBreakerSettingsAsync(cancellationToken);
        IReadOnlyList<BreakerTrip> trips =
            await store.GetOpenBreakerTripsAsync(
                BreakerTripFilter.All,
                cancellationToken);
        if (json)
        {
            WriteStatusJson(context, settings, trips);
            return 0;
        }

        WriteSettingsSummary(context, settings, trips.Count);
        if (trips.Count != 0)
        {
            context.Out.WriteLine();
            foreach (BreakerTrip trip in trips)
            {
                context.Out.WriteLine(
                    $"Trip {trip.Id.ToString(CultureInfo.InvariantCulture)} at {LedgerFormat.LocalTime(trip.TrippedAt, context.TimeProvider.LocalTimeZone)}: {trip.Repository} @ {trip.Branch}: {trip.Detail} on one prompt");
                context.Out.WriteLine(
                    $"  rule {BreakerRuleNames.ToName(trip.Rule)}, trace {trip.TraceId}, session {trip.ConversationId ?? "(none)"}");
            }

            context.Out.WriteLine(
                $"Reset a trip with: {GhExtension.CommandName(context.Platform)} breaker reset --id <id>");
        }

        return 0;
    }

    private static async Task<int> BreakerResetAsync(
        CliContext context,
        long? id,
        bool all,
        string? repository,
        string? branch,
        CancellationToken cancellationToken)
    {
        SqliteLedgerStore store = await OpenStoreAsync(context, cancellationToken);
        BreakerTripFilter filter;
        if (id is not null)
        {
            filter = new BreakerTripFilter { Id = id };
        }
        else if (all)
        {
            filter = BreakerTripFilter.All;
        }
        else if (repository is not null && branch is not null)
        {
            filter = new BreakerTripFilter
            {
                Repository = repository,
                Branch = branch,
            };
        }
        else
        {
            if (!TryResolveScope(context, out string scopeRepository, out string scopeBranch))
            {
                context.Error.WriteLine(
                    "Not in a Git repository. Pass --repo and --branch, --id, or --all.");
                return 1;
            }

            filter = new BreakerTripFilter
            {
                Repository = scopeRepository,
                Branch = scopeBranch,
            };
        }

        IReadOnlyList<BreakerTrip> trips =
            await store.ResetBreakerTripsAsync(filter, cancellationToken);
        context.Out.WriteLine(trips.Count switch
        {
            0 => "No open trips to reset.",
            1 => "Reset 1 trip.",
            int count => $"Reset {count.ToString(CultureInfo.InvariantCulture)} trips.",
        });
        await SyncFlagFilesAsync(context, store, trips, cancellationToken);
        return 0;
    }

    private static async Task<int> BreakerConfigAsync(
        CliContext context,
        string? velocityCredits,
        int? velocityMinutes,
        int? churnFailures,
        string? flagFile,
        string? notifications,
        CancellationToken cancellationToken)
    {
        SqliteLedgerStore store = await OpenStoreAsync(context, cancellationToken);
        BreakerSettings settings = await store.GetBreakerSettingsAsync(cancellationToken);
        bool changed = velocityCredits is not null
            || velocityMinutes is not null
            || churnFailures is not null
            || flagFile is not null
            || notifications is not null;
        if (changed)
        {
            settings = settings with
            {
                VelocityCredits = velocityCredits is null
                    ? settings.VelocityCredits
                    : decimal.Parse(
                        velocityCredits,
                        NumberStyles.AllowDecimalPoint,
                        CultureInfo.InvariantCulture),
                VelocityWindowMinutes = velocityMinutes ?? settings.VelocityWindowMinutes,
                ChurnFailures = churnFailures ?? settings.ChurnFailures,
                FlagFile = flagFile is null
                    ? settings.FlagFile
                    : flagFile == "on",
                Notifications = notifications is null
                    ? settings.Notifications
                    : notifications == "on",
            };
            await store.UpdateBreakerSettingsAsync(settings, cancellationToken);
            context.Out.WriteLine("Updated the circuit breaker settings.");
        }

        context.Out.WriteLine($"Rules: {BreakerMessages.RulesSummary(settings)}");
        context.Out.WriteLine(
            $"Flag file: {OnOff(settings.FlagFile)}. Notifications: {OnOff(settings.Notifications)}.");
        if (flagFile is not null)
        {
            IReadOnlyList<BreakerTrip> trips =
                await store.GetOpenBreakerTripsAsync(
                    BreakerTripFilter.All,
                    cancellationToken);
            await SyncFlagFilesAsync(context, store, trips, cancellationToken);
        }

        return 0;
    }

    private static async Task<int> BudgetSetAsync(
        CliContext context,
        string credits,
        string? repository,
        string? branch,
        CancellationToken cancellationToken)
    {
        SqliteLedgerStore store = await OpenStoreAsync(context, cancellationToken);
        if (!TryResolveBudgetScope(
            context,
            repository,
            branch,
            out string scopeRepository,
            out string scopeBranch))
        {
            return 1;
        }

        decimal value = decimal.Parse(
            credits,
            NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture);
        await store.SetBranchBudgetAsync(
            scopeRepository,
            scopeBranch,
            value,
            cancellationToken);
        BranchBudgetRow row = (await store.GetBranchBudgetsAsync(
            scopeRepository,
            scopeBranch,
            cancellationToken)).Single();
        context.Out.WriteLine(
            $"Budget for {scopeRepository} @ {scopeBranch}: {BreakerMessages.Credits(row.BudgetAiCredits)} AI credits. Spent so far: {BreakerMessages.Credits(row.SpentAiCredits)}.");
        if (row.Exceeded)
        {
            context.Out.WriteLine("This branch is already over its budget.");
        }

        return 0;
    }

    private static async Task<int> BudgetClearAsync(
        CliContext context,
        string? repository,
        string? branch,
        CancellationToken cancellationToken)
    {
        SqliteLedgerStore store = await OpenStoreAsync(context, cancellationToken);
        if (!TryResolveBudgetScope(
            context,
            repository,
            branch,
            out string scopeRepository,
            out string scopeBranch))
        {
            return 1;
        }

        bool cleared = await store.ClearBranchBudgetAsync(
            scopeRepository,
            scopeBranch,
            cancellationToken);
        context.Out.WriteLine(cleared
            ? $"Cleared the budget for {scopeRepository} @ {scopeBranch}."
            : $"No budget is set for {scopeRepository} @ {scopeBranch}.");
        return 0;
    }

    private static async Task<int> BudgetListAsync(
        CliContext context,
        bool json,
        CancellationToken cancellationToken)
    {
        SqliteLedgerStore store = await OpenStoreAsync(context, cancellationToken);
        IReadOnlyList<BranchBudgetRow> rows =
            await store.GetBranchBudgetsAsync(
                cancellationToken: cancellationToken);
        if (json)
        {
            WriteBudgetsJson(context, rows);
            return 0;
        }

        if (rows.Count == 0)
        {
            context.Out.WriteLine("No branch budgets are set.");
            return 0;
        }

        foreach (BranchBudgetRow row in rows)
        {
            context.Out.WriteLine(
                $"{row.Repository} @ {row.Branch}: {BreakerMessages.Credits(row.SpentAiCredits)} of {BreakerMessages.Credits(row.BudgetAiCredits)} AI credits{(row.Exceeded ? " (over budget)" : string.Empty)}");
        }

        return 0;
    }

    private static async Task<SqliteLedgerStore> OpenStoreAsync(
        CliContext context,
        CancellationToken cancellationToken)
    {
        SqliteLedgerStore store = new(context.DatabasePath, context.TimeProvider);
        await store.InitializeAsync(cancellationToken);
        return store;
    }

    private static bool TryResolveBudgetScope(
        CliContext context,
        string? repository,
        string? branch,
        out string resolvedRepository,
        out string resolvedBranch)
    {
        if (repository is not null && branch is not null)
        {
            resolvedRepository = repository;
            resolvedBranch = branch;
            return true;
        }

        if (TryResolveScope(context, out resolvedRepository, out resolvedBranch))
        {
            return true;
        }

        context.Error.WriteLine("Not in a Git repository. Pass --repo and --branch.");
        return false;
    }

    private static bool TryResolveScope(
        CliContext context,
        out string repository,
        out string branch)
    {
        GitWorkspaceContext? git;
        try
        {
            git = new FileSystemGitContextResolver().Resolve(context.WorkingDirectory);
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            git = null;
        }

        if (git is null)
        {
            repository = string.Empty;
            branch = string.Empty;
            return false;
        }

        repository = git.RepositorySlug ?? git.RepositoryRoot;
        branch = git.BranchName ?? "(none)";
        return true;
    }

    private static async Task SyncFlagFilesAsync(
        CliContext context,
        SqliteLedgerStore store,
        IReadOnlyList<BreakerTrip> trips,
        CancellationToken cancellationToken)
    {
        foreach (string root in trips
            .Where(trip => trip.RepositoryRoot is not null)
            .Select(trip => trip.RepositoryRoot!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(root => root, StringComparer.Ordinal))
        {
            try
            {
                await CircuitFlagFile.SyncAsync(
                    store,
                    root,
                    GhExtension.CommandName(context.Platform),
                    cancellationToken);
            }
            catch (Exception ex) when (
                ex is IOException or UnauthorizedAccessException)
            {
                context.Error.WriteLine(
                    $"Warning: could not update {CircuitFlagFile.GetPath(root)}: {ex.Message}");
            }
        }
    }

    private static bool TryParseCreditRange(
        string? value,
        decimal minimum,
        out decimal parsed,
        bool minimumInclusive = true)
    {
        parsed = 0m;
        return value is not null
            && decimal.TryParse(
                value,
                NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out parsed)
            && (minimumInclusive ? parsed >= minimum : parsed > minimum)
            && parsed <= 1_000_000m
            && parsed % 0.01m == 0m;
    }

    private static void WriteSettingsSummary(
        CliContext context,
        BreakerSettings settings,
        int tripCount)
    {
        context.Out.WriteLine(tripCount == 0
            ? "Circuit breaker: closed"
            : $"Circuit breaker: open ({tripCount.ToString(CultureInfo.InvariantCulture)} {(tripCount == 1 ? "trip" : "trips")})");
        context.Out.WriteLine($"Rules: {BreakerMessages.RulesSummary(settings)}");
        context.Out.WriteLine(
            $"Flag file: {OnOff(settings.FlagFile)}. Notifications: {OnOff(settings.Notifications)}.");
    }

    private static void WriteStatusJson(
        CliContext context,
        BreakerSettings settings,
        IReadOnlyList<BreakerTrip> trips)
    {
        using MemoryStream stream = new();
        using Utf8JsonWriter writer = new(stream, JsonOptions);
        writer.WriteStartObject();
        writer.WriteString("state", trips.Count == 0 ? "closed" : "open");
        writer.WriteStartObject("settings");
        LedgerFormat.WriteMoney(writer, "velocityCredits", settings.VelocityCredits);
        writer.WriteNumber("velocityWindowMinutes", settings.VelocityWindowMinutes);
        writer.WriteNumber("churnFailures", settings.ChurnFailures);
        writer.WriteBoolean("flagFile", settings.FlagFile);
        writer.WriteBoolean("notifications", settings.Notifications);
        writer.WriteEndObject();
        writer.WriteStartArray("openTrips");
        foreach (BreakerTrip trip in trips)
        {
            writer.WriteStartObject();
            writer.WriteNumber("id", trip.Id);
            writer.WriteString("rule", BreakerRuleNames.ToName(trip.Rule));
            writer.WriteString("repository", trip.Repository);
            writer.WriteString("branch", trip.Branch);
            if (trip.RepositoryRoot is null)
            {
                writer.WriteNull("repositoryRoot");
            }
            else
            {
                writer.WriteString("repositoryRoot", trip.RepositoryRoot);
            }

            if (trip.ConversationId is null)
            {
                writer.WriteNull("conversationId");
            }
            else
            {
                writer.WriteString("conversationId", trip.ConversationId);
            }

            writer.WriteString("traceId", trip.TraceId);
            LedgerFormat.WriteTime(writer, "trippedAt", trip.TrippedAt);
            writer.WriteString("detail", trip.Detail);
            if (trip.WindowCostUsd is null)
            {
                writer.WriteNull("windowAiCredits");
            }
            else
            {
                LedgerFormat.WriteMoney(
                    writer,
                    "windowAiCredits",
                    trip.WindowCostUsd.Value / AiCreditMath.UsdPerAiCredit);
            }

            if (trip.WindowMinutes is null)
            {
                writer.WriteNull("windowMinutes");
            }
            else
            {
                writer.WriteNumber("windowMinutes", trip.WindowMinutes.Value);
            }

            if (trip.FailureCount is null)
            {
                writer.WriteNull("failureCount");
            }
            else
            {
                writer.WriteNumber("failureCount", trip.FailureCount.Value);
            }

            LedgerFormat.WriteTime(writer, "recordedAt", trip.RecordedAt);
            LedgerFormat.WriteTime(writer, "notifiedAt", trip.NotifiedAt);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
        context.Out.WriteLine(LedgerFormat.FinishJson(writer, stream));
    }

    private static void WriteBudgetsJson(
        CliContext context,
        IReadOnlyList<BranchBudgetRow> rows)
    {
        using MemoryStream stream = new();
        using Utf8JsonWriter writer = new(stream, JsonOptions);
        writer.WriteStartObject();
        writer.WriteStartArray("budgets");
        foreach (BranchBudgetRow row in rows)
        {
            writer.WriteStartObject();
            writer.WriteString("repository", row.Repository);
            writer.WriteString("branch", row.Branch);
            LedgerFormat.WriteMoney(writer, "budgetAiCredits", row.BudgetAiCredits);
            LedgerFormat.WriteMoney(writer, "spentAiCredits", row.SpentAiCredits);
            writer.WriteBoolean("exceeded", row.Exceeded);
            LedgerFormat.WriteTime(writer, "setAt", row.SetAt);
            LedgerFormat.WriteTime(writer, "alertedAt", row.AlertedAt);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
        context.Out.WriteLine(LedgerFormat.FinishJson(writer, stream));
    }

    private static string OnOff(bool value) => value ? "on" : "off";
}
