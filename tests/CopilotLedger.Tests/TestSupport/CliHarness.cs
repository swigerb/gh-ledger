using System.Globalization;
using CopilotLedger.Cli;
using CopilotLedger.Otlp.Decoding;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CopilotLedger.Tests.TestSupport;

public sealed record CliRun(int ExitCode, string Out, string Error);

public sealed class CliHarness : IAsyncDisposable
{
    private readonly StringWriter _daemonOut = new(CultureInfo.InvariantCulture);
    private Task<CliRun>? _daemon;
    private TestServer? _daemonServer;

    public CliHarness(
        string root,
        FakeCliPlatform? platform = null,
        TimeProvider? timeProvider = null)
    {
        Platform = platform ?? new FakeCliPlatform();
        TimeProvider = timeProvider ?? TimeProvider.System;
        Root = root;
        Context = new CliContext
        {
            Out = new StringWriter(CultureInfo.InvariantCulture),
            Error = new StringWriter(CultureInfo.InvariantCulture),
            DataDirectory = Path.Combine(root, "data"),
            CopilotHome = Path.Combine(root, "copilot-home"),
            CodexHome = Path.Combine(root, "codex-home"),
            WorkingDirectory = root,
            TimeProvider = TimeProvider,
            Platform = Platform,
            ConfigureWebHost = builder =>
            {
                builder.UseTestServer();
                builder.ConfigureServices(services =>
                    services.AddHostedService(provider =>
                        new TestServerCapture(
                            provider.GetRequiredService<IServer>(),
                            server => _daemonServer = server)));
            },
            DaemonPollInterval = TimeSpan.FromMilliseconds(50),
            DaemonStartTimeout = TimeSpan.FromSeconds(10),
            DaemonStopTimeout = TimeSpan.FromSeconds(10),
        };
        if (Platform.StartDetachedHandler is null)
        {
            Platform.StartDetachedHandler = (fileName, arguments, workingDirectory) =>
            {
                string[] daemonArgs = arguments.Skip(2).ToArray();
                _daemon = InvokeAsync(Context with
                {
                    Out = _daemonOut,
                    Error = _daemonOut,
                }, ["daemon", "run", .. daemonArgs]);
                return Environment.ProcessId;
            };
        }
    }

    public string Root { get; }
    public FakeCliPlatform Platform { get; }
    public TimeProvider TimeProvider { get; }
    public CliContext Context { get; }
    public Task<CliRun>? InProcessDaemon => _daemon;
    public string DaemonOutput =>
        InProcessDaemon is { IsCompletedSuccessfully: true } run
            ? run.Result.Out
            : throw new InvalidOperationException("The in-process daemon hasn't finished.");

    public Task<CliRun> RunAsync(params string[] args) => InvokeAsync(Context, args);

    public HttpClient CreateDaemonClient() =>
        _daemonServer?.CreateClient()
        ?? throw new InvalidOperationException("No in-process daemon is running.");

    public Task<CliRun> RunInAsync(string workingDirectory, params string[] args) =>
        InvokeAsync(Context with { WorkingDirectory = workingDirectory }, args);

    public Task<CliRun> RunWithHttpAsync(HttpMessageHandler handler, params string[] args) =>
        InvokeAsync(Context with { CreateHttpHandler = () => handler }, args);

    // audit --local-llm sends through the handler that createHandler returns.
    public Task<CliRun> RunWithLocalLlmAsync(Func<HttpMessageHandler> createHandler, params string[] args) =>
        InvokeAsync(Context with { CreateLocalLlmHandler = createHandler }, args);

    public async Task<CliRun> StartInProcessDaemonAsync()
    {
        _daemon = InvokeAsync(Context with
        {
            Out = _daemonOut,
            Error = _daemonOut,
        }, ["daemon", "run"]);
        await WaitForAsync(
            () => File.Exists(Path.Combine(Context.DataDirectory, "daemon.json")),
            TimeSpan.FromSeconds(10));
        return await Task.FromResult(new CliRun(0, string.Empty, string.Empty));
    }

    public async ValueTask DisposeAsync()
    {
        if (_daemon is { IsCompleted: false })
        {
            Directory.CreateDirectory(Context.DataDirectory);
            File.WriteAllBytes(
                Path.Combine(Context.DataDirectory, "daemon.stop"),
                []);
            await Task.WhenAny(_daemon, Task.Delay(TimeSpan.FromSeconds(10)));
        }
    }

    private static async Task<CliRun> InvokeAsync(CliContext context, string[] args)
    {
        StringWriter output = new(CultureInfo.InvariantCulture);
        StringWriter error = new(CultureInfo.InvariantCulture);
        CliContext invocation = context with { Out = output, Error = error };
        int exitCode = await GhLedgerCli.InvokeAsync(args, invocation);
        return new CliRun(
            exitCode,
            output.ToString().Replace("\r\n", "\n"),
            error.ToString().Replace("\r\n", "\n"));
    }

    private static async Task WaitForAsync(Func<bool> condition, TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(25);
        }

        if (!condition())
        {
            throw new TimeoutException();
        }
    }

    private sealed class TestServerCapture(
        IServer server,
        Action<TestServer> capture) : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken)
        {
            capture((TestServer)server);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
