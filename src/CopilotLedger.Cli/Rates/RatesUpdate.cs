using System.Net;
using System.Text;
using CopilotLedger.Cli.Daemon;
using CopilotLedger.Cli.Install;
using CopilotLedger.Core.Pricing;

namespace CopilotLedger.Cli.Rates;

internal static class RatesUpdate
{
    public const string DownloadUrl =
        "https://github.com/swigerb/gh-ledger/releases/latest/download/rates.v2.json";

    // Releases before rates.v2.json attach only this schemaVersion 1 copy, which RunAsync takes only when it's older or
    // the same: a newer one would drop the API-equivalent prices.
    public const string LegacyDownloadUrl =
        "https://github.com/swigerb/gh-ledger/releases/latest/download/rates.json";

    private const int MaximumBytes = 1_048_576;

    private static readonly TimeSpan DownloadTimeout = TimeSpan.FromSeconds(30);

    // Uses the system proxy (HttpClient.DefaultProxy) and signs in to it with the current user's credentials.
    public static HttpMessageHandler CreateDefaultHandler() =>
        new SocketsHttpHandler
        {
            DefaultProxyCredentials = CredentialCache.DefaultCredentials,
        };

    public static async Task<int> RunAsync(CliContext context, CancellationToken cancellationToken)
    {
        byte[] bytes;
        string url = DownloadUrl;
        using (HttpClient client = new(
            context.CreateHttpHandler?.Invoke() ?? CreateDefaultHandler(),
            disposeHandler: true))
        {
            client.Timeout = DownloadTimeout;
            client.MaxResponseContentBufferSize = MaximumBytes;
            client.DefaultRequestHeaders.UserAgent.ParseAdd($"gh-ledger/{GhLedgerCli.Version}");
            try
            {
                HttpResponseMessage response = await client.GetAsync(url, cancellationToken);
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    // The latest release predates rates.v2.json.
                    response.Dispose();
                    url = LegacyDownloadUrl;
                    response = await client.GetAsync(url, cancellationToken);
                }

                using (response)
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        context.Error.WriteLine(
                            $"Could not download rates from {url}: HTTP {(int)response.StatusCode}.");
                        return 1;
                    }

                    bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                }
            }
            catch (HttpRequestException ex)
            {
                context.Error.WriteLine($"Could not download rates from {url}: {ex.Message}");
                return 1;
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                context.Error.WriteLine(
                    $"Could not download rates from {url}: no response within 30 seconds.");
                return 1;
            }
        }

        string json;
        try
        {
            json = new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            context.Error.WriteLine("The downloaded rates are invalid: the file isn't UTF-8.");
            return 1;
        }

        JsonRateCardCalculator published;
        try
        {
            published = JsonRateCardCalculator.Parse(json);
        }
        catch (InvalidDataException ex)
        {
            context.Error.WriteLine($"The downloaded rates are invalid: {ex.Message}");
            return 1;
        }

        string version = published.RatesVersion;
        if (!RateCardFiles.TryParseVersion(version, out DateOnly publishedOn))
        {
            context.Error.WriteLine(
                $"The downloaded rates are invalid: ratesVersion {version} isn't a yyyy-MM-dd date.");
            return 1;
        }

        RatesBase current = RateCardFiles.LoadBase(context);
        if (current.Warning is not null)
        {
            context.Out.WriteLine($"Warning: {current.Warning}");
        }

        DateOnly inUse = RateCardFiles.ParseVersion(current.Version);
        if (publishedOn < inUse)
        {
            context.Out.WriteLine(
                $"The published rates ({version}) are older than the rates in use ({current.Version}); nothing changed.");
            return 0;
        }

        if (publishedOn == inUse)
        {
            context.Out.WriteLine($"Rates are up to date ({current.Version}).");
            return 0;
        }

        int schemaVersion = JsonRateCardCalculator.LoadEmbedded().SchemaVersion;
        if (published.SchemaVersion < schemaVersion)
        {
            context.Error.WriteLine(
                $"The published rates ({version}) are schemaVersion {published.SchemaVersion}, without API-equivalent prices; this gh-ledger needs schemaVersion {schemaVersion}. Nothing changed.");
            return 1;
        }

        string? overlay = RateCardFiles.OverlayPath(context);
        if (overlay is not null)
        {
            try
            {
                JsonRateCardCalculator.LoadWithOverlay(json, overlay);
            }
            catch (InvalidDataException ex)
            {
                context.Error.WriteLine(
                    $"The downloaded rates ({version}) don't work with your rates overlay: {ex.Message}");
                return 1;
            }
        }

        string path = RateCardFiles.DownloadedPath(context);
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Directory.CreateDirectory(context.DataDirectory);
            await File.WriteAllTextAsync(temp, json, new UTF8Encoding(false), cancellationToken);
            File.Move(temp, path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            DaemonFiles.DeleteIfExists(temp);
            context.Error.WriteLine($"Could not write {path}: {ex.Message}");
            return 1;
        }

        context.Out.WriteLine($"Updated rates from {current.Version} to {version}.");
        if (DaemonFiles.IsHeld(context))
        {
            string command = GhExtension.CommandName(context.Platform);
            context.Out.WriteLine(
                $"Restart the daemon to price new spans with them: {command} daemon stop, then {command} daemon start.");
        }

        return 0;
    }
}
