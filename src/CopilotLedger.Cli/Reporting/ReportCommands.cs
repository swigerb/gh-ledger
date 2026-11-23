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
using CopilotLedger.Core.Telemetry;
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
        new("API equiv USD", true),
        new("Local saved", true),
    ];

    private static readonly TableColumn[] CacheColumns =
    [
        new("Spans", true),
        new("Cache reported", true),
        new("Input", true),
        new("Cache read", true),
        new("Cache write", true),
        new("Uncached", true),
        new("Output", true),
        new("Hit %", true),
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
        Command command = new("status", "Show daemon, configuration, and recent spend, or prompt-cache efficiency.");
        Option<bool> json = JsonOption();
        Option<bool> cache = new("--cache")
        {
            Description = "Report prompt-cache efficiency instead.",
        };
        Option<string> by = new("--by")
        {
            Description = "Group --cache rows by model, branch, pr, or agent.",
            DefaultValueFactory = _ => "model",
        };
        by.Validators.Add(result =>
        {
            if (result.GetValueOrDefault<string>() is not ("model" or "branch" or "pr" or "agent"))
            {
                result.AddError("--by must be model, branch, pr, or agent.");
            }
        });
        Option<string?> repository = RepositoryOption();
        Option<string?> branch = new("--branch")
        {
            Description = "Limit --cache to this branch, or (none).",
        };
        Option<int?> pullRequest = new("--pr")
        {
            Description = "Limit --cache to this pull request number.",
        };
        pullRequest.Validators.Add(result =>
        {
            if (result.GetValueOrDefault<int?>() is < 1)
            {
                result.AddError("--pr must be a positive integer.");
            }
        });
        Option<string> since = SinceOption(context);
        Option<bool> turns = new("--turns")
        {
            Description = "With --cache, list the model requests of one trace or conversation.",
        };
        Option<string?> trace = new("--trace")
        {
            Description = "The trace ID for --turns.",
        };
        trace.Validators.Add(result =>
        {
            string value = result.GetValueOrDefault<string?>() ?? string.Empty;
            if (value.Length != 32 || !value.All(char.IsAsciiHexDigit))
            {
                result.AddError("--trace must be a 32-character hexadecimal trace ID.");
            }
        });
        Option<string?> conversation = new("--conversation")
        {
            Description = "The conversation (Copilot session) ID for --turns.",
        };
        conversation.Validators.Add(result =>
        {
            if (string.IsNullOrWhiteSpace(result.GetValueOrDefault<string?>()))
            {
                result.AddError("--conversation can't be blank.");
            }
        });
        Option<bool> bloat = new("--bloat")
        {
            Description = "With --cache, rank prompt chains by uncached input and input growth.",
        };
        Option<int> limit = new("--limit")
        {
            Description = "Prompt chains in each --bloat ranking.",
            DefaultValueFactory = _ => 10,
        };
        limit.Validators.Add(result =>
        {
            if (result.GetValueOrDefault<int>() is < 1 or > 100)
            {
                result.AddError("--limit must be from 1 to 100.");
            }
        });
        command.Add(json);
        command.Add(cache);
        command.Add(by);
        command.Add(repository);
        command.Add(branch);
        command.Add(pullRequest);
        command.Add(since);
        command.Add(turns);
        command.Add(trace);
        command.Add(conversation);
        command.Add(bloat);
        command.Add(limit);
        Option<string?> pricing = WhatIfPricing.CreateOption();
        command.Add(pricing);
        command.Validators.Add(result =>
        {
            if (result.GetValue(cache) && result.GetResult(pricing) is { Implicit: false })
            {
                result.AddError("--pricing can't be used with --cache.");
                return;
            }

            string[] given = new Option[] { by, repository, branch, pullRequest, since, turns, trace, conversation, bloat, limit }
                .Where(option => result.GetResult(option) is { Implicit: false })
                .Select(option => option.Name)
                .ToArray();
            if (!result.GetValue(cache))
            {
                if (given.Length > 0)
                {
                    result.AddError($"{given[0]} requires --cache.");
                }

                return;
            }

            bool turnsView = result.GetValue(turns);
            bool bloatView = result.GetValue(bloat);
            string? error;
            if (turnsView && bloatView)
            {
                error = "Use either --turns or --bloat, not both.";
            }
            else if (turnsView)
            {
                string? other = given.FirstOrDefault(name => name is not ("--turns" or "--trace" or "--conversation"));
                error = other is not null
                    ? $"{other} can't be used with --turns."
                    : given.Contains("--trace") == given.Contains("--conversation")
                        ? "Use --turns with either --trace <traceId> or --conversation <id>."
                        : null;
            }
            else
            {
                string? turnsOnly = given.FirstOrDefault(name => name is "--trace" or "--conversation");
                string? notForBloat = given.FirstOrDefault(name => name is "--by" or "--branch" or "--pr");
                error = turnsOnly is not null
                    ? $"{turnsOnly} requires --turns."
                    : bloatView && notForBloat is not null
                        ? $"{notForBloat} can't be used with --bloat."
                        : !bloatView && given.Contains("--limit")
                            ? "--limit requires --bloat."
                            : null;
            }

            if (error is not null)
            {
                result.AddError(error);
            }
        });
        command.SetAction(
            (ParseResult result, CancellationToken cancellationToken) =>
                !result.GetValue(cache)
                    ? StatusAsync(context, result.GetValue(json), result.GetValue(pricing), cancellationToken)
                    : result.GetValue(turns)
                        ? TurnsAsync(
                            context,
                            result.GetValue(trace) is string traceId
                                ? TurnSelector.ForTrace(traceId)
                                : TurnSelector.ForConversation(result.GetValue(conversation)!),
                            result.GetValue(json),
                            cancellationToken)
                        : result.GetValue(bloat)
                            ? BloatAsync(
                                context,
                                result.GetValue(repository),
                                ParsePeriod(context, result.GetValue(since)),
                                result.GetValue(limit),
                                result.GetValue(json),
                                cancellationToken)
                            : CacheAsync(
                                context,
                                result.GetValue(by)!,
                                new LedgerFilter
                                {
                                    Repository = result.GetValue(repository),
                                    Branch = result.GetValue(branch),
                                    PullRequestNumber = result.GetValue(pullRequest),
                                },
                                ParsePeriod(context, result.GetValue(since)),
                                result.GetValue(json),
                                cancellationToken));
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
        Option<string?> pricing = WhatIfPricing.CreateOption();
        command.Add(repository);
        command.Add(allRepositories);
        command.Add(since);
        command.Add(json);
        command.Add(pricing);
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
                    result.GetValue(pricing),
                    cancellationToken));
        return command;
    }

    private static Command CreateAgents(CliContext context)
    {
        Command command = new("agents", "Report spend by agent.");
        Option<string?> repository = RepositoryOption();
        Option<string> since = SinceOption(context);
        Option<bool> json = JsonOption();
        Option<string?> pricing = WhatIfPricing.CreateOption();
        command.Add(repository);
        command.Add(since);
        command.Add(json);
        command.Add(pricing);
        command.SetAction(
            (ParseResult result, CancellationToken cancellationToken) =>
                RollupAsync(
                    context,
                    LedgerGrouping.Agent,
                    result.GetValue(repository),
                    ParsePeriod(context, result.GetValue(since)),
                    result.GetValue(json),
                    result.GetValue(pricing),
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
        Option<string?> pricing = WhatIfPricing.CreateOption();
        command.Add(repository);
        command.Add(since);
        command.Add(limit);
        command.Add(json);
        command.Add(pricing);
        command.SetAction(
            (ParseResult result, CancellationToken cancellationToken) =>
                TopPromptsAsync(
                    context,
                    result.GetValue(repository),
                    ParsePeriod(context, result.GetValue(since)),
                    result.GetValue(limit),
                    result.GetValue(json),
                    result.GetValue(pricing),
                    cancellationToken));
        return command;
    }

    private static async Task<int> StatusAsync(
        CliContext context,
        bool json,
        string? pricingFile,
        CancellationToken cancellationToken)
    {
        if (!TryLoadPricing(context, pricingFile, out WhatIfPricing? pricing))
        {
            return 1;
        }

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
        List<(string Label, Period Period, StatusTotals Totals)> totals = [];
        foreach ((string label, Period period) in periods)
        {
            IReadOnlyList<LedgerRollupRow> rows = await store.GetRollupAsync(
                LedgerGrouping.ChannelModel,
                new LedgerFilter { From = period.From },
                pricing?.Calculator,
                cancellationToken);
            totals.Add((label, period, new StatusTotals(
                Sum(rows),
                Sum([.. rows.Where(row => row.Channel == BillingChannel.GitHubAiCredits)]),
                Sum([.. rows.Where(row => row.Channel == BillingChannel.Local)]))));
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
                rates.DownloadedJson is not null,
                pricing);
            return 0;
        }

        context.Out.WriteLine(extension
            ? $"gh-ledger {GhLedgerCli.Version} (GitHub CLI extension)"
            : $"gh-ledger {GhLedgerCli.Version}");
        context.Out.WriteLine($"Daemon:  {DaemonLine(running, state, context, command)}");
        context.Out.WriteLine($"Ledger:  {context.DatabasePath}");
        context.Out.WriteLine(
            $"Rates:   {(ratesError is not null ? $"invalid overlay: {ratesError}" : rates.DownloadedJson is null ? ratesVersion : $"{ratesVersion} (downloaded)")}");
        WritePricingLabel(context, pricing);
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
                (IReadOnlyList<string>)[item.Label, .. MoneyCells(item.Totals.All)]).ToArray());
        context.Out.WriteLine();
        string[][] lenses = totals.Select(item => new[]
        {
            item.Label,
            LedgerFormat.Usd(item.Totals.Copilot.AiCreditsUsd),
            LedgerFormat.ApiEquivalent(item.Totals.Copilot),
            BilledToApi(item.Totals.Copilot),
            LedgerFormat.Usd(item.Totals.Local.LocalSavedUsd),
            LedgerFormat.ApiEquivalent(item.Totals.Local),
        }).ToArray();
        TableWriter.Write(
            context.Out,
            [
                new("Period", false),
                new("GitHub USD", true),
                new("At API prices", true),
                new("GitHub/API", true),
                new("Local saved", true),
                new("Local at API prices", true),
            ],
            lenses);
        LedgerRollupRow widest = totals[^1].Totals.All;
        WriteApiEquivalentFootnotes(
            context,
            [
                .. totals.Select(item => LedgerFormat.ApiEquivalent(item.Totals.All)),
                .. lenses.SelectMany(row => new[] { row[2], row[5] }),
            ],
            widest.SpanCount,
            widest.ApiEquivalentUnpricedSpanCount,
            widest.ApiEquivalentEstimatedSpanCount,
            " in the last 30 days");

        context.Out.WriteLine();
        TableWriter.Write(
            context.Out,
            [new("Period", false), new("Cache hit", true), new("Cache reported", false)],
            totals.Select(item =>
                (IReadOnlyList<string>)
                [
                    item.Label,
                    LedgerFormat.Percent(item.Totals.All.CacheHitRatio),
                    $"{LedgerFormat.Count(item.Totals.All.CacheReportedSpanCount)} of {Spans(item.Totals.All.SpanCount)}",
                ]).ToArray());
        return 0;
    }

    private static async Task<int> BranchAsync(
        CliContext context,
        string? requestedRepository,
        bool allRepositories,
        Period period,
        bool json,
        string? pricingFile,
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
            pricingFile,
            cancellationToken);
    }

    private static async Task<int> RollupAsync(
        CliContext context,
        LedgerGrouping grouping,
        string? repository,
        Period period,
        bool json,
        string? pricingFile,
        CancellationToken cancellationToken) =>
        await RollupRowsAsync(
            context,
            grouping,
            repository,
            allRepositories: false,
            period,
            json,
            pricingFile,
            cancellationToken);

    private static async Task<int> RollupRowsAsync(
        CliContext context,
        LedgerGrouping grouping,
        string? repository,
        bool allRepositories,
        Period period,
        bool json,
        string? pricingFile,
        CancellationToken cancellationToken)
    {
        if (!TryLoadPricing(context, pricingFile, out WhatIfPricing? pricing))
        {
            return 1;
        }

        SqliteLedgerStore store = new(context.DatabasePath, context.TimeProvider);
        await store.InitializeAsync(cancellationToken);
        IReadOnlyList<LedgerRollupRow> rows = await store.GetRollupAsync(
            grouping,
            new LedgerFilter
            {
                From = period.From,
                Repository = repository,
            },
            pricing?.Calculator,
            cancellationToken);
        string scope = repository ?? "all repositories";

        if (json)
        {
            WriteRollupJson(context, period, repository, grouping, rows, pricing);
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
        WritePricingLabel(context, pricing);
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
        WriteApiEquivalentFootnotes(
            context,
            [.. rows.Select(LedgerFormat.ApiEquivalent)],
            rows.Sum(row => row.SpanCount),
            rows.Sum(row => row.ApiEquivalentUnpricedSpanCount),
            rows.Sum(row => row.ApiEquivalentEstimatedSpanCount),
            string.Empty);
        return 0;
    }

    private static async Task<int> TopPromptsAsync(
        CliContext context,
        string? repository,
        Period period,
        int limit,
        bool json,
        string? pricingFile,
        CancellationToken cancellationToken)
    {
        if (!TryLoadPricing(context, pricingFile, out WhatIfPricing? pricing))
        {
            return 1;
        }

        SqliteLedgerStore store = new(context.DatabasePath, context.TimeProvider);
        await store.InitializeAsync(cancellationToken);
        IReadOnlyList<PromptChainRow> rows = await store.GetTopPromptChainsAsync(
            new LedgerFilter
            {
                From = period.From,
                Repository = repository,
            },
            limit,
            pricing?.Calculator,
            cancellationToken);
        string scope = repository ?? "all repositories";
        if (json)
        {
            WriteTopPromptsJson(context, period, repository, rows, pricing);
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
        WritePricingLabel(context, pricing);
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
            new("API equiv USD", true),
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
                LedgerFormat.ApiEquivalent(row),
                LedgerFormat.Usd(row.LocalSavedUsd),
                row.TraceId,
            ]).ToArray();
        TableWriter.Write(context.Out, columns, tableRows);
        WriteApiEquivalentFootnotes(
            context,
            [.. rows.Select(LedgerFormat.ApiEquivalent)],
            rows.Sum(row => row.SpanCount),
            rows.Sum(row => row.ApiEquivalentUnpricedSpanCount),
            rows.Sum(row => row.ApiEquivalentEstimatedSpanCount),
            string.Empty);
        return 0;
    }

    private static async Task<int> CacheAsync(
        CliContext context,
        string by,
        LedgerFilter filter,
        Period period,
        bool json,
        CancellationToken cancellationToken)
    {
        LedgerGrouping grouping = by switch
        {
            "branch" => LedgerGrouping.Branch,
            "pr" => LedgerGrouping.PullRequest,
            "agent" => LedgerGrouping.Agent,
            _ => LedgerGrouping.Model,
        };
        SqliteLedgerStore store = new(context.DatabasePath, context.TimeProvider);
        await store.InitializeAsync(cancellationToken);
        IReadOnlyList<CacheEfficiencyRow> rows = await store.GetCacheEfficiencyAsync(
            grouping,
            filter with { From = period.From },
            cancellationToken);
        if (json)
        {
            WriteCacheJson(context, period, by, filter, rows);
            return 0;
        }

        string scope = Scope(filter);
        if (rows.Count == 0)
        {
            context.Out.WriteLine($"No spans recorded in {scope} ({period.Label}).");
            return 0;
        }

        context.Out.WriteLine(
            $"Cache efficiency by {(by == "pr" ? "pull request" : by)} in {scope} ({period.Label})");
        context.Out.WriteLine();
        bool repositoryColumn = filter.Repository is null
            && grouping is LedgerGrouping.Branch or LedgerGrouping.PullRequest;
        TableColumn[] leading = grouping switch
        {
            LedgerGrouping.Model => [new("Model", false)],
            LedgerGrouping.Agent => [new("Agent", false)],
            LedgerGrouping.Branch => repositoryColumn
                ? [new("Repository", false), new("Branch", false)]
                : [new("Branch", false)],
            _ => repositoryColumn
                ? [new("Repository", false), new("Pull request", false)]
                : [new("Pull request", false)],
        };
        IReadOnlyList<IReadOnlyList<string>> tableRows = rows.Select(row =>
        {
            string key = grouping switch
            {
                LedgerGrouping.Model => row.Model!,
                LedgerGrouping.Agent => row.Agent!,
                LedgerGrouping.Branch => row.Branch!,
                _ => row.PullRequestNumber is int number
                    ? "#" + number.ToString(CultureInfo.InvariantCulture)
                    : $"(no PR) {row.Branch}",
            };
            string[] identifiers = repositoryColumn ? [row.Repository!, key] : [key];
            return (IReadOnlyList<string>)[.. identifiers, .. CacheCells(row)];
        }).ToArray();
        TableWriter.Write(context.Out, [.. leading, .. CacheColumns], tableRows);
        long unreported = rows.Sum(row => row.CacheUnreportedSpanCount);
        if (unreported > 0)
        {
            context.Out.WriteLine();
            context.Out.WriteLine(
                $"{LedgerFormat.Count(unreported)} of {Spans(rows.Sum(row => row.SpanCount))} reported no cache counters. Hit % and the cache columns leave them out; * marks a partial total.");
        }

        return 0;
    }

    private static async Task<int> TurnsAsync(
        CliContext context,
        TurnSelector selector,
        bool json,
        CancellationToken cancellationToken)
    {
        SqliteLedgerStore store = new(context.DatabasePath, context.TimeProvider);
        await store.InitializeAsync(cancellationToken);
        IReadOnlyList<TurnTokenRow> rows = await store.GetTurnBreakdownAsync(
            selector,
            LedgerFilter.All,
            cancellationToken);
        if (json)
        {
            WriteTurnsJson(context, selector, rows);
            return 0;
        }

        string scope = selector.TraceId is string traceId
            ? $"trace {traceId}"
            : $"conversation {selector.ConversationId}";
        if (rows.Count == 0)
        {
            context.Out.WriteLine($"No model requests recorded for {scope}.");
            return 0;
        }

        bool conversation = selector.ConversationId is not null;
        context.Out.WriteLine($"Model requests in {scope}");
        context.Out.WriteLine();
        TableColumn[] columns =
        [
            new("#", true),
            new("Started", false),
            new("Agent", false),
            new("Model", false),
            new("Input", true),
            new("Cache read", true),
            new("Cache write", true),
            new("Uncached", true),
            new("Output", true),
            new("Hit %", true),
            new("Growth", true),
            .. conversation ? [new TableColumn("Trace", false)] : Array.Empty<TableColumn>(),
        ];
        IReadOnlyList<IReadOnlyList<string>> tableRows = rows.Select((row, index) =>
            (IReadOnlyList<string>)
            [
                (index + 1).ToString(CultureInfo.InvariantCulture),
                LedgerFormat.LocalTimeSeconds(row.StartTime, context.TimeProvider.LocalTimeZone),
                row.Agent ?? "(none)",
                row.Model,
                LedgerFormat.Count(row.InputTokens),
                row.CacheReadInputTokens is long read ? LedgerFormat.Count(read) : "n/a",
                row.CacheWriteInputTokens is long write ? LedgerFormat.Count(write) : "n/a",
                row.UncachedInputTokens is long uncached ? LedgerFormat.Count(uncached) : "n/a",
                LedgerFormat.Count(row.OutputTokens),
                LedgerFormat.Percent(row.CacheHitRatio),
                Growth(row),
                .. conversation ? [row.TraceId] : Array.Empty<string>(),
            ]).ToArray();
        TableWriter.Write(context.Out, columns, tableRows);
        context.Out.WriteLine();
        context.Out.WriteLine(
            "Growth is the change in input from the previous request by the same agent invocation and model; - marks its first request.");
        if (rows.Any(row => row.AgentSpanId is null))
        {
            context.Out.WriteLine(
                "n/a growth: the request's agent invocation isn't recorded, so it isn't compared with others.");
        }

        long unreported = rows.Count(row => row.CacheReadInputTokens is null);
        if (unreported > 0)
        {
            context.Out.WriteLine(
                $"{LedgerFormat.Count(unreported)} of {Requests(rows.Count)} reported no cache counters.");
        }

        return 0;
    }

    private static async Task<int> BloatAsync(
        CliContext context,
        string? repository,
        Period period,
        int limit,
        bool json,
        CancellationToken cancellationToken)
    {
        SqliteLedgerStore store = new(context.DatabasePath, context.TimeProvider);
        await store.InitializeAsync(cancellationToken);
        ContextBloatReport report = await store.GetContextBloatAsync(
            new LedgerFilter { From = period.From, Repository = repository },
            limit,
            cancellationToken);
        if (json)
        {
            WriteBloatJson(context, period, repository, limit, report);
            return 0;
        }

        TimeZoneInfo zone = context.TimeProvider.LocalTimeZone;
        context.Out.WriteLine($"Context bloat in {repository ?? "all repositories"} ({period.Label})");
        context.Out.WriteLine();
        context.Out.WriteLine("Prompt chains by uncached input");
        WriteSection(
            context,
            [
                new("#", true),
                new("Started", false),
                new("Repository", false),
                new("Spans", true),
                new("Input", true),
                new("Uncached", true),
                new("Hit %", true),
                new("Trace", false),
            ],
            report.ByUncachedInput.Select((row, index) =>
                (IReadOnlyList<string>)
                [
                    (index + 1).ToString(CultureInfo.InvariantCulture),
                    LedgerFormat.LocalTime(row.StartTime, zone),
                    row.Repository,
                    LedgerFormat.Count(row.SpanCount),
                    LedgerFormat.Count(row.InputTokens),
                    LedgerFormat.Count(row.ReportedUncachedInputTokens),
                    LedgerFormat.Percent(row.CacheHitRatio),
                    row.TraceId,
                ]).ToArray());
        context.Out.WriteLine();
        context.Out.WriteLine("Prompt chains by input growth within one agent invocation and model");
        WriteSection(
            context,
            [
                new("#", true),
                new("Started", false),
                new("Repository", false),
                new("Agent", false),
                new("Model", false),
                new("Requests", true),
                new("First input", true),
                new("Peak input", true),
                new("Growth", true),
                new("Trace", false),
            ],
            report.ByInputGrowth.Select((row, index) =>
                (IReadOnlyList<string>)
                [
                    (index + 1).ToString(CultureInfo.InvariantCulture),
                    LedgerFormat.LocalTime(row.StartTime, zone),
                    row.Repository,
                    row.Agent ?? "(none)",
                    row.Model,
                    LedgerFormat.Count(row.TurnCount),
                    LedgerFormat.Count(row.FirstInputTokens),
                    LedgerFormat.Count(row.PeakInputTokens),
                    LedgerFormat.Count(row.InputGrowthTokens),
                    row.TraceId,
                ]).ToArray());
        context.Out.WriteLine();
        context.Out.WriteLine("Prompt chains with requests that reported no cache counters, by estimated uncached input");
        WriteSection(
            context,
            [
                new("#", true),
                new("Started", false),
                new("Repository", false),
                new("Spans", true),
                new("Cache reported", true),
                new("Input", true),
                new("Est. uncached", true),
                new("Trace", false),
            ],
            report.ByEstimatedUncachedInput.Select((row, index) =>
                (IReadOnlyList<string>)
                [
                    (index + 1).ToString(CultureInfo.InvariantCulture),
                    LedgerFormat.LocalTime(row.StartTime, zone),
                    row.Repository,
                    LedgerFormat.Count(row.SpanCount),
                    LedgerFormat.Count(row.CacheReportedSpanCount),
                    LedgerFormat.Count(row.InputTokens),
                    LedgerFormat.Count(row.EstimatedUncachedInputTokens),
                    row.TraceId,
                ]).ToArray());
        if (report.ByEstimatedUncachedInput.Count > 0)
        {
            context.Out.WriteLine();
            context.Out.WriteLine(
                "Est. uncached is an estimate: it counts all input of a request without cache counters as uncached, as pricing does.");
        }

        if (report.SpansWithoutAgent > 0)
        {
            context.Out.WriteLine();
            context.Out.WriteLine(
                $"Input growth leaves out {Requests(report.SpansWithoutAgent)} without a recorded agent invocation.");
        }

        return 0;
    }

    // A ranking's table, or "None." when it has no rows.
    private static void WriteSection(
        CliContext context,
        IReadOnlyList<TableColumn> columns,
        IReadOnlyList<IReadOnlyList<string>> rows)
    {
        if (rows.Count == 0)
        {
            context.Out.WriteLine("None.");
            return;
        }

        TableWriter.Write(context.Out, columns, rows);
    }

    private static void WriteStatusJson(
        CliContext context,
        bool running,
        DaemonState? state,
        string? ratesVersion,
        string? ratesError,
        bool enabled,
        IReadOnlyList<string> warnings,
        IReadOnlyList<(string Label, Period Period, StatusTotals Totals)> totals,
        bool extension,
        bool downloadedRates,
        WhatIfPricing? pricing)
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

        pricing?.Write(writer);
        writer.WriteBoolean("copilotOtelEnabled", enabled);
        writer.WriteStartArray("warnings");
        foreach (string warning in warnings)
        {
            writer.WriteStringValue(warning);
        }

        writer.WriteEndArray();
        writer.WriteStartArray("periods");
        foreach ((string _, Period period, StatusTotals periodTotals) in totals)
        {
            LedgerRollupRow total = periodTotals.All;
            writer.WriteStartObject();
            writer.WriteString("label", period.Label);
            LedgerFormat.WriteTime(writer, "from", period.From);
            LedgerFormat.WriteTotals(writer, total);
            LedgerFormat.WriteApiEquivalent(writer, total);
            writer.WritePropertyName("lenses");
            writer.WriteStartObject();
            writer.WritePropertyName("copilot");
            writer.WriteStartObject();
            writer.WriteNumber("spans", periodTotals.Copilot.SpanCount);
            writer.WriteNumber("unpricedSpans", periodTotals.Copilot.UnpricedSpanCount);
            LedgerFormat.WriteMoney(writer, "aiCredits", periodTotals.Copilot.AiCredits);
            LedgerFormat.WriteMoney(writer, "aiCreditsUsd", periodTotals.Copilot.AiCreditsUsd);
            LedgerFormat.WriteApiEquivalent(
                writer,
                periodTotals.Copilot.ApiEquivalentUsd,
                periodTotals.Copilot.ApiEquivalentUnpricedSpanCount,
                periodTotals.Copilot.ApiEquivalentEstimatedSpanCount);
            LedgerFormat.WriteDecimal(writer, "billedToApiRatio", BilledToApiRatio(periodTotals.Copilot));
            writer.WriteEndObject();
            writer.WritePropertyName("local");
            writer.WriteStartObject();
            writer.WriteNumber("spans", periodTotals.Local.SpanCount);
            LedgerFormat.WriteMoney(writer, "localSavedUsd", periodTotals.Local.LocalSavedUsd);
            LedgerFormat.WriteApiEquivalent(
                writer,
                periodTotals.Local.ApiEquivalentUsd,
                periodTotals.Local.ApiEquivalentUnpricedSpanCount,
                periodTotals.Local.ApiEquivalentEstimatedSpanCount);
            writer.WriteEndObject();
            writer.WriteEndObject();
            writer.WritePropertyName("cache");
            writer.WriteStartObject();
            writer.WriteNumber("cacheReportedSpans", total.CacheReportedSpanCount);
            writer.WriteNumber("cacheUnreportedSpans", total.CacheUnreportedSpanCount);
            writer.WriteNumber("reportedInputTokens", total.ReportedInputTokens);
            writer.WriteNumber("reportedCacheReadInputTokens", total.ReportedCacheReadInputTokens);
            LedgerFormat.WriteDecimal(writer, "cacheHitRatio", total.CacheHitRatio);
            writer.WriteEndObject();
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
        IReadOnlyList<LedgerRollupRow> rows,
        WhatIfPricing? pricing)
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

        pricing?.Write(writer);
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
            LedgerFormat.WriteApiEquivalent(writer, row);
            LedgerFormat.WriteCacheCoverage(writer, row);
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
        IReadOnlyList<PromptChainRow> rows,
        WhatIfPricing? pricing)
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

        pricing?.Write(writer);
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
            LedgerFormat.WriteApiEquivalent(writer, row);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
        context.Out.WriteLine(LedgerFormat.FinishJson(writer, stream));
    }

    private static void WriteCacheJson(
        CliContext context,
        Period period,
        string by,
        LedgerFilter filter,
        IReadOnlyList<CacheEfficiencyRow> rows)
    {
        using MemoryStream stream = new();
        using Utf8JsonWriter writer = new(stream, JsonOptions);
        writer.WriteStartObject();
        writer.WriteString("period", period.Label);
        LedgerFormat.WriteTime(writer, "from", period.From);
        writer.WriteString("by", by);
        LedgerFormat.WriteNullable(writer, "repository", filter.Repository);
        LedgerFormat.WriteNullable(writer, "branch", filter.Branch);
        LedgerFormat.WriteNullable(writer, "pullRequest", filter.PullRequestNumber);
        writer.WriteStartArray("rows");
        foreach (CacheEfficiencyRow row in rows)
        {
            writer.WriteStartObject();
            switch (by)
            {
                case "model":
                    writer.WriteString("model", row.Model);
                    break;
                case "agent":
                    writer.WriteString("agent", row.Agent);
                    break;
                case "branch":
                    writer.WriteString("repository", row.Repository);
                    writer.WriteString("branch", row.Branch);
                    break;
                default:
                    writer.WriteString("repository", row.Repository);
                    LedgerFormat.WriteNullable(writer, "pullRequest", row.PullRequestNumber);
                    LedgerFormat.WriteNullable(writer, "branch", row.Branch);
                    break;
            }

            writer.WriteNumber("spans", row.SpanCount);
            writer.WriteNumber("cacheReportedSpans", row.CacheReportedSpanCount);
            writer.WriteNumber("cacheUnreportedSpans", row.CacheUnreportedSpanCount);
            writer.WriteNumber("inputTokens", row.InputTokens);
            writer.WriteNumber("outputTokens", row.OutputTokens);
            writer.WriteNumber("maxInputTokens", row.MaxInputTokens);
            LedgerFormat.WriteDecimal(writer, "averageInputTokensPerTurn", row.AverageInputTokensPerTurn);
            writer.WriteNumber("reportedInputTokens", row.ReportedInputTokens);
            writer.WriteNumber("reportedCacheReadInputTokens", row.ReportedCacheReadInputTokens);
            writer.WriteNumber("reportedCacheWriteInputTokens", row.ReportedCacheWriteInputTokens);
            LedgerFormat.WriteNullable(writer, "uncachedInputTokens", row.UncachedInputTokens);
            writer.WriteNumber("estimatedUncachedInputTokens", row.EstimatedUncachedInputTokens);
            LedgerFormat.WriteDecimal(writer, "cacheHitRatio", row.CacheHitRatio);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
        context.Out.WriteLine(LedgerFormat.FinishJson(writer, stream));
    }

    private static void WriteTurnsJson(
        CliContext context,
        TurnSelector selector,
        IReadOnlyList<TurnTokenRow> rows)
    {
        using MemoryStream stream = new();
        using Utf8JsonWriter writer = new(stream, JsonOptions);
        writer.WriteStartObject();
        LedgerFormat.WriteNullable(writer, "traceId", selector.TraceId);
        LedgerFormat.WriteNullable(writer, "conversationId", selector.ConversationId);
        writer.WriteStartArray("turns");
        for (int index = 0; index < rows.Count; index++)
        {
            TurnTokenRow row = rows[index];
            writer.WriteStartObject();
            writer.WriteNumber("turn", index + 1);
            writer.WriteString("traceId", row.TraceId);
            writer.WriteString("spanId", row.SpanId);
            LedgerFormat.WriteNullable(writer, "parentSpanId", row.ParentSpanId);
            LedgerFormat.WriteNullable(writer, "conversationId", row.ConversationId);
            LedgerFormat.WriteNullable(writer, "agentSpanId", row.AgentSpanId);
            LedgerFormat.WriteTime(writer, "startTime", row.StartTime);
            writer.WriteString("repository", row.Repository);
            writer.WriteString("branch", row.Branch);
            LedgerFormat.WriteNullable(writer, "pullRequest", row.PullRequestNumber);
            LedgerFormat.WriteNullable(writer, "agent", row.Agent);
            writer.WriteString("model", row.Model);
            writer.WriteNumber("inputTokens", row.InputTokens);
            writer.WriteNumber("outputTokens", row.OutputTokens);
            writer.WriteString("cacheReadState", StateName(row.CacheReadState));
            writer.WriteString("cacheWriteState", StateName(row.CacheWriteState));
            LedgerFormat.WriteNullable(writer, "cacheReadInputTokens", row.CacheReadInputTokens);
            LedgerFormat.WriteNullable(writer, "cacheWriteInputTokens", row.CacheWriteInputTokens);
            LedgerFormat.WriteNullable(writer, "uncachedInputTokens", row.UncachedInputTokens);
            LedgerFormat.WriteDecimal(writer, "cacheHitRatio", row.CacheHitRatio);
            LedgerFormat.WriteNullable(writer, "inputDeltaFromPreviousTurn", row.InputDeltaFromPreviousTurn);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
        context.Out.WriteLine(LedgerFormat.FinishJson(writer, stream));
    }

    private static void WriteBloatJson(
        CliContext context,
        Period period,
        string? repository,
        int limit,
        ContextBloatReport report)
    {
        using MemoryStream stream = new();
        using Utf8JsonWriter writer = new(stream, JsonOptions);
        writer.WriteStartObject();
        writer.WriteString("period", period.Label);
        LedgerFormat.WriteTime(writer, "from", period.From);
        LedgerFormat.WriteNullable(writer, "repository", repository);
        writer.WriteNumber("limit", limit);
        WriteChains(writer, "byUncachedInput", report.ByUncachedInput);
        writer.WriteStartArray("byInputGrowth");
        for (int index = 0; index < report.ByInputGrowth.Count; index++)
        {
            InputGrowthRow row = report.ByInputGrowth[index];
            writer.WriteStartObject();
            writer.WriteNumber("rank", index + 1);
            writer.WriteString("traceId", row.TraceId);
            LedgerFormat.WriteNullable(writer, "conversationId", row.ConversationId);
            writer.WriteString("agentSpanId", row.AgentSpanId);
            LedgerFormat.WriteNullable(writer, "agent", row.Agent);
            writer.WriteString("model", row.Model);
            writer.WriteString("repository", row.Repository);
            writer.WriteString("branch", row.Branch);
            LedgerFormat.WriteTime(writer, "startTime", row.StartTime);
            writer.WriteNumber("turns", row.TurnCount);
            writer.WriteNumber("firstInputTokens", row.FirstInputTokens);
            writer.WriteNumber("peakInputTokens", row.PeakInputTokens);
            writer.WriteNumber("inputGrowthTokens", row.InputGrowthTokens);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        WriteChains(writer, "byEstimatedUncachedInput", report.ByEstimatedUncachedInput);
        writer.WriteNumber("spansWithoutAgent", report.SpansWithoutAgent);
        writer.WriteEndObject();
        context.Out.WriteLine(LedgerFormat.FinishJson(writer, stream));
    }

    private static void WriteChains(
        Utf8JsonWriter writer,
        string name,
        IReadOnlyList<BloatChainRow> rows)
    {
        writer.WriteStartArray(name);
        for (int index = 0; index < rows.Count; index++)
        {
            BloatChainRow row = rows[index];
            writer.WriteStartObject();
            writer.WriteNumber("rank", index + 1);
            writer.WriteString("traceId", row.TraceId);
            LedgerFormat.WriteNullable(writer, "conversationId", row.ConversationId);
            LedgerFormat.WriteTime(writer, "startTime", row.StartTime);
            writer.WriteString("repository", row.Repository);
            writer.WriteString("branch", row.Branch);
            writer.WriteNumber("spans", row.SpanCount);
            writer.WriteNumber("cacheReportedSpans", row.CacheReportedSpanCount);
            writer.WriteNumber("cacheUnreportedSpans", row.SpanCount - row.CacheReportedSpanCount);
            writer.WriteNumber("inputTokens", row.InputTokens);
            writer.WriteNumber("reportedInputTokens", row.ReportedInputTokens);
            writer.WriteNumber("reportedCacheReadInputTokens", row.ReportedCacheReadInputTokens);
            LedgerFormat.WriteNullable(
                writer,
                "uncachedInputTokens",
                row.CacheReportedSpanCount == 0 ? null : row.ReportedUncachedInputTokens);
            writer.WriteNumber("estimatedUncachedInputTokens", row.EstimatedUncachedInputTokens);
            LedgerFormat.WriteDecimal(writer, "cacheHitRatio", row.CacheHitRatio);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
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
            ApiEquivalentUsd = rows.Count > 0 && rows.All(row => row.ApiEquivalentUsd is null)
                ? null
                : rows.Sum(row => row.ApiEquivalentUsd ?? 0m),
            ApiEquivalentUnpricedSpanCount = rows.Sum(row => row.ApiEquivalentUnpricedSpanCount),
            ApiEquivalentEstimatedSpanCount = rows.Sum(row => row.ApiEquivalentEstimatedSpanCount),
        };

    private static string[] MoneyCells(LedgerRollupRow row) =>
    [
        LedgerFormat.Count(row.SpanCount),
        LedgerFormat.Count(row.UnpricedSpanCount),
        LedgerFormat.Credits(row.AiCredits),
        LedgerFormat.Usd(row.AiCreditsUsd),
        LedgerFormat.Usd(row.FoundryByokUsd),
        LedgerFormat.ApiEquivalent(row),
        LedgerFormat.Usd(row.LocalSavedUsd),
    ];

    private static string[] CacheCells(CacheEfficiencyRow row) =>
    [
        LedgerFormat.Count(row.SpanCount),
        LedgerFormat.Count(row.CacheReportedSpanCount),
        LedgerFormat.Count(row.InputTokens),
        Observed(row, row.ReportedCacheReadInputTokens),
        Observed(row, row.ReportedCacheWriteInputTokens),
        Observed(row, row.ReportedUncachedInputTokens),
        LedgerFormat.Count(row.OutputTokens),
        LedgerFormat.Percent(row.CacheHitRatio),
    ];

    // A total over the spans that reported cache usage: n/a when none did, and marked * when only some did.
    private static string Observed(CacheEfficiencyRow row, long value) =>
        row.CacheReportedSpanCount == 0
            ? "n/a"
            : LedgerFormat.Count(value) + (row.CacheUnreportedSpanCount > 0 ? "*" : string.Empty);

    private static string Scope(LedgerFilter filter) =>
        (filter.Repository ?? "all repositories")
        + (filter.Branch is null ? string.Empty : $", branch {filter.Branch}")
        + (filter.PullRequestNumber is int number
            ? ", pull request #" + number.ToString(CultureInfo.InvariantCulture)
            : string.Empty);

    // + and the input added since the sequence's previous request, the decrease, - for its first request, or n/a when
    // the request's agent invocation isn't known.
    private static string Growth(TurnTokenRow row) =>
        row.AgentSpanId is null
            ? "n/a"
            : row.InputDeltaFromPreviousTurn is not long delta
                ? "-"
                : delta > 0
                    ? "+" + LedgerFormat.Count(delta)
                    : LedgerFormat.Count(delta);

    private static string StateName(CacheCounterState state) => state switch
    {
        CacheCounterState.Reported => "reported",
        CacheCounterState.Absent => "absent",
        _ => "legacy_unknown",
    };

    private static string Joined(IReadOnlyList<string> values) =>
        values.Count == 0 ? "-" : string.Join(", ", values);

    private static string Spans(long count) =>
        $"{LedgerFormat.Count(count)} {(count == 1 ? "span" : "spans")}";

    // A blank line and the footnotes for a table's API-equivalent cells: one when a cell has "*", then one when a cell has
    // "~". spans, unpricedSpans, and estimatedSpans are the table's totals, for the spans that scope names.
    private static void WriteApiEquivalentFootnotes(
        CliContext context,
        IReadOnlyList<string> cells,
        long spans,
        long unpricedSpans,
        long estimatedSpans,
        string scope)
    {
        bool partial = cells.Any(cell => cell.EndsWith('*'));
        bool estimate = cells.Any(cell => cell.StartsWith('~'));
        if (!partial && !estimate)
        {
            return;
        }

        context.Out.WriteLine();
        if (partial)
        {
            context.Out.WriteLine(
                $"{LedgerFormat.Count(unpricedSpans)} of {Spans(spans)}{scope} had no API-equivalent price. API-equivalent amounts leave them out; * marks a partial amount.");
        }

        if (estimate)
        {
            context.Out.WriteLine(
                $"{LedgerFormat.Count(estimatedSpans)} priced {(estimatedSpans == 1 ? "span" : "spans")}{scope} reported no cache counters, so all of {(estimatedSpans == 1 ? "its" : "their")} input is priced as uncached; ~ marks an estimate.");
        }
    }

    private static string Requests(long count) =>
        $"{LedgerFormat.Count(count)} {(count == 1 ? "request" : "requests")}";

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

    // The --pricing file: false after writing its error when it's invalid; pricing is null without one.
    private static bool TryLoadPricing(CliContext context, string? file, out WhatIfPricing? pricing)
    {
        pricing = file is null ? null : WhatIfPricing.Load(context, file);
        return file is null || pricing is not null;
    }

    // The line under a report's title that names the --pricing file.
    private static void WritePricingLabel(CliContext context, WhatIfPricing? pricing)
    {
        if (pricing is not null)
        {
            context.Out.WriteLine($"Pricing: {pricing.Label()}, for API-equivalent amounts only");
        }
    }
    // GitHub-billed USD (AI credits at $0.01 each) per USD of the same requests at API list prices. null unless both views
    // priced every one of them and the API-equivalent amount isn't 0.
    private static decimal? BilledToApiRatio(LedgerRollupRow copilot) =>
        copilot.SpanCount > 0
        && copilot.UnpricedSpanCount == 0
        && copilot.ApiEquivalentUnpricedSpanCount == 0
        && copilot.ApiEquivalentUsd is decimal api
        && api != 0m
            ? copilot.AiCredits * AiCreditMath.UsdPerAiCredit / api
            : null;

    // BilledToApiRatio with two decimal places, rounded half away from zero, as 1.25x, after "~" when the API-equivalent amount
    // is an estimate; n/a when there's no ratio.
    private static string BilledToApi(LedgerRollupRow copilot) =>
        BilledToApiRatio(copilot) is decimal ratio
            ? $"{(copilot.ApiEquivalentEstimatedSpanCount > 0 ? "~" : "")}{decimal.Round(ratio, 2, MidpointRounding.AwayFromZero).ToString("0.00", CultureInfo.InvariantCulture)}x"
            : "n/a";

    // A status period's totals: All its spans, the GitHub-billed (Copilot) ones, and the Local ones.
    private sealed record StatusTotals(LedgerRollupRow All, LedgerRollupRow Copilot, LedgerRollupRow Local);
}
