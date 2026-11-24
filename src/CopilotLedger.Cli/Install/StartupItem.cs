using System.Globalization;
using System.Security;

namespace CopilotLedger.Cli.Install;

internal static class StartupItem
{
    public static StartupItemPlan Plan(CliContext context, string executable)
    {
        if (context.Platform.IsWindows)
        {
            string command = $"\"{executable}\" daemon start";
            return new StartupItemPlan(
                @"HKCU\Software\Microsoft\Windows\CurrentVersion\Run\CopilotLedger",
                command,
                command.Length > 260
                    ? "Warning: the gh-ledger path is too long for a sign-in command, so gh-ledger won't start when you sign in. Run gh-ledger daemon start after you sign in."
                    : null);
        }

        if (context.Platform.IsMacOS)
        {
            string identifier = Path.Combine(
                context.Platform.HomeDirectory,
                "Library",
                "LaunchAgents",
                "io.github.swigerb.copilot-ledger.plist");
            string template = """
                <?xml version="1.0" encoding="UTF-8"?>
                <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
                <plist version="1.0">
                <dict>
                  <key>Label</key>
                  <string>io.github.swigerb.copilot-ledger</string>
                  <key>ProgramArguments</key>
                  <array>
                    <string>{0}</string>
                    <string>daemon</string>
                    <string>run</string>
                    <string>--log-file</string>
                    <string>{1}</string>
                  </array>
                  <key>RunAtLoad</key>
                  <true/>
                </dict>
                </plist>
                """.ReplaceLineEndings("\n") + "\n";
            string content = string.Format(
                CultureInfo.InvariantCulture,
                template,
                SecurityElement.Escape(executable),
                SecurityElement.Escape(
                    Path.Combine(context.DataDirectory, "daemon.log")));
            return new StartupItemPlan(identifier, content, null);
        }

        string? config = context.Platform.GetEnvironmentVariable("XDG_CONFIG_HOME");
        string configDirectory = !string.IsNullOrWhiteSpace(config)
            && Path.IsPathFullyQualified(config)
                ? config
                : Path.Combine(context.Platform.HomeDirectory, ".config");
        string desktop = Path.Combine(
            configDirectory,
            "autostart",
            "copilot-ledger.desktop");
        bool invalid = executable.IndexOfAny(['"', '`', '$', '\\', '%', '\n', '\r']) >= 0;
        string desktopTemplate = """
            [Desktop Entry]
            Type=Application
            Name=gh-ledger
            Comment=Starts the gh-ledger daemon
            Exec="{0}" daemon start
            Terminal=false
            """.ReplaceLineEndings("\n") + "\n";
        string desktopContent = string.Format(
            CultureInfo.InvariantCulture,
            desktopTemplate,
            executable);
        return new StartupItemPlan(
            desktop,
            desktopContent,
            invalid
                ? "Warning: the gh-ledger path has characters that a desktop autostart entry can't run, so gh-ledger won't start when you sign in. Run gh-ledger daemon start after you sign in."
                : null);
    }
}

internal sealed record StartupItemPlan(
    string Identifier,
    string Content,
    string? SkipWarning);
