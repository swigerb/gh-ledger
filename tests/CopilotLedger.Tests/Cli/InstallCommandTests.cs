using System.Security;
using System.Text.Json;
using CopilotLedger.Cli;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Cli;

public sealed class InstallCommandTests
{
    [Fact]
    public async Task IN1_FreshWindowsInstallMakesOnlyFakePlatformChanges()
    {
        using TempDirectory temp = new();
        FakeCliPlatform platform = WindowsPlatform();
        await using CliHarness harness = new(temp.Path, platform);

        CliRun run = await harness.RunAsync("install");

        Assert.Equal(0, run.ExitCode);
        Assert.Equal(
            [("COPILOT_OTEL_ENABLED", "true")],
            platform.UserVariableWrites);
        (string gh, IReadOnlyList<string> arguments) =
            Assert.Single(platform.ProcessRuns);
        Assert.Equal(@"C:\gh\gh.exe", gh);
        Assert.Equal(
            [
                "alias",
                "set",
                "ledger",
                "'C:/tools/gh-ledger.exe' \"$@\"",
                "--shell",
                "--clobber",
            ],
            arguments);
        Assert.Equal(
            Normalize($"""
            Set COPILOT_OTEL_ENABLED=true for your user account.
            Set gh alias: gh ledger runs C:\tools\gh-ledger.exe.
            Warning: GitHub CLI needs sh.exe from Git for Windows to run shell aliases, so gh ledger won't work until Git for Windows is installed. Run gh-ledger directly.
            gh-ledger will start when you sign in.
            gh-ledger daemon started (pid {Environment.ProcessId}) on http://localhost:4318.
            CopilotLedger is installed. Restart the GitHub Copilot app, VS Code, and open terminals so they pick up COPILOT_OTEL_ENABLED.

            """),
            run.Out);
        Assert.Equal(
            [("CopilotLedger", "\"C:\\tools\\gh-ledger.exe\" daemon start")],
            platform.StartupCommandWrites);
        using JsonDocument manifest = JsonDocument.Parse(
            await FileRetry.ReadAllTextAsync(ManifestPath(temp.Path)));
        Assert.Equal(
            @"C:\tools\gh-ledger.exe",
            manifest.RootElement.GetProperty("executablePath").GetString());
        Assert.True(
            manifest.RootElement.GetProperty("windowsUserVariableSet").GetBoolean());
        Assert.Equal(
            JsonValueKind.Null,
            manifest.RootElement
                .GetProperty("windowsUserVariablePreviousValue").ValueKind);
        Assert.Empty(
            manifest.RootElement.GetProperty("profileFiles").EnumerateArray());
        Assert.True(manifest.RootElement.GetProperty("ghAliasSet").GetBoolean());
        Assert.Equal(
            @"HKCU\Software\Microsoft\Windows\CurrentVersion\Run\CopilotLedger",
            manifest.RootElement.GetProperty("startupItem").GetString());
    }

    [Fact]
    public async Task IN2_WindowsInstallOmitsShWarningWhenGitForWindowsProvidesSh()
    {
        using TempDirectory temp = new();
        FakeCliPlatform platform = WindowsPlatform();
        platform.ExistingFiles.Add(@"C:\git\cmd\..\bin\sh.exe");
        await using CliHarness harness = new(temp.Path, platform);

        CliRun run = await harness.RunAsync("install");

        Assert.Equal(0, run.ExitCode);
        Assert.Equal(
            [("COPILOT_OTEL_ENABLED", "true")],
            platform.UserVariableWrites);
        (string gh, IReadOnlyList<string> arguments) =
            Assert.Single(platform.ProcessRuns);
        Assert.Equal(@"C:\gh\gh.exe", gh);
        Assert.Equal(
            [
                "alias",
                "set",
                "ledger",
                "'C:/tools/gh-ledger.exe' \"$@\"",
                "--shell",
                "--clobber",
            ],
            arguments);
        Assert.Equal(
            Normalize($"""
            Set COPILOT_OTEL_ENABLED=true for your user account.
            Set gh alias: gh ledger runs C:\tools\gh-ledger.exe.
            gh-ledger will start when you sign in.
            gh-ledger daemon started (pid {Environment.ProcessId}) on http://localhost:4318.
            CopilotLedger is installed. Restart the GitHub Copilot app, VS Code, and open terminals so they pick up COPILOT_OTEL_ENABLED.

            """),
            run.Out);
        Assert.Equal(
            [("CopilotLedger", "\"C:\\tools\\gh-ledger.exe\" daemon start")],
            platform.StartupCommandWrites);
        using JsonDocument manifest = JsonDocument.Parse(
            await FileRetry.ReadAllTextAsync(ManifestPath(temp.Path)));
        Assert.Equal(
            @"C:\tools\gh-ledger.exe",
            manifest.RootElement.GetProperty("executablePath").GetString());
        Assert.True(
            manifest.RootElement.GetProperty("windowsUserVariableSet").GetBoolean());
        Assert.Equal(
            JsonValueKind.Null,
            manifest.RootElement
                .GetProperty("windowsUserVariablePreviousValue").ValueKind);
        Assert.Empty(
            manifest.RootElement.GetProperty("profileFiles").EnumerateArray());
        Assert.True(manifest.RootElement.GetProperty("ghAliasSet").GetBoolean());
        Assert.Equal(
            @"HKCU\Software\Microsoft\Windows\CurrentVersion\Run\CopilotLedger",
            manifest.RootElement.GetProperty("startupItem").GetString());
    }

