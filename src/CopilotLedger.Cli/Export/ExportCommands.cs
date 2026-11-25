using System.CommandLine;
using System.CommandLine.Parsing;
using System.Globalization;
using CopilotLedger.Cli.Rates;
using CopilotLedger.Core.Audit;
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
        Option<bool> bundle = new("--bundle")
        {
            Description = "Also write a ZIP with the report, a manifest, and its data as JSON, beside the report.",
        };
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
        command.Add(bundle);
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
                    result.GetValue(bundle),
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
        bool bundle,
        bool force,
        CancellationToken cancellationToken)
    {
        string htmlPath = Path.GetFullPath(output, context.WorkingDirectory);
        string? zipPath = bundle ? Path.ChangeExtension(htmlPath, ".zip") : null;
        string zipShown = Path.ChangeExtension(output, ".zip");
        if (!CanWrite(context, htmlPath, output, force) || (zipPath is not null && !CanWrite(context, zipPath, zipShown, force)))
        {
            return 1;
        }

        SqliteLedgerStore store = new(context.DatabasePath, context.TimeProvider);
        await store.InitializeAsync(cancellationToken);
        inputs = inputs with { AuditPricing = LoadAuditPricing(context) };
        string htmlTemp = TemporaryPath(htmlPath);
        string? zipTemp = zipPath is null ? null : TemporaryPath(zipPath);
        string? zipBackup = null;
        bool zipMoved = false;
        string writing = output;
        ExportReportModel model;
        try
        {
            // Both files are written from one snapshot, then moved into place: the ZIP first, then the report. Without --force,
            // no move replaces a file, so one that appeared since CanWrite is refused. With it, an existing ZIP waits beside the
            // new one until the report is in place, so a failed export leaves both as they were.
            await using (LedgerExportSnapshot snapshot = await store.OpenExportSnapshotAsync(cancellationToken))
            {
                model = await ExportModelBuilder.BuildAsync(snapshot, inputs, cancellationToken);
                byte[] html = HtmlReportWriter.Write(model);
                await context.Platform.WriteFileAsync(htmlTemp, html, cancellationToken);
                if (zipTemp is not null)
                {
                    writing = zipShown;
                    await ExportBundleWriter.WriteAsync(
                        zipTemp,
                        BundleEntries(snapshot, inputs, model, html),
                        hashes => ExportJson.Manifest(model, hashes),
                        cancellationToken);
                }
            }

            if (zipTemp is not null)
            {
                writing = zipShown;
                if (force && File.Exists(zipPath))
                {
                    string backup = BackupPath(zipPath!);
                    context.Platform.MoveFile(zipPath!, backup, overwrite: false);
                    zipBackup = backup;
                }

                context.Platform.MoveFile(zipTemp, zipPath!, overwrite: false);
                zipMoved = true;
            }

            writing = output;
            context.Platform.MoveFile(htmlTemp, htmlPath, overwrite: force);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            context.Error.WriteLine(!force && ex is IOException && File.Exists(writing == output ? htmlPath : zipPath)
                ? $"{writing} already exists. Use --force to replace it."
                : $"Could not write {writing}: {WriteFailure(ex)}");
            if (zipPath is not null && RestoreZip(context.Platform, zipPath, zipShown, output, zipBackup, zipMoved) is string left)
            {
                context.Error.WriteLine(left);
            }

            return 1;
        }
        finally
        {
            DeleteIfPresent(htmlTemp);
            if (zipTemp is not null)
            {
                DeleteIfPresent(zipTemp);
            }
        }

        // The report is in place, so the replaced ZIP goes; an export that has to leave it behind doesn't succeed.
        if (zipBackup is not null)
        {
            try
            {
                context.Platform.DeleteFile(zipBackup);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                context.Error.WriteLine(
                    $"{output} and {zipShown} were written, but the replaced {zipShown} is kept as {Path.GetFileName(zipBackup)}: it couldn't be deleted.");
                return 1;
            }
        }

        context.Out.WriteLine(
            $"Wrote {Path.GetFileName(htmlPath)}: {ExportRecords.Plural(model.Summary.Totals.SpanCount, "request", "requests")} in {model.Header.Scope} ({model.Header.Filter.Period}).");
        if (zipPath is not null)
        {
            context.Out.WriteLine($"Wrote {Path.GetFileName(zipPath)}: report.html, manifest.json, and 5 telemetry files.");
        }

        if (model.Coverage.TimelineTruncated)
        {
            context.Out.WriteLine(ExportRecords.TimelineTruncation(model.Coverage));
        }

        return 0;
    }

    // The bundle's files besides manifest.json. The report is the same bytes as the .html file.
    private static IReadOnlyList<ExportBundleEntry> BundleEntries(
        LedgerExportSnapshot snapshot,
        ExportInputs inputs,
        ExportReportModel model,
        byte[] html)
    {
        byte[] chains = ExportJson.PromptChains(model);
        byte[] summary = ExportJson.Summary(model);
        byte[] audit = ExportJson.AuditFindings(model);
        return
        [
            new("report.html", (stream, token) => stream.WriteAsync(html, token).AsTask()),
            new("telemetry/audit-findings.json", (stream, token) => stream.WriteAsync(audit, token).AsTask()),
            new("telemetry/prompt-chains.json", (stream, token) => stream.WriteAsync(chains, token).AsTask()),
            new("telemetry/spans.json", (stream, token) => ExportJson.WriteSpansAsync(stream, snapshot, inputs, token)),
            new("telemetry/summary.json", (stream, token) => stream.WriteAsync(summary, token).AsTask()),
            new(
                "telemetry/tool-calls.json",
                (stream, token) => ExportJson.WriteToolCallsAsync(stream, snapshot, inputs, model.ToolNames, token)),
        ];
    }

    // The rates in use, loaded as import loads them, for the audit findings. An invalid overlay doesn't stop the export: it
    // warns, and the findings leave out premium_model_trivial_turn and repriced estimates.
    private static IAuditPricing? LoadAuditPricing(CliContext context)
    {
        try
        {
            return RateCardFiles.Load(RateCardFiles.LoadBase(context), RateCardFiles.OverlayPath(context));
        }
        catch (InvalidDataException ex)
        {
            context.Error.WriteLine(
                $"Warning: invalid rates overlay, so the report's audit findings leave out premium_model_trivial_turn and repriced estimates: {ex.Message}");
            return null;
        }
    }

    // False, after writing why, when path is a directory, a file that --force doesn't allow replacing, or a read-only file,
    // which even --force doesn't replace.
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

        if (File.Exists(path) && IsReadOnly(path))
        {
            context.Error.WriteLine($"Could not write {shown}: access is denied.");
            return false;
        }

        return true;
    }

    // Whether path is read-only (on Unix, a file that this user can read but not write); false when it can't be examined.
    private static bool IsReadOnly(string path)
    {
        try
        {
            return File.GetAttributes(path).HasFlag(FileAttributes.ReadOnly);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    // The temporary file beside target that a file is written to before it's moved into place.
    private static string TemporaryPath(string target) =>
        $"{target}.{Environment.ProcessId.ToString(CultureInfo.InvariantCulture)}.tmp";

    // Where a ZIP that --force replaces waits until the report is in place.
    private static string BackupPath(string target) =>
        $"{target}.{Environment.ProcessId.ToString(CultureInfo.InvariantCulture)}.bak";

    // Why a file couldn't be written, without the path that the exception's message names.
    private static string WriteFailure(Exception ex) => ex switch
    {
        DirectoryNotFoundException => "its folder doesn't exist.",
        UnauthorizedAccessException => "access is denied.",
        _ => $"the file system reported error 0x{ex.HResult.ToString("X8", CultureInfo.InvariantCulture)}.",
    };

    // Puts the ZIP back as it was before the export: the one --force replaced, or none. Returns a line for standard error when
    // it can't, so what's left is said, or null.
    private static string? RestoreZip(
        ICliPlatform platform,
        string zipPath,
        string zipShown,
        string output,
        string? backup,
        bool moved)
    {
        try
        {
            if (backup is not null)
            {
                platform.MoveFile(backup, zipPath, overwrite: true);
            }
            else if (moved)
            {
                platform.DeleteFile(zipPath);
            }

            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            string kept = backup is null ? string.Empty : $"The replaced {zipShown} is kept as {Path.GetFileName(backup)}.";
            return moved ? $"{zipShown} was written, but {output} wasn't, so they don't match. {kept}".TrimEnd() : kept;
        }
    }

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
