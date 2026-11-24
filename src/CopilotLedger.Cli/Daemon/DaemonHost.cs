using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using CopilotLedger.Cli.Alerts;
using CopilotLedger.Cli.Install;
using CopilotLedger.Cli.Rates;
using CopilotLedger.Core.Git;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Otlp.Attribution;
using CopilotLedger.Otlp.Breaker;
using CopilotLedger.Otlp.Ingestion;
using CopilotLedger.Otlp.Receiver;
using CopilotLedger.Storage;
using Microsoft.AspNetCore.Builder;

namespace CopilotLedger.Cli.Daemon;

internal static class DaemonHost
{
    public static async Task<int> RunAsync(
        CliContext context,
        int port,
        string? logFile,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(context.DataDirectory);
        TextWriter writer = TextWriter.Synchronized(
            CreateLogWriter(context, logFile, out bool ownsWriter));
        FileStream lockStream;
        try
        {
            lockStream = DaemonFiles.AcquireLock(context);
        }
        catch (IOException)
        {
            try
            {
                WriteLog(writer, context, "gh-ledger daemon is already running.");
            }
            finally
            {
                if (ownsWriter)
                {
                    writer.Dispose();
                }
            }

            return 1;
        }

        WebApplication? app = null;
        AlertDispatcher? dispatcher = null;
        Task? dispatching = null;
        CancellationTokenSource dispatchStop = new();
        bool started = false;
        PosixSignalRegistration? sighupRegistration = null;
        try
        {
            DaemonFiles.DeleteIfExists(DaemonFiles.StopPath(context));

            try
            {
                if (DetachedLaunchPolicy.DiesWithJob(context.Platform.GetJobState()))
                {
                    WriteLog(writer, context, $"Warning: this daemon runs in a Windows job that stops its processes when the job closes, so it stops when the program that started it closes. Run {GhExtension.CommandName(context.Platform)} daemon start from a regular terminal.");
                }
            }
            // The job state is advisory here, and the daemon keeps running.
            catch (Win32Exception)
            {
            }

            RatesBase rates = RateCardFiles.LoadBase(context);
            if (rates.Warning is not null)
            {
                WriteLog(writer, context, $"Warning: {rates.Warning}");
            }

            JsonRateCardCalculator calculator;
            try
            {
                calculator = RateCardFiles.Load(rates, RateCardFiles.OverlayPath(context));
            }
            catch (InvalidDataException ex)
            {
                WriteLog(writer, context, $"Invalid rates overlay: {ex.Message}");
                return 1;
            }

            SqliteLedgerStore store = new(context.DatabasePath, context.TimeProvider);
            try
            {
                await store.InitializeAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                WriteLog(
                    writer,
                    context,
                    $"Cannot open the ledger at {context.DatabasePath}: {ex.Message}");
                return 1;
            }

            DateTimeOffset lastPrune = context.TimeProvider.GetUtcNow();
            await store.PruneSpanLinksAsync(
                lastPrune - TimeSpan.FromDays(7),
                cancellationToken);
            dispatcher = new AlertDispatcher(
                context,
                store,
                message => WriteLog(writer, context, message));
            CircuitBreakerMonitor monitor = new(store, dispatcher, dispatcher.Command);
            LedgerIngestor ingestor = new(
                store,
                calculator,
                new CachingGitContextResolver(
                    new FileSystemGitContextResolver(),
                    context.TimeProvider),
                new CopilotSessionWorkspaceLocator(context.CopilotHome, context.TimeProvider),
                monitor);
            IngestFailureLog ingestFailures = new(
                message => WriteLog(writer, context, message),
                context.TimeProvider);
            app = OtlpReceiverApp.Build(
                new OtlpReceiverOptions { Port = port },
                ingestor,
                context.ConfigureWebHost,
                ingestFailures.Report);

            if (!OperatingSystem.IsWindows())
            {
                sighupRegistration = PosixSignalRegistration.Create(
                    PosixSignal.SIGHUP,
                    signal => signal.Cancel = true);
            }

            try
            {
                await dispatcher.EnqueuePendingAsync(cancellationToken);
                dispatching = dispatcher.RunAsync(dispatchStop.Token);
                await app.StartAsync(cancellationToken);
                started = true;
            }
            catch (IOException)
            {
                WriteLog(
                    writer,
                    context,
                    $"Port {port} is in use. Stop the other program, or run gh-ledger daemon run --port {port}.");
                return 1;
            }

            DaemonState state = new()
            {
                SchemaVersion = 1,
                ProcessId = Environment.ProcessId,
                StartedAt = context.TimeProvider.GetUtcNow(),
                Endpoint = $"http://localhost:{port}",
                Version = GhLedgerCli.Version,
                DatabasePath = context.DatabasePath,
                ImportRules = DaemonState.CurrentImportRules,
            };
            await DaemonFiles.WriteStateAsync(context, state, cancellationToken);
            WriteLog(
                writer,
                context,
                $"gh-ledger {GhLedgerCli.Version} daemon listening on http://localhost:{port} (pid {Environment.ProcessId}); ledger {context.DatabasePath}");

            using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                app.Lifetime.ApplicationStopping);
            while (!linked.IsCancellationRequested)
            {
                if (File.Exists(DaemonFiles.StopPath(context)))
                {
                    DaemonFiles.DeleteIfExists(DaemonFiles.StopPath(context));
                    break;
                }

                DateTimeOffset now = context.TimeProvider.GetUtcNow();
                if (now - lastPrune >= TimeSpan.FromHours(6))
                {
                    await store.PruneSpanLinksAsync(
                        now - TimeSpan.FromDays(7),
                        cancellationToken);
                    lastPrune = now;
                }

                try
                {
                    await Task.Delay(context.DaemonPollInterval, linked.Token);
                }
                catch (OperationCanceledException) when (linked.IsCancellationRequested)
                {
                    break;
                }
            }

            return 0;
        }
        finally
        {
            sighupRegistration?.Dispose();
            if (started && app is not null)
            {
                await app.StopAsync(CancellationToken.None);
            }

            if (dispatching is not null)
            {
                dispatcher!.Complete();
                if (await Task.WhenAny(
                    dispatching,
                    Task.Delay(TimeSpan.FromSeconds(5))) != dispatching)
                {
                    dispatchStop.Cancel();
                    await Task.WhenAny(
                        dispatching,
                        Task.Delay(TimeSpan.FromSeconds(1)));
                }

                if (dispatching.IsFaulted)
                {
                    WriteLog(
                        writer,
                        context,
                        $"Warning: the alert dispatcher stopped: {dispatching.Exception!.GetBaseException().Message}");
                }
            }

            dispatchStop.Dispose();
            DaemonFiles.DeleteIfExists(DaemonFiles.StatePath(context));
            if (app is not null)
            {
                await app.DisposeAsync();
            }

            lockStream.Dispose();
            if (started)
            {
                WriteLog(writer, context, "gh-ledger daemon stopped");
            }

            if (ownsWriter)
            {
                writer.Dispose();
            }
        }
    }

    private static TextWriter CreateLogWriter(
        CliContext context,
        string? logFile,
        out bool ownsWriter)
    {
        if (logFile is null)
        {
            ownsWriter = false;
            return context.Out;
        }

        string directory = Path.GetDirectoryName(Path.GetFullPath(logFile))!;
        Directory.CreateDirectory(directory);
        ownsWriter = true;
        return new StreamWriter(
            new FileStream(logFile, FileMode.Append, FileAccess.Write, FileShare.ReadWrite),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
        {
            AutoFlush = true,
        };
    }

    private static void WriteLog(TextWriter writer, CliContext context, string message) =>
        writer.WriteLine($"{context.TimeProvider.GetUtcNow():O} {message}");
}