    [Fact]
    public async Task IN3_RepeatedWindowsInstallPreservesManifestAndIsIdempotent()
    {
        using TempDirectory temp = new();
        FakeCliPlatform platform = WindowsPlatform();
        await using CliHarness harness = new(temp.Path, platform);
        Assert.Equal(0, (await harness.RunAsync("install")).ExitCode);

        CliRun second = await harness.RunAsync("install");

        Assert.Equal(0, second.ExitCode);
        Assert.Single(platform.UserVariableWrites);
        Assert.StartsWith(
            "COPILOT_OTEL_ENABLED=true is already set for your user account.\n",
            second.Out);
        Assert.Contains("gh-ledger already starts when you sign in.", second.Out);
        Assert.Contains(
            $"gh-ledger daemon is already running (pid {Environment.ProcessId}).",
            second.Out);
        Assert.Single(platform.StartupCommandWrites);
        using JsonDocument manifest = JsonDocument.Parse(
            await FileRetry.ReadAllTextAsync(ManifestPath(temp.Path)));
        Assert.True(
            manifest.RootElement.GetProperty("windowsUserVariableSet").GetBoolean());
        Assert.Equal(
            JsonValueKind.Null,
            manifest.RootElement
                .GetProperty("windowsUserVariablePreviousValue").ValueKind);
    }

    [Fact]
    public async Task IN4_UninstallRestoresPreviousWindowsUserVariable()
    {
        using TempDirectory temp = new();
        FakeCliPlatform platform = WindowsPlatform();
        platform.UserVariables["COPILOT_OTEL_ENABLED"] = "false";
        await using CliHarness harness = new(temp.Path, platform);
        Assert.Equal(0, (await harness.RunAsync("install")).ExitCode);
        Assert.Equal(
            [("COPILOT_OTEL_ENABLED", "true")],
            platform.UserVariableWrites);

        using (JsonDocument manifest = JsonDocument.Parse(
                   await FileRetry.ReadAllTextAsync(ManifestPath(temp.Path))))
        {
            Assert.Equal(
                "false",
                manifest.RootElement
                    .GetProperty("windowsUserVariablePreviousValue").GetString());
        }

        CliRun uninstall = await harness.RunAsync("uninstall");

        Assert.Equal(0, uninstall.ExitCode);
        Assert.Equal(
            ("COPILOT_OTEL_ENABLED", "false"),
            platform.UserVariableWrites[^1]);
        Assert.Contains(
            "Restored COPILOT_OTEL_ENABLED=false for your user account.",
            uninstall.Out);
    }

