using System.Globalization;
using System.Text.Json;
using CopilotLedger.Cli;
using CopilotLedger.Core.Breaker;
using CopilotLedger.Core.Git;
using CopilotLedger.Mcp;
using CopilotLedger.Storage;
using CopilotLedger.Tests.TestSupport;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Protocol;

namespace CopilotLedger.Tests.Cli;

public sealed class ExtensionHintTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(false, "gh-ledger")]
    [InlineData(true, "gh ledger")]
    public async Task XH1_BreakerStatus_NamesTheCommand(
        bool extension,
        string command)
    {
        using TempDirectory temp = new();
        await using CliHarness harness =
            new(temp.Path, Platform(extension), new ManualTimeProvider(Now));
        SqliteLedgerStore store =
            new(harness.Context.DatabasePath, harness.TimeProvider);
        await store.InitializeAsync();
        await store.RecordBreakerTripAsync(Trip());

        CliRun run = await harness.RunAsync("breaker", "status");

        Assert.Equal(0, run.ExitCode);
        Assert.EndsWith(
            "\nReset a trip with: " + command + " breaker reset --id <id>\n",
            run.Out,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false, "gh-ledger")]
    [InlineData(true, "gh ledger")]
    public async Task XH2_TheFlagFile_NamesTheCommand(
        bool extension,
        string command)
    {
        using TempDirectory temp = new();
        string repo = GitFixture.Create(temp.Path);
        await using CliHarness harness =
            new(temp.Path, Platform(extension), new ManualTimeProvider(Now));
        SqliteLedgerStore store =
            new(harness.Context.DatabasePath, harness.TimeProvider);
        await store.InitializeAsync();
        await store.RecordBreakerTripAsync(Trip() with { RepositoryRoot = repo });

        CliRun run = await harness.RunAsync(
            "breaker", "config", "--flag-file", "on");

        Assert.Equal(0, run.ExitCode);
        using JsonDocument flag = JsonDocument.Parse(
            await FileRetry.ReadAllTextAsync(
                Path.Combine(repo, ".copilot-ledger", "CIRCUIT_OPEN")));
        JsonElement trip =
            Assert.Single(flag.RootElement.GetProperty("openTrips").EnumerateArray());
        Assert.Equal(
            command + " breaker reset --id 1",
            trip.GetProperty("resetCommand").GetString());
    }

    [Theory]
    [InlineData(null, "gh-ledger")]
    [InlineData("gh ledger", "gh ledger")]
    public async Task XH3_McpWarnings_NameTheCommand(
        string? contextCommand,
        string command)
    {
        using TempDirectory temp = new();
        GitFixture.Create(temp.Path);
        SqliteLedgerStore store = await AuditSeeds.CreateAsync(temp.Path);
        await store.RecordBreakerTripAsync(Trip() with
        {
            TraceId = AuditSeeds.R,
            ConversationId = "conv-r",
            TrippedAt = AuditSeeds.At("15:01:00"),
        });
        LedgerMcpContext context = new()
        {
            DatabasePath = Path.Combine(temp.Path, "data", "ledger.db"),
            WorkingDirectory = Path.Combine(temp.Path, "repo"),
            GetEnvironmentVariable = _ => null,
            GitResolver = new FileSystemGitContextResolver(
                ceilingDirectory: temp.Path),
            TimeProvider = new ManualTimeProvider(
                AuditSeeds.GeneratedAt,
                ManualTimeProvider.UtcMinus5),
            Version = "9.9.9",
        };
        if (contextCommand is not null)
        {
            context = context with { Command = contextCommand };
        }

        await using McpTestHarness harness =
            await McpTestHarness.StartAsync(context);
        CallToolResult result = await harness.Client.CallToolAsync(
            "get_branch_ledger",
            new Dictionary<string, object?>());

        Assert.NotEqual(true, result.IsError);
        string text =
            Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Equal(
            "WARNING: gh-ledger circuit breaker is open for swigerb/copilot-ledger @ feature/ledger: 180.00 AI credits in 5 minutes on one prompt (trip 1). Stop and ask the user before continuing. The user can reset it with: " +
            command +
            " breaker reset --id 1",
            text.Split('\n')[0]);
        JsonElement structured = Assert.NotNull(result.StructuredContent);
        JsonElement trip = Assert.Single(
            structured.GetProperty("circuitBreaker")
                .GetProperty("openTrips")
                .EnumerateArray());
        Assert.Equal(
            command + " breaker reset --id 1",
            trip.GetProperty("resetCommand").GetString());
    }

    [Theory]
    [InlineData(false, "gh-ledger")]
    [InlineData(true, "gh ledger")]
    public async Task XH4_RatesInit_NamesTheCommand(
        bool extension,
        string command)
    {
        using TempDirectory temp = new();
        await using CliHarness harness =
            new(temp.Path, Platform(extension), new ManualTimeProvider(Now));

        CliRun run = await harness.RunAsync("rates", "init");

        Assert.Equal(0, run.ExitCode);
        Assert.Empty(run.Error);
        Assert.Equal(
            "Created " + harness.Context.RatesOverlayPath +
            ". Add your prices, then run: " + command + " rates validate\n",
            run.Out);
    }

    [Theory]
    [InlineData("windows")]
    [InlineData("linux")]
    public async Task XH5_UnregisterableSignInItems_NameTheExtensionsCommand(
        string platformName)
    {
        using TempDirectory temp = new();
        FakeCliPlatform platform = platformName == "windows"
            ? WindowsUnregisterableExtension()
            : LinuxUnregisterableExtension(temp.Path);
        await using CliHarness harness = new(
            temp.Path,
            platform,
            new ManualTimeProvider(Now));

        CliRun run = await harness.RunAsync("install", "--dry-run");

        Assert.Equal(0, run.ExitCode);
        string warning = platformName switch
        {
            "windows" =>
                "Warning: the gh-ledger path is too long for a sign-in command, so gh-ledger won't start when you sign in. Run gh ledger daemon start after you sign in.",
            "linux" =>
                "Warning: the gh-ledger path has characters that a desktop autostart entry can't run, so gh-ledger won't start when you sign in. Run gh ledger daemon start after you sign in.",
            _ => throw new ArgumentOutOfRangeException(nameof(platformName)),
        };
        Assert.Contains(warning + "\n", run.Out, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false, "gh-ledger")]
    [InlineData(true, "gh ledger")]
    public async Task XH6_APortInUse_NamesTheCommand(
        bool extension,
        string command)
    {
        using TempDirectory temp = new();
        await using CliHarness harness =
            new(temp.Path, Platform(extension), new ManualTimeProvider(Now));
        using StringWriter output = new(CultureInfo.InvariantCulture);
        using StringWriter error = new(CultureInfo.InvariantCulture);

        int exitCode = await GhLedgerCli.InvokeAsync(
            ["daemon", "run", "--port", "4319"],
            harness.Context with
            {
                Out = output,
                Error = error,
                ConfigureWebHost = builder =>
                {
                    builder.UseTestServer();
                    builder.ConfigureServices(
                        services =>
                            services.AddHostedService(_ => new PortInUse()));
                },
            });

        Assert.Equal(1, exitCode);
        Assert.Equal(
            "2026-11-24T12:00:00.0000000+00:00 Port 4319 is in use. Stop the other program, or run " +
            command +
            " daemon run --port 4319.\n",
            output.ToString().ReplaceLineEndings("\n"));
        Assert.Empty(error.ToString());
    }

    private static NewBreakerTrip Trip() => new()
    {
        Rule = BreakerRule.CreditVelocity,
        TraceId = new string('1', 32),
        Repository = "swigerb/copilot-ledger",
        Branch = "feature/ledger",
        TrippedAt = Now.AddMinutes(-30),
        Detail = "180.00 AI credits in 5 minutes",
        WindowCostUsd = 1.80m,
        WindowMinutes = 5,
    };

    private static FakeCliPlatform Platform(bool extension) =>
        extension
            ? new FakeCliPlatform
            {
                IsWindows = true,
                ProcessPath =
                    @"C:\Users\me\AppData\Local\GitHub CLI\extensions\gh-ledger\gh-ledger.exe",
                ExistingFiles =
                {
                    @"C:\Users\me\AppData\Local\GitHub CLI\extensions\gh-ledger\manifest.yml",
                },
            }
            : new FakeCliPlatform();

    private static FakeCliPlatform WindowsUnregisterableExtension()
    {
        string directory =
            @"C:\" + new string('a', 220) +
            @"\GitHub CLI\extensions\gh-ledger";
        return new FakeCliPlatform
        {
            IsWindows = true,
            ProcessPath = directory + @"\gh-ledger.exe",
            ExistingFiles =
            {
                directory + @"\manifest.yml",
            },
            PathExecutables =
            {
                ["gh"] = @"C:\gh\gh.exe",
            },
        };
    }

    private static FakeCliPlatform LinuxUnregisterableExtension(string root)
    {
        string home = Path.Combine(root, "home");
        Directory.CreateDirectory(home);
        File.WriteAllText(Path.Combine(home, ".zshrc"), "export FOO=1");
        const string directory =
            "/home/me/my$data/gh/extensions/gh-ledger";
        return new FakeCliPlatform
        {
            IsWindows = false,
            HomeDirectory = home,
            ShellPath = "/bin/zsh",
            ProcessPath = directory + "/gh-ledger",
            ExistingFiles =
            {
                directory + "/manifest.yml",
            },
            PathExecutables =
            {
                ["gh"] = "/usr/bin/gh",
            },
        };
    }

    private sealed class PortInUse : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken) =>
            throw new IOException("Address already in use");

        public Task StopAsync(CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
