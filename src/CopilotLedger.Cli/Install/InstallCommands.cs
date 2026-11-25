using System.CommandLine;
using System.CommandLine.Parsing;
using System.Security;
using System.Text;
using System.Text.Json;
using CopilotLedger.Cli.Daemon;

namespace CopilotLedger.Cli.Install;

internal static class InstallCommands
{
    private static readonly JsonSerializerOptions ManifestOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static IEnumerable<Command> Create(CliContext context)
    {
        yield return CreateInstall(context);
        yield return CreateUninstall(context);
    }

    private static Command CreateInstall(CliContext context)
    {
        Command command = new("install", "Enable Copilot export and install gh integration.");
        Option<bool> dryRun = DryRunOption();
        command.Add(dryRun);
        command.SetAction(
            (ParseResult result, CancellationToken cancellationToken) =>
                InstallAsync(context, result.GetValue(dryRun), cancellationToken));
        return command;
    }

    private static Command CreateUninstall(CliContext context)
    {
        Command command = new("uninstall", "Undo changes made by install.");
        Option<bool> dryRun = DryRunOption();
        command.Add(dryRun);
        command.SetAction(
            (ParseResult result, CancellationToken cancellationToken) =>
                UninstallAsync(context, result.GetValue(dryRun), cancellationToken));
        return command;
    }

    private static async Task<int> InstallAsync(
        CliContext context,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        string? executable = context.Platform.ProcessPath;
        if (!ExecutableName(executable).Equals("gh-ledger", StringComparison.OrdinalIgnoreCase))
        {
            context.Error.WriteLine(
                $"Run install from the gh-ledger executable (current process: {executable ?? "unknown"}).");
            return 1;
        }

        Func<string, string?> lookup = name =>
            context.Platform.GetUserEnvironmentVariable(name) ??
            context.Platform.GetEnvironmentVariable(name);
        foreach (string warning in CopilotOtelEnvironment.GetWarnings(lookup))
        {
            context.Out.WriteLine($"Warning: {warning}");
        }

        string manifestPath = ManifestPath(context);
        InstallManifest? previous = await TryReadManifestAsync(
            manifestPath,
            cancellationToken);
        bool variableSet = previous?.WindowsUserVariableSet == true;
        string? previousVariableValue = previous?.WindowsUserVariablePreviousValue;
        HashSet<string> profiles = previous is null
            ? []
            : [.. previous.ProfileFiles];
        bool ghAliasSet = previous?.GhAliasSet == true;

        if (context.Platform.IsWindows)
        {
            string? current =
                context.Platform.GetUserEnvironmentVariable("COPILOT_OTEL_ENABLED");
            previousVariableValue = previous?.WindowsUserVariableSet == true
                ? previous.WindowsUserVariablePreviousValue
                : current;
            if (IsTrue(current))
            {
                context.Out.WriteLine(
                    "COPILOT_OTEL_ENABLED=true is already set for your user account.");
            }
            else
            {
                if (dryRun)
                {
                    context.Out.WriteLine(
                        "Would set COPILOT_OTEL_ENABLED=true for your user account.");
                }
                else
                {
                    context.Platform.SetUserEnvironmentVariable(
                        "COPILOT_OTEL_ENABLED",
                        "true");
                    context.Out.WriteLine(
                        "Set COPILOT_OTEL_ENABLED=true for your user account.");
                }

                variableSet = true;
            }
        }
        else
        {
            string profile = ProfilePath(context);
            profiles.Add(profile);
            string? contents = File.Exists(profile)
                ? await File.ReadAllTextAsync(profile, cancellationToken)
                : null;
            if (contents?.Split('\n').Any(
                    line => line.TrimEnd('\r') == "# >>> copilot-ledger >>>") == true)
            {
                context.Out.WriteLine($"{profile} already sets COPILOT_OTEL_ENABLED.");
            }
            else
            {
                if (dryRun)
                {
                    context.Out.WriteLine(
                        $"Would add COPILOT_OTEL_ENABLED=true to {profile}.");
                }
                else
                {
                    Directory.CreateDirectory(
                        Path.GetDirectoryName(profile)!);
                    string separator = string.IsNullOrEmpty(contents)
                        ? string.Empty
                        : contents.EndsWith('\n') ? "\n" : "\n\n";
                    const string block =
                        "# >>> copilot-ledger >>>\n" +
                        "export COPILOT_OTEL_ENABLED=true\n" +
                        "# <<< copilot-ledger <<<\n";
                    await File.AppendAllTextAsync(
                        profile,
                        separator + block,
                        cancellationToken);
                    context.Out.WriteLine(
                        $"Added COPILOT_OTEL_ENABLED=true to {profile}.");
                }
            }
        }

        bool extension = GhExtension.IsInstalled(context.Platform);
        string? gh = context.Platform.FindOnPath("gh");
        if (extension)
        {
            context.Out.WriteLine(
                "gh ledger runs this GitHub CLI extension, so no alias is needed.");
        }
        else if (gh is null)
        {
            context.Out.WriteLine(
                "GitHub CLI (gh) was not found, so the gh ledger alias was not set. Run gh-ledger directly.");
        }
        else if (dryRun)
        {
            context.Out.WriteLine(
                $"Would set gh alias: gh ledger runs {executable}.");
        }
        else
        {
            string target = context.Platform.IsWindows
                ? executable!.Replace('\\', '/')
                : executable!;
            string expansion = Quote(target) + " \"$@\"";
            ProcessRunResult result = await context.Platform.RunAsync(
                gh,
                ["alias", "set", "ledger", expansion, "--shell", "--clobber"],
                cancellationToken);
            if (result.ExitCode == 0)
            {
                ghAliasSet = true;
                context.Out.WriteLine(
                    $"Set gh alias: gh ledger runs {executable}.");
            }
            else
            {
                string error = result.StandardError.Trim();
                context.Out.WriteLine(
                    $"Warning: could not set the gh alias: {(error.Length == 0
                        ? $"exit code {result.ExitCode}"
                        : error)}");
            }
        }