    [Fact]
    public async Task IN5_UnixInstallAddsOneMarkedProfileBlockAndIsIdempotent()
    {
        using TempDirectory temp = new();
        string profile = Path.Combine(temp.Path, "home", ".zshrc");
        FakeCliPlatform platform = UnixPlatform(temp.Path);
        Directory.CreateDirectory(Path.GetDirectoryName(profile)!);
        await File.WriteAllTextAsync(profile, "export FOO=1");
        await using CliHarness harness = new(temp.Path, platform);

        CliRun first = await harness.RunAsync("install");
        string afterFirst = await FileRetry.ReadAllTextAsync(profile);
        CliRun second = await harness.RunAsync("install");

        Assert.Equal(0, first.ExitCode);
        Assert.Equal(
            "export FOO=1\n\n# >>> copilot-ledger >>>\nexport COPILOT_OTEL_ENABLED=true\n# <<< copilot-ledger <<<\n",
            afterFirst.Replace("\r\n", "\n"));
        Assert.Equal(afterFirst, await FileRetry.ReadAllTextAsync(profile));
        Assert.Empty(platform.UserVariableWrites);
        Assert.Equal(
            "'/opt/bin/gh-ledger' \"$@\"",
            platform.ProcessRuns[0].Arguments[3]);
        Assert.Contains($"Added COPILOT_OTEL_ENABLED=true to {profile}.", first.Out);
        Assert.DoesNotContain("sh.exe", first.Out);
        Assert.Contains(
            $"{profile} already sets COPILOT_OTEL_ENABLED.",
            second.Out);
        using JsonDocument manifest = JsonDocument.Parse(
            await FileRetry.ReadAllTextAsync(ManifestPath(temp.Path)));
        Assert.Equal(
            [profile],
            manifest.RootElement.GetProperty("profileFiles")
                .EnumerateArray().Select(item => item.GetString()));
    }

    public static TheoryData<string?, bool, string> UnixProfiles => new()
    {
        { "/bin/bash", true, ".bash_profile" },
        { "/usr/bin/bash", false, ".bashrc" },
        { "/usr/bin/fish", false, ".profile" },
        { null, false, ".profile" },
    };

    [Theory]
    [MemberData(nameof(UnixProfiles))]
    public async Task IN6_UnixShellSelectsExactProfile(
        string? shellPath,
        bool isMacOs,
        string profileName)
    {
        using TempDirectory temp = new();
        FakeCliPlatform platform = UnixPlatform(temp.Path);
        platform.ShellPath = shellPath;
        platform.IsMacOS = isMacOs;
        await using CliHarness harness = new(temp.Path, platform);

        CliRun run = await harness.RunAsync("install");

        Assert.Equal(0, run.ExitCode);
        Assert.Equal(
            "# >>> copilot-ledger >>>\nexport COPILOT_OTEL_ENABLED=true\n# <<< copilot-ledger <<<\n",
            (await FileRetry.ReadAllTextAsync(
                Path.Combine(temp.Path, "home", profileName))).Replace("\r\n", "\n"));
    }

    public static TheoryData<bool, string> GhFailures => new()
    {
        { true, "GitHub CLI (gh) was not found, so the gh ledger alias was not set. Run gh-ledger directly." },
        { false, "Warning: could not set the gh alias: boom" },
    };

    [Theory]
    [MemberData(nameof(GhFailures))]
    public async Task IN7_MissingOrFailingGhDoesNotFailInstall(
        bool missingGh,
        string expectedMessage)
    {
        using TempDirectory temp = new();
        FakeCliPlatform platform = WindowsPlatform();
        if (missingGh)
        {
            platform.PathExecutables.Remove("gh");
        }
        else
        {
            platform.RunHandler = (_, _, _) =>
                Task.FromResult(new ProcessRunResult(1, "", "boom"));
        }

        await using CliHarness harness = new(temp.Path, platform);
        CliRun run = await harness.RunAsync("install");

        Assert.Equal(0, run.ExitCode);
        Assert.Contains(expectedMessage, run.Out);
        Assert.False(
            JsonDocument.Parse(
                await FileRetry.ReadAllTextAsync(ManifestPath(temp.Path)))
                .RootElement.GetProperty("ghAliasSet").GetBoolean());
    }

    [Fact]
    public async Task IN8_InstallRequiresGhLedgerExecutableAndMakesNoChanges()
    {
        using TempDirectory temp = new();
        FakeCliPlatform platform = UnixPlatform(temp.Path);
        platform.ProcessPath = "/usr/share/dotnet/dotnet";
        await using CliHarness harness = new(temp.Path, platform);

        CliRun run = await harness.RunAsync("install");

        Assert.Equal(1, run.ExitCode);
        Assert.Equal(
            "Run install from the gh-ledger executable (current process: /usr/share/dotnet/dotnet).\n",
            run.Error);
        Assert.Empty(platform.UserVariableWrites);
        Assert.Empty(platform.ProcessRuns);
        Assert.Empty(platform.DetachedStarts);
        Assert.False(File.Exists(ManifestPath(temp.Path)));
    }

