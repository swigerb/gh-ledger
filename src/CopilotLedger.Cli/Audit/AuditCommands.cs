using System.CommandLine;
using System.CommandLine.Parsing;
using System.Text;
using CopilotLedger.Cli.Rates;
using CopilotLedger.Cli.Reporting;
using CopilotLedger.Core.Audit;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Storage;

namespace CopilotLedger.Cli.Audit;

// gh ledger audit: ranked review signals of wasted AI spend, computed from the ledger's metadata in one read snapshot.
internal static class AuditCommands
{
    public const int DefaultLimit = 20;
    public const int MaxLimit = 200;

    public static Command Create(CliContext context)
    {
        Command command = new(
            "audit",
            "Find signs of wasted AI spend: request bursts, tool loops, retries, context bloat, cache misses, and premium models on trivial requests.");
        Option<string?> repository = new("--repo") { Description = "Only this repository key; default: every repository." };
        Option<string?> branch = new("--branch") { Description = "Only this branch, or (none)." };
        Option<int?> pullRequest = new("--pr") { Description = "Only this pull request number." };
        pullRequest.Validators.Add(result =>
        {
            if (result.GetValueOrDefault<int?>() is < 1)
            {
                result.AddError("--pr must be a positive integer.");
            }
        });
        Option<string> since = new("--since")
        {
            Description = "Period: today, all, Nd, or yyyy-MM-dd.",
            DefaultValueFactory = _ => "7d",
        };
        since.Validators.Add(result =>
        {
            string value = result.GetValueOrDefault<string>() ?? string.Empty;
            if (!Period.TryParse(value, context, out _))
            {
                result.AddError($"Invalid --since value {value}. Use today, all, <N>d (1-3650), or yyyy-MM-dd.");
            }
        });
        Option<string[]> rules = new("--rule") { Description = "Only this rule; repeat it for more. Default: every rule." };
        rules.Validators.Add(result =>
        {
            if ((result.GetValueOrDefault<string[]>() ?? []).Any(value => !AuditRuleIds.IsKnown(value)))
            {
                result.AddError($"--rule must be {string.Join(", ", AuditRuleIds.All.SkipLast(1))}, or {AuditRuleIds.All[^1]}.");
            }
        });
        Option<string> minSeverity = new("--min-severity")
        {
            Description = "Leave out findings below this severity: info, warning, or high.",
            DefaultValueFactory = _ => "info",
        };
        minSeverity.Validators.Add(result =>
        {
            if (!AuditSeverities.TryParse(result.GetValueOrDefault<string>(), out _))
            {
                result.AddError("--min-severity must be info, warning, or high.");
            }
        });
        Option<int> limit = new("--limit")
        {
            Description = "Most findings to show (1 to 200).",
            DefaultValueFactory = _ => DefaultLimit,
        };
        limit.Validators.Add(result =>
        {
            if (result.GetValueOrDefault<int>() is < 1 or > MaxLimit)
            {
                result.AddError("--limit must be from 1 to 200.");
            }
        });
        Option<string?> economyModel = new("--economy-model")
        {
            Description = "The GitHub AI credits model premium-model impacts compare with; default: the rates' local baseline.",
        };
        economyModel.Validators.Add(result =>
        {
            if (string.IsNullOrWhiteSpace(result.GetValueOrDefault<string?>()))
            {
                result.AddError("--economy-model can't be blank.");
            }
        });
        Option<string?> failOn = new("--fail-on")
        {
            Description = "Exit with code 2 when a finding is at or above this severity: warning or high.",
        };
        failOn.Validators.Add(result =>
        {
            if (result.GetValueOrDefault<string?>() is not ("warning" or "high"))
            {
                result.AddError("--fail-on must be warning or high.");
            }
        });
        Option<bool> json = new("--json") { Description = "Write JSON instead of a table." };
        command.Add(repository);
        command.Add(branch);
        command.Add(pullRequest);
        command.Add(since);
        command.Add(rules);
        command.Add(minSeverity);
        command.Add(limit);
        command.Add(economyModel);
        command.Add(failOn);
        command.Add(json);
        command.SetAction((ParseResult result, CancellationToken cancellationToken) =>
        {
            string[] selected = result.GetValue(rules) ?? [];
            return RunAsync(
                context,
                new AuditRequest
                {
                    Repository = result.GetValue(repository),
                    Branch = result.GetValue(branch),
                    PullRequestNumber = result.GetValue(pullRequest),
                    Since = result.GetValue(since)!.Trim(),
                    Rules = selected.Length == 0 ? AuditRuleIds.All : [.. selected.Distinct(StringComparer.Ordinal)],
                    MinSeverity = AuditSeverities.TryParse(result.GetValue(minSeverity), out AuditSeverity severity)
                        ? severity
                        : throw new InvalidOperationException(),
                    Limit = result.GetValue(limit),
                    EconomyModel = result.GetValue(economyModel),
                    FailOn = AuditSeverities.TryParse(result.GetValue(failOn), out AuditSeverity threshold) ? threshold : null,
                    Json = result.GetValue(json),
                },
                cancellationToken);
        });
        return command;
    }