        if (!extension && gh is not null && context.Platform.IsWindows && FindSh(context) is null)
        {
            context.Out.WriteLine(
                "Warning: GitHub CLI needs sh.exe from Git for Windows to run shell aliases, so gh ledger won't work until Git for Windows is installed. Run gh-ledger directly.");
        }

        string? startupItem = previous?.StartupItem;
        bool signInItemPlanned = false;
        if (!string.IsNullOrWhiteSpace(
                context.Platform.GetEnvironmentVariable("COPILOT_LEDGER_HOME")))
        {
            context.Out.WriteLine(
                "Warning: COPILOT_LEDGER_HOME is set. The daemon that starts when you sign in uses it only if your sign-in environment sets it too.");
        }

        StartupItemPlan planned = StartupItem.Plan(context, executable!);
        if (planned.SkipWarning is not null)
        {
            context.Out.WriteLine(planned.SkipWarning);
        }
        else
        {
            try
            {
                bool alreadyPresent;
                if (context.Platform.IsWindows)
                {
                    alreadyPresent = string.Equals(
                        context.Platform.GetStartupCommand("CopilotLedger"),
                        planned.Content,
                        StringComparison.Ordinal);
                }
                else
                {
                    alreadyPresent = File.Exists(planned.Identifier)
                        && string.Equals(
                            await File.ReadAllTextAsync(
                                planned.Identifier,
                                cancellationToken),
                            planned.Content,
                            StringComparison.Ordinal);
                }

                if (alreadyPresent)
                {
                    context.Out.WriteLine("gh-ledger already starts when you sign in.");
                }
                else if (dryRun)
                {
                    context.Out.WriteLine("Would start gh-ledger when you sign in.");
                    signInItemPlanned = context.Platform.IsWindows;
                }
                else
                {
                    if (context.Platform.IsWindows)
                    {
                        context.Platform.SetStartupCommand(
                            "CopilotLedger",
                            planned.Content);
                    }
                    else
                    {
                        Directory.CreateDirectory(
                            Path.GetDirectoryName(planned.Identifier)!);
                        await File.WriteAllTextAsync(
                            planned.Identifier,
                            planned.Content,
                            new UTF8Encoding(false),
                            cancellationToken);
                    }

                    context.Out.WriteLine("gh-ledger will start when you sign in.");
                }

                if (!dryRun)
                {
                    startupItem = planned.Identifier;
                }
            }
            catch (Exception ex) when (
                ex is IOException
                or UnauthorizedAccessException
                or SecurityException)
            {
                context.Out.WriteLine(
                    $"Warning: could not register the sign-in item: {ex.Message}");
            }
        }