    [Fact]
    public async Task IN9_WindowsDryRunMakesNoChangesAndPrintsExactPlan()
    {
        using TempDirectory temp = new();
        FakeCliPlatform platform = WindowsPlatform();
        await using CliHarness harness = new(temp.Path, platform);

        CliRun run = await harness.RunAsync("install", "--dry-run");

        Assert.Equal(0, run.ExitCode);
        Assert.Empty(platform.UserVariableWrites);
        Assert.Empty(platform.ProcessRuns);
        Assert.Empty(platform.DetachedStarts);
        Assert.Empty(platform.StartupCommandWrites);
        Assert.False(File.Exists(ManifestPath(temp.Path)));
        Assert.Equal(
            Normalize("""
            Would set COPILOT_OTEL_ENABLED=true for your user account.
            Would set gh alias: gh ledger runs C:\tools\gh-ledger.exe.
            Warning: GitHub CLI needs sh.exe from Git for Windows to run shell aliases, so gh ledger won't work until Git for Windows is installed. Run gh-ledger directly.
            Would start gh-ledger when you sign in.
            Would start the gh-ledger daemon.
            Dry run: no changes made.

            """),
            run.Out);
    }

    public static TheoryData<string> UninstallCases => new()
    {
        "after-windows",
        "after-unix",
        "no-manifest",
        "no-such-alias",
        "changed-variable",
    };

    [Theory]
    [MemberData(nameof(UninstallCases))]
    public async Task IN10_UninstallReversesOnlyRecordedChanges(string caseName)
    {
        using TempDirectory temp = new();
        FakeCliPlatform platform = caseName == "after-unix"
            ? UnixPlatform(temp.Path)
            : WindowsPlatform();
        await using CliHarness harness = new(temp.Path, platform);

        if (caseName != "no-manifest")
        {
            Assert.Equal(0, (await harness.RunAsync("install")).ExitCode);
        }

        if (caseName == "no-such-alias")
        {
            platform.RunHandler = (_, arguments, _) =>
                Task.FromResult(arguments[1] == "delete"
                    ? new ProcessRunResult(1, "", "no such alias ledger")
                    : new ProcessRunResult(0, "", ""));
        }
        else if (caseName == "changed-variable")
        {
            platform.UserVariables["COPILOT_OTEL_ENABLED"] = "1";
        }

        CliRun uninstall = await harness.RunAsync("uninstall");

        Assert.Equal(0, uninstall.ExitCode);
        if (caseName == "after-windows")
        {
            Assert.Contains("gh-ledger daemon stopped.", uninstall.Out);
            Assert.Contains(
                "gh-ledger will no longer start when you sign in.",
                uninstall.Out);
            Assert.Equal(
                ("CopilotLedger", (string?)null),
                platform.StartupCommandWrites[^1]);
            Assert.Equal(
                ("COPILOT_OTEL_ENABLED", (string?)null),
                platform.UserVariableWrites[^1]);
            Assert.Contains(
                "Removed COPILOT_OTEL_ENABLED from your user account.",
                uninstall.Out);
            Assert.Equal(["alias", "delete", "ledger"], platform.ProcessRuns[^1].Arguments);
            Assert.Equal(@"C:\gh\gh.exe", platform.ProcessRuns[^1].FileName);
            Assert.Contains("Removed gh alias: ledger.", uninstall.Out);
            Assert.False(File.Exists(ManifestPath(temp.Path)));
            Assert.EndsWith(
                $"CopilotLedger is uninstalled. Your ledger data is still in {Path.Combine(temp.Path, "data")}.\n",
                uninstall.Out);
        }
        else if (caseName == "after-unix")
        {
            Assert.Contains(
                "gh-ledger will no longer start when you sign in.",
                uninstall.Out);
            Assert.False(File.Exists(
                Path.Combine(
                    temp.Path,
                    "home",
                    ".config",
                    "autostart",
                    "copilot-ledger.desktop")));
            Assert.Equal(
                "export FOO=1\n",
                (await FileRetry.ReadAllTextAsync(
                    Path.Combine(temp.Path, "home", ".zshrc"))).Replace("\r\n", "\n"));
        }
        else if (caseName == "no-manifest")
        {
            Assert.Equal(
                "CopilotLedger is not installed; nothing to undo.\n",
                uninstall.Out);
        }
        else if (caseName == "no-such-alias")
        {
            Assert.Contains("Removed gh alias: ledger.", uninstall.Out);
        }
        else
        {
            Assert.Contains(
                "Left COPILOT_OTEL_ENABLED unchanged because it is no longer true.",
                uninstall.Out);
            Assert.Single(platform.UserVariableWrites);
        }
    }

