using System.Globalization;
using CopilotLedger.Cli.Install;
using CopilotLedger.Core.Pricing;

namespace CopilotLedger.Cli.Rates;

// DownloadedJson: the text of rates.downloaded.json when it's the base, else null (the built-in rates are the base).
// Version: the base's ratesVersion. Warning: why an existing rates.downloaded.json was ignored, or null.
internal sealed record RatesBase(string? DownloadedJson, string Version, string? Warning);

internal static class RateCardFiles
{
    public static string DownloadedPath(CliContext context) =>
        Path.Combine(context.DataDirectory, "rates.downloaded.json");

    // The downloaded rates when rates.downloaded.json is valid, its ratesVersion date is later than the built-in one, and its
    // schemaVersion is at least the built-in one's; otherwise the built-in rates. A download that isn't later is ignored
    // without a warning, and a later download with an older schemaVersion is ignored with one, so a schemaVersion 1 file
    // never replaces built-in API-equivalent prices.
    public static RatesBase LoadBase(CliContext context)
    {
        JsonRateCardCalculator builtInRates = JsonRateCardCalculator.LoadEmbedded();
        string builtIn = builtInRates.RatesVersion;
        string path = DownloadedPath(context);
        if (!File.Exists(path))
        {
            return new RatesBase(null, builtIn, null);
        }

        string json;
        JsonRateCardCalculator downloadedRates;
        try
        {
            json = File.ReadAllText(path);
            downloadedRates = JsonRateCardCalculator.Parse(json);
        }
        catch (Exception ex) when (
            ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            return new RatesBase(null, builtIn, $"ignored {path}: {ex.Message}");
        }

        string version = downloadedRates.RatesVersion;
        if (!TryParseVersion(version, out DateOnly downloaded))
        {
            return new RatesBase(
                null,
                builtIn,
                $"ignored {path}: ratesVersion {version} isn't a yyyy-MM-dd date.");
        }

        if (downloaded <= ParseVersion(builtIn))
        {
            return new RatesBase(null, builtIn, null);
        }

        return downloadedRates.SchemaVersion < builtInRates.SchemaVersion
            ? new RatesBase(
                null,
                builtIn,
                $"ignored {path}: its schemaVersion {downloadedRates.SchemaVersion} rates have no API-equivalent prices; run {GhExtension.CommandName(context.Platform)} rates update to replace them.")
            : new RatesBase(json, version, null);
    }

    // The base rates with the overlay at overlayPath merged on top; null overlayPath means no overlay.
    // Throws InvalidDataException when the overlay is invalid; the message starts with overlayPath.
    public static JsonRateCardCalculator Load(RatesBase rates, string? overlayPath)
    {
        if (rates.DownloadedJson is not null)
        {
            return JsonRateCardCalculator.LoadWithOverlay(rates.DownloadedJson, overlayPath);
        }

        return overlayPath is null
            ? JsonRateCardCalculator.LoadEmbedded()
            : JsonRateCardCalculator.LoadEmbeddedWithOverlay(overlayPath);
    }

    // context.RatesOverlayPath when that file exists, else null.
    public static string? OverlayPath(CliContext context) =>
        File.Exists(context.RatesOverlayPath) ? context.RatesOverlayPath : null;

    public static bool TryParseVersion(string version, out DateOnly date) =>
        DateOnly.TryParseExact(
            version,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out date);

    public static DateOnly ParseVersion(string version) =>
        DateOnly.ParseExact(version, "yyyy-MM-dd", CultureInfo.InvariantCulture);
}
