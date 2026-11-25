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
        Option<string?> localLlm = new("--local-llm")
        {
            Description = "Ask a local model on this machine for suggestions: an OpenAI-compatible base URL on localhost, 127.0.0.0/8, or [::1].",
        };
        localLlm.Validators.Add(result =>
        {
            if (!LocalEndpointPolicy.TryValidate(result.GetValueOrDefault<string?>(), out _, out string? error))
            {
                result.AddError($"Invalid --local-llm value: {error}");
            }
        });
        Option<string?> localLlmModel = new("--local-llm-model") { Description = "The local model's name, as its server knows it." };
        localLlmModel.Validators.Add(result =>
        {
            string? value = result.GetValueOrDefault<string?>();
            if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > 128)
            {
                result.AddError("--local-llm-model can't be blank or longer than 128 characters.");
            }
        });
        Option<int?> localLlmTimeout = new("--local-llm-timeout")
        {
            Description = "Seconds to wait for the local model (1 to 600); default: 60.",
        };
        localLlmTimeout.Validators.Add(result =>
        {
            if (result.GetValueOrDefault<int?>() is not (>= 1 and <= 600))
            {
                result.AddError("--local-llm-timeout must be from 1 to 600.");
            }
        });
        Option<bool> showLlmPayload = new("--show-llm-payload")
        {
            Description = "Print the request --local-llm would send, and send nothing.",
        };
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
        command.Add(localLlm);
        command.Add(localLlmModel);
        command.Add(localLlmTimeout);
        command.Add(showLlmPayload);
        command.Validators.Add(result =>
        {
            bool local = result.GetValue(localLlm) is not null;
            if (local && result.GetValue(localLlmModel) is null)
            {
                result.AddError("--local-llm requires --local-llm-model.");
            }

            if (!local && result.GetValue(localLlmModel) is not null)
            {
                result.AddError("--local-llm-model requires --local-llm.");
            }

            if (!local && result.GetValue(localLlmTimeout) is not null)
            {
                result.AddError("--local-llm-timeout requires --local-llm.");
            }

            if (!local && result.GetValue(showLlmPayload))
            {
                result.AddError("--show-llm-payload requires --local-llm.");
            }

            if (local && result.GetValue(json))
            {
                result.AddError("--local-llm can't be used with --json.");
            }

            if (result.GetValue(showLlmPayload) && result.GetValue(failOn) is not null)
            {
                result.AddError("--fail-on can't be used with --show-llm-payload.");
            }
        });
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
                    LocalLlm = LocalEndpointPolicy.TryValidate(result.GetValue(localLlm), out LocalEndpoint? endpoint, out _) ? endpoint : null,
                    LocalLlmModel = result.GetValue(localLlmModel)?.Trim(),
                    LocalLlmTimeoutSeconds = result.GetValue(localLlmTimeout) ?? 60,
                    ShowLlmPayload = result.GetValue(showLlmPayload),
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
        IReadOnlyDictionary<string, string> toolNames;
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
            toolNames = await snapshot.GetMcpToolNamesAsync(cancellationToken);
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
            request.Limit).WithToolNames(toolNames);
        byte[]? body = request.LocalLlm is null ? null : AuditLlmPayload.Body(report, request.LocalLlmModel!);
        if (request.ShowLlmPayload)
        {
            if (body is null)
            {
                context.Error.WriteLine(AuditLlmPayload.TooLargeWarning);
            }
            else
            {
                context.Out.Write(Encoding.UTF8.GetString(body) + "\n");
            }

            return 0;
        }

        if (request.Json)
        {
            context.Out.Write(Encoding.UTF8.GetString(AuditJson.Serialize(report)));
        }
        else
        {
            AuditTable.Write(context.Out, report, context.TimeProvider.LocalTimeZone);
        }

        if (request.LocalLlm is not null && body is null)
        {
            context.Error.WriteLine(AuditLlmPayload.TooLargeWarning);
        }
        else if (request.LocalLlm is LocalEndpoint endpoint && body is not null)
        {
            LocalLlmAnswer answer = await LocalLlmAdvisor.AskAsync(
                context.CreateLocalLlmHandler?.Invoke() ?? LocalLlmAdvisor.CreateDefaultHandler(),
                endpoint,
                body,
                request.LocalLlmTimeoutSeconds,
                cancellationToken);
            if (answer.Suggestions is not null)
            {
                context.Out.WriteLine();
                context.Out.WriteLine(
                    $"Local model suggestions ({AdvisoryText.Label(request.LocalLlmModel, 128)}, advisory, not stored)");
                context.Out.WriteLine(answer.Suggestions);
            }
            else
            {
                context.Error.WriteLine(answer.Warning);
            }
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
        public LocalEndpoint? LocalLlm { get; init; }
        public string? LocalLlmModel { get; init; }
        public required int LocalLlmTimeoutSeconds { get; init; }
        public required bool ShowLlmPayload { get; init; }
    }
}