    [Fact]
    public async Task IN11_LinuxAutostart()
    {
        using TempDirectory temp = new();
        FakeCliPlatform platform = UnixPlatform(temp.Path);
        await using CliHarness harness = new(temp.Path, platform);
        string autostart = Path.Combine(
            temp.Path,
            "home",
            ".config",
            "autostart",
            "copilot-ledger.desktop");

        CliRun install = await harness.RunAsync("install");

        Assert.Contains(
            "gh-ledger will start when you sign in.",
            install.Out);
        Assert.Equal(
            """
            [Desktop Entry]
            Type=Application
            Name=CopilotLedger
            Comment=Starts the CopilotLedger daemon
            Exec="/opt/bin/gh-ledger" daemon start
            Terminal=false
            """.ReplaceLineEndings("\n") + "\n",
            await FileRetry.ReadAllTextAsync(autostart));
        using (JsonDocument manifest = JsonDocument.Parse(
                   await FileRetry.ReadAllTextAsync(ManifestPath(temp.Path))))
        {
            Assert.Equal(
                autostart,
                manifest.RootElement.GetProperty("startupItem").GetString());
        }

        CliRun second = await harness.RunAsync("install");
        Assert.Contains("gh-ledger already starts when you sign in.", second.Out);

        CliRun uninstall = await harness.RunAsync("uninstall");
        Assert.Contains(
            "gh-ledger will no longer start when you sign in.",
            uninstall.Out);
        Assert.False(File.Exists(autostart));
    }

    public static TheoryData<string> XdgConfigHomes => new()
    {
        "absolute",
        "relative",
    };

    [Theory]
    [MemberData(nameof(XdgConfigHomes))]
    public async Task IN12_XdgConfigHome(string caseName)
    {
        using TempDirectory temp = new();
        FakeCliPlatform platform = UnixPlatform(temp.Path);
        string? xdg = caseName == "absolute"
            ? Path.Combine(temp.Path, "xdg")
            : Path.Combine("relative", "xdg");
        platform.ProcessVariables["XDG_CONFIG_HOME"] = xdg;
        string expected = Path.Combine(
            caseName == "absolute"
                ? xdg
                : Path.Combine(temp.Path, "home", ".config"),
            "autostart",
            "copilot-ledger.desktop");
        await using CliHarness harness = new(temp.Path, platform);

        Assert.Equal(0, (await harness.RunAsync("install")).ExitCode);

        Assert.True(File.Exists(expected));
        if (caseName == "absolute")
        {
            Assert.False(Directory.Exists(
                Path.Combine(temp.Path, "home", ".config")));
        }
    }

    [Fact]
    public async Task IN13_MacOSLaunchAgent()
    {
        using TempDirectory temp = new();
        FakeCliPlatform platform = UnixPlatform(temp.Path);
        platform.IsMacOS = true;
        await using CliHarness harness = new(temp.Path, platform);
        string agent = Path.Combine(
            temp.Path,
            "home",
            "Library",
            "LaunchAgents",
            "io.github.swigerb.copilot-ledger.plist");

        CliRun install = await harness.RunAsync("install");

        Assert.Contains(
            "gh-ledger will start when you sign in.",
            install.Out);
        Assert.Equal(
            $$"""
            <?xml version="1.0" encoding="UTF-8"?>
            <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
            <plist version="1.0">
            <dict>
              <key>Label</key>
              <string>io.github.swigerb.copilot-ledger</string>
              <key>ProgramArguments</key>
              <array>
                <string>/opt/bin/gh-ledger</string>
                <string>daemon</string>
                <string>run</string>
                <string>--log-file</string>
                <string>{{SecurityElement.Escape(Path.Combine(temp.Path, "data", "daemon.log"))}}</string>
              </array>
              <key>RunAtLoad</key>
              <true/>
            </dict>
            </plist>
            """.ReplaceLineEndings("\n") + "\n",
            await FileRetry.ReadAllTextAsync(agent));
        using (JsonDocument manifest = JsonDocument.Parse(
                   await FileRetry.ReadAllTextAsync(ManifestPath(temp.Path))))
        {
            Assert.Equal(
                agent,
                manifest.RootElement.GetProperty("startupItem").GetString());
        }

        CliRun uninstall = await harness.RunAsync("uninstall");
        Assert.Contains(
            "gh-ledger will no longer start when you sign in.",
            uninstall.Out);
        Assert.False(File.Exists(agent));
    }