        if (!dryRun)
        {
            Directory.CreateDirectory(context.DataDirectory);
            InstallManifest manifest = new()
            {
                SchemaVersion = 1,
                InstalledAt = context.TimeProvider.GetUtcNow(),
                ExecutablePath = executable!,
                WindowsUserVariableSet = variableSet,
                WindowsUserVariablePreviousValue = previousVariableValue,
                ProfileFiles = profiles.Order(StringComparer.Ordinal).ToArray(),
                GhAliasSet = ghAliasSet,
                StartupItem = startupItem,
            };
            await using FileStream stream = new(
                manifestPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None);
            await JsonSerializer.SerializeAsync(
                stream,
                manifest,
                ManifestOptions,
                cancellationToken);
        }

        int exitCode;
        if (dryRun)
        {
            DaemonStartDecision decision =
                DaemonCommands.DecideStart(context, signInItemPlanned);
            if (decision.Outcome == DaemonStartOutcome.Start)
            {
                context.Out.WriteLine("Would start the gh-ledger daemon.");
                exitCode = 0;
            }
            else
            {
                exitCode = DaemonCommands.WriteStartDecision(
                    context,
                    decision,
                    dryRun: true);
            }
        }
        else
        {
            exitCode = await DaemonCommands.StartAsync(
                context,
                Otlp.Receiver.OtlpReceiverOptions.DefaultPort,
                cancellationToken);
        }

