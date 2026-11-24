using System.CommandLine;
using System.CommandLine.Parsing;
using System.Globalization;
using CopilotLedger.Core.Git;
using CopilotLedger.Storage;

namespace CopilotLedger.Cli.Export;

// gh ledger export: an offline HTML report of the ledger, written from one read snapshot.
internal static class ExportCommands
{
    public static Command Create(CliContext context)
    {
        Command command = new("export", "Write an offline HTML cost report, and optionally a bundle of its data.");
        Option<string> format = new("--format")
        {
            Description = "Report format: html.",
            DefaultValueFactory = _ => "html",
        };
        format.Validators.Add(result =>
        {
            if (result.GetValueOrDefault<string>() != "html")
            {
                result.AddError("--format must be html.");
            }
        });
        Option<string> output = new("--out")
        {
            Description = "The report file (.html), relative to the current directory.",
            DefaultValueFactory = _ => ExportLimits.DefaultOut,
        };
        output.Validators.Add(result =>
        {
            string value = result.GetValueOrDefault<string>() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(value))
            {
                result.AddError("--out can't be blank.");
            }
            else if (!value.EndsWith(".html", StringComparison.OrdinalIgnoreCase) &&
                     !value.EndsWith(".htm", StringComparison.OrdinalIgnoreCase))
            {
                result.AddError("--out must end with .html or .htm.");
            }
        });
        Option<string?> repository = new("--repo") { Description = "Limit the report to this repository key." };
        Option<bool> allRepositories = new("--all-repos") { Description = "Report every repository." };
        Option<string?> branch = new("--branch") { Description = "Limit the report to this branch, or (none)." };
        Option<int?> pullRequest = new("--pr") { Description = "Limit the report to this pull request number." };
        pullRequest.Validators.Add(result =>
        {
            if (result.GetValueOrDefault<int?>() is < 1)
            {
                result.AddError("--pr must be a positive integer.");
            }
        });
        Option<string?> conversation = new("--conversation") { Description = "Limit the report to one Copilot session." };
        conversation.Validators.Add(result =>
        {
            if (string.IsNullOrWhiteSpace(result.GetValueOrDefault<string?>()))
            {
                result.AddError("--conversation can't be blank.");
            }
        });
        Option<string> since = new("--since")
        {
            Description = "Period: today, all, Nd, or yyyy-MM-dd.",
            DefaultValueFactory = _ => "30d",
        };
        since.Validators.Add(result =>
        {
            string value = result.GetValueOrDefault<string>() ?? string.Empty;
            if (!LedgerPeriod.TryParse(value, context.TimeProvider, out _))
            {
                result.AddError($"Invalid --since value {value}. Use today, all, <N>d (1-3650), or yyyy-MM-dd.");
            }
        });
        Option<string?> timeZone = new("--time-zone")
        {
            Description = "IANA time zone for dates, times, and night shading; default: this computer's.",
        };
        timeZone.Validators.Add(result =>
        {
            string value = result.GetValueOrDefault<string?>() ?? string.Empty;
            if (context.Platform.FindTimeZone(value) is null)
            {
                result.AddError($"Invalid --time-zone value {value}. Use an IANA time zone ID, such as America/New_York.");
            }
        });
        Option<bool> includeLocalPaths = new("--include-local-paths")
        {
            Description = "Show local repository paths, which are left out by default.",
        };
        Option<int> maxTimelineSpans = new("--max-timeline-spans")
        {
            Description = "Most requests, and tool calls, to draw on the timeline (0 to 100000).",
            DefaultValueFactory = _ => ExportLimits.DefaultMaxTimelineSpans,
        };
        maxTimelineSpans.Validators.Add(result =>
        {
            if (result.GetValueOrDefault<int>() is < 0 or > ExportLimits.MaxTimelineSpans)
            {
                result.AddError("--max-timeline-spans must be from 0 to 100000.");
            }
        });
        Option<string?> generatedAt = new("--generated-at")
        {
            Description = "The generation time to record, in UTC, to reproduce an earlier export.",
        };
        generatedAt.Validators.Add(result =>
        {
            string? value = result.GetValueOrDefault<string?>();
            if (!ExportTime.TryParseGeneratedAt(value, out _))
            {
                result.AddError($"Invalid --generated-at value {value}. Use a UTC time such as 2026-11-21T21:30:00Z.");
            }
        });
        Option<bool> force = new("--force") { Description = "Replace existing files." };
        command.Add(format);
        command.Add(output);
        command.Add(repository);
        command.Add(allRepositories);
        command.Add(branch);
        command.Add(pullRequest);
        command.Add(conversation);
        command.Add(since);
        command.Add(timeZone);
        command.Add(includeLocalPaths);
        command.Add(maxTimelineSpans);
        command.Add(generatedAt);
        command.Add(force);
        command.Validators.Add(result =>
        {
            if (result.GetValue(repository) is not null && result.GetValue(allRepositories))
            {
                result.AddError("Use either --repo or --all-repos, not both.");
            }
        });
        command.SetAction(
            (ParseResult result, CancellationToken cancellationToken) =>
                ExportAsync(
                    context,
                    Resolve(
                        context,
                        result.GetValue(generatedAt),
                        result.GetValue(timeZone),
                        result.GetValue(since)!,
                        result.GetValue(allRepositories) ? null : result.GetValue(repository),
                        result.GetValue(allRepositories),
                        new LedgerFilter
                        {
                            Branch = result.GetValue(branch),
                            PullRequestNumber = result.GetValue(pullRequest),
                            ConversationId = result.GetValue(conversation),
                        },
                        result.GetValue(maxTimelineSpans),
                        result.GetValue(includeLocalPaths)),
                    result.GetValue(output)!,
                    result.GetValue(force),
                    cancellationToken));
        return command;
    }

    // The options with generatedAt, the time zone, the period, and the repository resolved. Without --repo or --all-repos,
    // the working directory's repository (its slug, else its root), or every repository outside one.
    private static ExportInputs Resolve(
        CliContext context,
        string? generatedAtText,
        string? timeZoneId,
        string since,
        string? repository,
        bool allRepositories,
        LedgerFilter keys,
        int maxTimelineSpans,
        bool includeLocalPaths)
    {
        DateTimeOffset generatedAt = generatedAtText is null
            ? ExportTime.TruncateToMilliseconds(context.TimeProvider.GetUtcNow())
            : ExportTime.TryParseGeneratedAt(generatedAtText, out DateTimeOffset parsed)
                ? parsed
                : throw new InvalidOperationException();
        TimeZoneInfo zone = timeZoneId is null
            ? context.TimeProvider.LocalTimeZone
            : context.Platform.FindTimeZone(timeZoneId) ?? throw new InvalidOperationException();
        LedgerPeriod period = LedgerPeriod.TryParse(since, new ExportClock(generatedAt, zone), out LedgerPeriod? value)
            ? value
            : throw new InvalidOperationException();
        if (!allRepositories && repository is null)
        {
            GitWorkspaceContext? git = new FileSystemGitContextResolver().Resolve(context.WorkingDirectory);
            repository = git?.RepositorySlug ?? git?.RepositoryRoot;
        }

        return new ExportInputs
        {
            GeneratedAt = generatedAt,
            TimeZone = zone,
            RequestedTimeZone = timeZoneId,
            Since = since.Trim(),
            Period = period,
            Filter = keys with { From = period.From, To = generatedAt, Repository = repository },
            AllRepositories = repository is null,
            MaxTimelineSpans = maxTimelineSpans,
            IncludeLocalPaths = includeLocalPaths,
        };
    }

    private static async Task<int> ExportAsync(
        CliContext context,
        ExportInputs inputs,
        string output,
        bool force,
        CancellationToken cancellationToken)
    {
        string htmlPath = Path.GetFullPath(output, context.WorkingDirectory);
        if (!CanWrite(context, htmlPath, output, force))
        {
            return 1;
        }

        SqliteLedgerStore store = new(context.DatabasePath, context.TimeProvider);
        await store.InitializeAsync(cancellationToken);
        ExportReportModel model;
        byte[] html;
        await using (LedgerExportSnapshot snapshot = await store.OpenExportSnapshotAsync(cancellationToken))
        {
            model = await ExportModelBuilder.BuildAsync(snapshot, inputs, cancellationToken);
            html = HtmlReportWriter.Write(model);
        }

        string htmlTemp = TemporaryPath(htmlPath);
        try
        {
            await File.WriteAllBytesAsync(htmlTemp, html, cancellationToken);
            File.Move(htmlTemp, htmlPath, overwrite: force);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            context.Error.WriteLine($"Could not write {output}: {ex.Message}");
            return 1;
        }
        finally
        {
            DeleteIfPresent(htmlTemp);
        }

        context.Out.WriteLine(
            $"Wrote {Path.GetFileName(htmlPath)}: {ExportRecords.Plural(model.Summary.Totals.SpanCount, "request", "requests")} in {model.Header.Scope} ({model.Header.Filter.Period}).");
        if (model.Coverage.TimelineTruncated)
        {
            context.Out.WriteLine(ExportRecords.TimelineTruncation(model.Coverage));
        }

        return 0;
    }

    // False, after writing why, when path is a directory, or a file that --force doesn't allow replacing.
    private static bool CanWrite(CliContext context, string path, string shown, bool force)
    {
        if (Directory.Exists(path))
        {
            context.Error.WriteLine($"{shown} is a directory.");
            return false;
        }

        if (!force && File.Exists(path))
        {
            context.Error.WriteLine($"{shown} already exists. Use --force to replace it.");
            return false;
        }

        return true;
    }

    // The temporary file beside target that a file is written to before it's moved into place.
    private static string TemporaryPath(string target) =>
        $"{target}.{Environment.ProcessId.ToString(CultureInfo.InvariantCulture)}.tmp";

    private static void DeleteIfPresent(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    // LedgerPeriod's clock for the export: generatedAt, in the export's time zone.
    private sealed class ExportClock(DateTimeOffset utcNow, TimeZoneInfo zone) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;

        public override TimeZoneInfo LocalTimeZone => zone;
    }
}