    public static TheoryData<string> UnregisterableItems => new()
    {
        "windows-too-long",
        "linux-characters",
    };

    [Theory]
    [MemberData(nameof(UnregisterableItems))]
    public async Task IN14_ItemsThatCantBeRegistered(string caseName)
    {
        using TempDirectory temp = new();
        FakeCliPlatform platform = caseName == "windows-too-long"
            ? WindowsPlatform()
            : UnixPlatform(temp.Path);
        if (caseName == "windows-too-long")
        {
            platform.ProcessPath = @"C:\" + new string('a', 250) + @"\gh-ledger.exe";
        }
        else
        {
            platform.ProcessPath = "/opt/my$tools/gh-ledger";
        }

        await using CliHarness harness = new(temp.Path, platform);
        CliRun run = await harness.RunAsync("install");

        Assert.Equal(0, run.ExitCode);
        if (caseName == "windows-too-long")
        {
            Assert.Contains(
                "Warning: the gh-ledger path is too long for a sign-in command, so gh-ledger won't start when you sign in. Run gh-ledger daemon start after you sign in.",
                run.Out);
            Assert.Empty(platform.StartupCommandWrites);
        }
        else
        {
            Assert.Contains(
                "Warning: the gh-ledger path has characters that a desktop autostart entry can't run, so gh-ledger won't start when you sign in. Run gh-ledger daemon start after you sign in.",
                run.Out);
            Assert.False(File.Exists(
                Path.Combine(
                    temp.Path,
                    "home",
                    ".config",
                    "autostart",
                    "copilot-ledger.desktop")));
        }

        using JsonDocument manifest = JsonDocument.Parse(
            await FileRetry.ReadAllTextAsync(ManifestPath(temp.Path)));
        Assert.Equal(
            JsonValueKind.Null,
            manifest.RootElement.GetProperty("startupItem").ValueKind);
    }

    [Fact]
    public async Task IN15_CopilotLedgerHomeWarnsBeforeRegistration()
    {
        using TempDirectory temp = new();
        FakeCliPlatform platform = WindowsPlatform();
        platform.ProcessVariables["COPILOT_LEDGER_HOME"] =
            Path.Combine(temp.Path, "data");
        await using CliHarness harness = new(temp.Path, platform);

        CliRun run = await harness.RunAsync("install");

        Assert.Equal(0, run.ExitCode);
        int warning = run.Out.IndexOf(
            "Warning: COPILOT_LEDGER_HOME is set. The daemon that starts when you sign in uses it only if your sign-in environment sets it too.",
            StringComparison.Ordinal);
        int registration = run.Out.IndexOf(
            "gh-ledger will start when you sign in.",
            StringComparison.Ordinal);
        Assert.True(warning >= 0);
        Assert.True(registration > warning);
    }

    public static TheoryData<string> StartupFailures => new()
    {
        "windows-register-fails",
        "linux-register-fails",
        "windows-remove-fails",
    };