        context.Out.WriteLine(dryRun
            ? "Dry run: no changes made."
            : "gh-ledger is installed. Restart the GitHub Copilot app, VS Code, and open terminals so they pick up COPILOT_OTEL_ENABLED.");
        return exitCode;
    }

    private static async Task<int> UninstallAsync(
        CliContext context,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        string manifestPath = ManifestPath(context);
        InstallManifest? manifest = await TryReadManifestAsync(
            manifestPath,
            cancellationToken);
        if (manifest is null)
        {
            context.Out.WriteLine("gh-ledger is not installed; nothing to undo.");
            return 0;
        }

        if (DaemonFiles.IsHeld(context))
        {
            if (dryRun)
            {
                context.Out.WriteLine("Would stop the gh-ledger daemon.");
            }
            else
            {
                await DaemonCommands.StopAsync(context, cancellationToken);
            }
        }

        if (manifest.StartupItem is not null)
        {
            try
            {
                bool exists = context.Platform.IsWindows
                    ? context.Platform.GetStartupCommand("CopilotLedger") is not null
                    : File.Exists(manifest.StartupItem);
                if (exists)
                {
                    if (dryRun)
                    {
                        context.Out.WriteLine(
                            "Would stop starting gh-ledger when you sign in.");
                    }
                    else
                    {
                        if (context.Platform.IsWindows)
                        {
                            context.Platform.SetStartupCommand(
                                "CopilotLedger",
                                null);
                        }
                        else
                        {
                            File.Delete(manifest.StartupItem);
                        }

                        context.Out.WriteLine(
                            "gh-ledger will no longer start when you sign in.");
                    }
                }
            }
            catch (Exception ex) when (
                ex is IOException
                or UnauthorizedAccessException
                or SecurityException)
            {
                context.Out.WriteLine(
                    $"Warning: could not remove the sign-in item: {ex.Message}");
            }
        }

        if (context.Platform.IsWindows && manifest.WindowsUserVariableSet)
        {
            string? current =
                context.Platform.GetUserEnvironmentVariable("COPILOT_OTEL_ENABLED");
            if (IsTrue(current))
            {
                string? previous = manifest.WindowsUserVariablePreviousValue;
                if (dryRun)
                {
                    context.Out.WriteLine(previous is null
                        ? "Would remove COPILOT_OTEL_ENABLED from your user account."
                        : $"Would restore COPILOT_OTEL_ENABLED={previous} for your user account.");
                }
                else
                {
                    context.Platform.SetUserEnvironmentVariable(
                        "COPILOT_OTEL_ENABLED",
                        previous);
                    context.Out.WriteLine(previous is null
                        ? "Removed COPILOT_OTEL_ENABLED from your user account."
                        : $"Restored COPILOT_OTEL_ENABLED={previous} for your user account.");
                }
            }
            else
            {
                context.Out.WriteLine(
                    "Left COPILOT_OTEL_ENABLED unchanged because it is no longer true.");
            }
        }

        foreach (string profile in manifest.ProfileFiles)
        {
            if (!File.Exists(profile))
            {
                continue;
            }

            string contents = await File.ReadAllTextAsync(profile, cancellationToken);
            string[] lines = contents.Split('\n');
            int start = Array.FindIndex(
                lines,
                line => line.TrimEnd('\r') == "# >>> copilot-ledger >>>");
            int end = Array.FindIndex(
                lines,
                line => line.TrimEnd('\r') == "# <<< copilot-ledger <<<");
            if (start >= 0 && end >= start)
            {
                int count = end - start + 1;
                List<string> remaining = lines.ToList();
                if (start > 0 && remaining[start - 1].Length == 0)
                {
                    remaining.RemoveAt(start - 1);
                    start--;
                }

                remaining.RemoveRange(start, count);
                if (!dryRun)
                {
                    await File.WriteAllTextAsync(
                        profile,
                        string.Join('\n', remaining),
                        cancellationToken);
                }

                context.Out.WriteLine(dryRun
                    ? $"Would remove the gh-ledger block from {profile}."
                    : $"Removed the gh-ledger block from {profile}.");
            }
            else
            {
                context.Out.WriteLine(
                    $"Warning: no complete gh-ledger block in {profile}; edit it by hand.");
            }
        }

        if (manifest.GhAliasSet)
        {
            string? gh = context.Platform.FindOnPath("gh");
            if (gh is null)
            {
                context.Out.WriteLine(
                    "Warning: GitHub CLI (gh) was not found. Remove the alias with: gh alias delete ledger");
            }
            else if (dryRun)
            {
                context.Out.WriteLine("Would remove gh alias: ledger.");
            }
            else
            {
                ProcessRunResult result = await context.Platform.RunAsync(
                    gh,
                    ["alias", "delete", "ledger"],
                    cancellationToken);
                string error = result.StandardError.Trim();
                if (result.ExitCode == 0 ||
                    error.Contains("no such alias", StringComparison.OrdinalIgnoreCase))
                {
                    context.Out.WriteLine("Removed gh alias: ledger.");
                }
                else
                {
                    context.Out.WriteLine(
                        $"Warning: could not remove the gh alias: {error}");
                }
            }
        }

        if (dryRun)
        {
            context.Out.WriteLine("Dry run: no changes made.");
        }
        else
        {
            File.Delete(manifestPath);
            context.Out.WriteLine(
                $"gh-ledger is uninstalled. Your ledger data is still in {context.DataDirectory}.");
        }

        return 0;
    }

    internal static async Task<InstallManifest?> TryReadManifestAsync(
        string path,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            await using FileStream stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<InstallManifest>(
                stream,
                ManifestOptions,
                cancellationToken);
        }
        catch (Exception ex) when (
            ex is IOException
            or UnauthorizedAccessException
            or JsonException
            or NotSupportedException)
        {
            return null;
        }
    }

    private static Option<bool> DryRunOption() =>
        new("--dry-run") { Description = "Print the changes without making them." };

    internal static string ManifestPath(CliContext context) =>
        Path.Combine(context.DataDirectory, "install.json");

    private static string ExecutableName(string? path)
    {
        if (path is null)
        {
            return string.Empty;
        }

        int separator = Math.Max(path.LastIndexOf('/'), path.LastIndexOf('\\'));
        string name = separator < 0 ? path : path[(separator + 1)..];
        return name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? name[..^4]
            : name;
    }

    private static bool IsTrue(string? value) =>
        value?.Trim().Equals("true", StringComparison.OrdinalIgnoreCase) == true;

    private static string Quote(string value) =>
        "'" + value.Replace("'", "'\\''", StringComparison.Ordinal) + "'";

    private static string ProfilePath(CliContext context)
    {
        string? shellName = context.Platform.ShellPath;
        if (shellName is not null)
        {
            int separator = shellName.LastIndexOf('/');
            shellName = separator < 0 ? shellName : shellName[(separator + 1)..];
        }

        string fileName = shellName?.Equals("zsh", StringComparison.Ordinal) == true
            ? ".zshrc"
            : shellName?.Equals("bash", StringComparison.Ordinal) == true
                ? context.Platform.IsMacOS ? ".bash_profile" : ".bashrc"
                : ".profile";
        return Path.Combine(context.Platform.HomeDirectory, fileName);
    }

    private static string? FindSh(CliContext context)
    {
        string? sh = context.Platform.FindOnPath("sh");
        if (sh is not null)
        {
            return sh;
        }

        string? git = context.Platform.FindOnPath("git");
        if (git is null)
        {
            return null;
        }

        int separator = Math.Max(git.LastIndexOf('\\'), git.LastIndexOf('/'));
        string directory = separator < 0 ? string.Empty : git[..separator];
        string first = directory + @"\..\bin\sh.exe";
        string second = directory + @"\..\apps\git\current\bin\sh.exe";
        return context.Platform.FileExists(first)
            ? first
            : context.Platform.FileExists(second)
                ? second
                : null;
    }
}