    private static async Task<int> RunAsync(CliContext context, AuditRequest request, CancellationToken cancellationToken)
    {
        RatesBase rates = RateCardFiles.LoadBase(context);
        if (rates.Warning is not null)
        {
            context.Error.WriteLine($"Warning: {rates.Warning}");
        }

        JsonRateCardCalculator card;
        try
        {
            card = RateCardFiles.Load(rates, RateCardFiles.OverlayPath(context));
        }
        catch (InvalidDataException ex)
        {
            context.Error.WriteLine($"Invalid rates overlay: {ex.Message}");
            return 1;
        }

        string? economyModel = request.EconomyModel is null ? card.BaselineModel : card.FindGitHubModel(request.EconomyModel);
        if (economyModel is null)
        {
            context.Error.WriteLine(
                $"--economy-model {request.EconomyModel!.Trim()} isn't a GitHub AI credits model in the rates in use ({card.RatesVersion}).");
            return 1;
        }

        DateTimeOffset generatedAt = DateTimeOffset.FromUnixTimeMilliseconds(context.TimeProvider.GetUtcNow().ToUnixTimeMilliseconds());
        Period period = Period.TryParse(request.Since, context, out Period value) ? value : throw new InvalidOperationException();
        SqliteLedgerStore store = new(context.DatabasePath, context.TimeProvider);
        await store.InitializeAsync(cancellationToken);
        AuditInput input;
        await using (LedgerExportSnapshot snapshot = await store.OpenExportSnapshotAsync(cancellationToken))
        {
            input = await snapshot.GetAuditInputAsync(
                new LedgerFilter
                {
                    From = period.From,
                    To = generatedAt,
                    Repository = request.Repository,
                    Branch = request.Branch,
                    PullRequestNumber = request.PullRequestNumber,
                },
                generatedAt,
                cancellationToken);
        }

        AuditReport report = AuditAnalyzer.Report(
            input with
            {
                Pricing = card,
                EconomyModel = economyModel,
                TimeZone = context.TimeProvider.LocalTimeZone,
                Rules = request.Rules,
            },
            generatedAt,
            new AuditReportFilter
            {
                Repository = request.Repository,
                Branch = request.Branch,
                PullRequestNumber = request.PullRequestNumber,
                Since = request.Since,
                Period = period.Label,
                FromUtc = period.From,
                ToUtc = generatedAt,
            },
            request.MinSeverity,
            request.Limit);
        if (request.Json)
        {
            context.Out.Write(Encoding.UTF8.GetString(AuditJson.Serialize(report)));
        }
        else
        {
            AuditTable.Write(context.Out, report, context.TimeProvider.LocalTimeZone);
        }

        return request.FailOn is AuditSeverity failOn && report.Findings.Any(finding => finding.Severity >= failOn) ? 2 : 0;
    }

    // The command's options, validated.
    private sealed record AuditRequest
    {
        public string? Repository { get; init; }
        public string? Branch { get; init; }
        public int? PullRequestNumber { get; init; }
        public required string Since { get; init; }
        public required IReadOnlyList<string> Rules { get; init; }
        public required AuditSeverity MinSeverity { get; init; }
        public required int Limit { get; init; }
        public string? EconomyModel { get; init; }
        public AuditSeverity? FailOn { get; init; }
        public required bool Json { get; init; }
    }
}