    [Theory]
    [MemberData(nameof(StartupFailures))]
    public async Task IN16_SignInItemsThatFail(string caseName)
    {
        using TempDirectory temp = new();
        FakeCliPlatform platform = caseName == "linux-register-fails"
            ? UnixPlatform(temp.Path)
            : WindowsPlatform();
        if (caseName == "windows-register-fails")
        {
            platform.StartupCommandException =
                new UnauthorizedAccessException("Access to the registry key is denied.");
        }
        else if (caseName == "linux-register-fails")
        {
            string config = Path.Combine(temp.Path, "home", ".config");
            Directory.CreateDirectory(config);
            await File.WriteAllTextAsync(
                Path.Combine(config, "autostart"),
                string.Empty);
        }

        await using CliHarness harness = new(temp.Path, platform);

        if (caseName == "windows-remove-fails")
        {
            Assert.Equal(0, (await harness.RunAsync("install")).ExitCode);
            platform.StartupCommandException =
                new System.Security.SecurityException(
                    "Requested registry access is not allowed.");

            CliRun uninstall = await harness.RunAsync("uninstall");

            Assert.Equal(0, uninstall.ExitCode);
            Assert.Contains(
                "Warning: could not remove the sign-in item: Requested registry access is not allowed.",
                uninstall.Out);
            Assert.True(platform.StartupCommands.ContainsKey("CopilotLedger"));
            Assert.EndsWith(
                $"CopilotLedger is uninstalled. Your ledger data is still in {Path.Combine(temp.Path, "data")}.\n",
                uninstall.Out);
            return;
        }

        CliRun install = await harness.RunAsync("install");

        Assert.Equal(0, install.ExitCode);
        if (caseName == "windows-register-fails")
        {
            Assert.Contains(
                "Warning: could not register the sign-in item: Access to the registry key is denied.",
                install.Out);
            Assert.Contains(
                $"gh-ledger daemon started (pid {Environment.ProcessId}) on http://localhost:4318.",
                install.Out);
            Assert.DoesNotContain(
                "gh-ledger will start when you sign in.",
                install.Out);
            Assert.Empty(platform.StartupCommandWrites);
        }
        else
        {
            Assert.Contains(
                install.Out.Split('\n'),
                line => line.StartsWith(
                    "Warning: could not register the sign-in item: ",
                    StringComparison.Ordinal));
        }

        using JsonDocument manifest = JsonDocument.Parse(
            await FileRetry.ReadAllTextAsync(ManifestPath(temp.Path)));
        Assert.Equal(
            JsonValueKind.Null,
            manifest.RootElement.GetProperty("startupItem").ValueKind);
        if (caseName == "linux-register-fails")
        {
            Assert.True(File.Exists(
                Path.Combine(temp.Path, "home", ".config", "autostart")));
        }
    }

    private static FakeCliPlatform WindowsPlatform() =>
        new()
        {
            IsWindows = true,
            ProcessPath = @"C:\tools\gh-ledger.exe",
            PathExecutables =
            {
                ["gh"] = @"C:\gh\gh.exe",
                ["git"] = @"C:\git\cmd\git.exe",
            },
        };

    private const string WindowsExtension =
        @"C:\Users\me\AppData\Local\GitHub CLI\extensions\gh-ledger\gh-ledger.exe";

    [Fact]
    public async Task IN17_WindowsExtensionInstallSkipsTheAlias()
    {
        using TempDirectory temp = new();
        FakeCliPlatform platform = WindowsPlatform();
        platform.ProcessPath = WindowsExtension;
        platform.ExistingFiles.Add(
            @"C:\Users\me\AppData\Local\GitHub CLI\extensions\gh-ledger\manifest.yml");
        await using CliHarness harness = new(temp.Path, platform);

        CliRun run = await harness.RunAsync("install");

        Assert.Equal(0, run.ExitCode);
        Assert.Equal(
            "Set COPILOT_OTEL_ENABLED=true for your user account.\n" +
            "gh ledger runs this GitHub CLI extension, so no alias is needed.\n" +
            "gh-ledger will start when you sign in.\n" +
            $"gh-ledger daemon started (pid {Environment.ProcessId}) on http://localhost:4318.\n" +
            "CopilotLedger is installed. Restart the GitHub Copilot app, VS Code, and open terminals so they pick up COPILOT_OTEL_ENABLED.\n",
            run.Out);
        Assert.Equal(string.Empty, run.Error);
        Assert.Empty(platform.ProcessRuns);
        Assert.Equal(
            [("CopilotLedger", $"\"{WindowsExtension}\" daemon start")],
            platform.StartupCommandWrites);
        DetachedStart start = Assert.Single(platform.DetachedStarts);
        Assert.Equal(WindowsExtension, start.FileName);
        using JsonDocument manifest = JsonDocument.Parse(
            await FileRetry.ReadAllTextAsync(ManifestPath(temp.Path)));
        Assert.Equal(
            WindowsExtension,
            manifest.RootElement.GetProperty("executablePath").GetString());
        Assert.False(manifest.RootElement.GetProperty("ghAliasSet").GetBoolean());
    }

    [Fact]
    public async Task IN18_LinuxExtensionInstallStartsTheExtensionAtSignIn()
    {
        using TempDirectory temp = new();
        FakeCliPlatform platform = UnixPlatform(temp.Path);
        platform.ProcessPath = "/home/me/.local/share/gh/extensions/gh-ledger/gh-ledger";
        platform.ExistingFiles.Add("/home/me/.local/share/gh/extensions/gh-ledger/manifest.yml");
        await using CliHarness harness = new(temp.Path, platform);

        CliRun run = await harness.RunAsync("install");

        Assert.Equal(0, run.ExitCode);
        Assert.Equal(
            $"Added COPILOT_OTEL_ENABLED=true to {Path.Combine(temp.Path, "home", ".zshrc")}.\n" +
            "gh ledger runs this GitHub CLI extension, so no alias is needed.\n" +
            "gh-ledger will start when you sign in.\n" +
            $"gh-ledger daemon started (pid {Environment.ProcessId}) on http://localhost:4318.\n" +
            "CopilotLedger is installed. Restart the GitHub Copilot app, VS Code, and open terminals so they pick up COPILOT_OTEL_ENABLED.\n",
            run.Out);
        Assert.Equal(string.Empty, run.Error);
        Assert.Empty(platform.ProcessRuns);
        Assert.Equal(
            """
            [Desktop Entry]
            Type=Application
            Name=CopilotLedger
            Comment=Starts the CopilotLedger daemon
            Exec="/home/me/.local/share/gh/extensions/gh-ledger/gh-ledger" daemon start
            Terminal=false
            """.ReplaceLineEndings("\n") + "\n",
            await FileRetry.ReadAllTextAsync(Path.Combine(
                temp.Path,
                "home",
                ".config",
                "autostart",
                "copilot-ledger.desktop")));
    }

    public static TheoryData<string, string?> NotExtensions => new()
    {
        { @"C:\Users\me\AppData\Local\GitHub CLI\extensions\gh-ledger\gh-ledger.exe", null },
        { @"C:\Users\me\AppData\Local\GitHub CLI\other\gh-ledger\gh-ledger.exe", @"C:\Users\me\AppData\Local\GitHub CLI\other\gh-ledger\manifest.yml" },
        { @"C:\Users\me\AppData\Local\GitHub CLI\extensions\gh-tools\gh-ledger.exe", @"C:\Users\me\AppData\Local\GitHub CLI\extensions\gh-tools\manifest.yml" },
    };

    [Theory]
    [MemberData(nameof(NotExtensions))]
    public async Task IN19_OnlyTheGhExtensionLayoutSkipsTheAlias(string executable, string? existingManifest)
    {
        using TempDirectory temp = new();
        FakeCliPlatform platform = WindowsPlatform();
        platform.ProcessPath = executable;
        if (existingManifest is not null)
        {
            platform.ExistingFiles.Add(existingManifest);
        }

        await using CliHarness harness = new(temp.Path, platform);

        CliRun run = await harness.RunAsync("install");

        Assert.Equal(0, run.ExitCode);
        (string gh, IReadOnlyList<string> arguments) = Assert.Single(platform.ProcessRuns);
        Assert.Equal(@"C:\gh\gh.exe", gh);
        Assert.Equal(
            ["alias", "set", "ledger", $"'{executable.Replace('\\', '/')}' \"$@\"", "--shell", "--clobber"],
            arguments);
        Assert.DoesNotContain("no alias is needed", run.Out);
    }

    private static FakeCliPlatform UnixPlatform(string root)
    {
        string home = Path.Combine(root, "home");
        Directory.CreateDirectory(home);
        File.WriteAllText(Path.Combine(home, ".zshrc"), "export FOO=1");
        return new()
        {
            IsWindows = false,
            HomeDirectory = home,
            ShellPath = "/bin/zsh",
            ProcessPath = "/opt/bin/gh-ledger",
            PathExecutables =
            {
                ["gh"] = "/usr/bin/gh",
            },
        };
    }

    private static string ManifestPath(string root) =>
        Path.Combine(root, "data", "install.json");

    private static string Normalize(string value) => value.Replace("\r\n", "\n");
}
