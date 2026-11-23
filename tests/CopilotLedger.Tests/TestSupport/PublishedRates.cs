using System.Text;
using System.Text.Json.Nodes;
using CopilotLedger.Core.Pricing;

namespace CopilotLedger.Tests.TestSupport;

public static class PublishedRates
{
    public const string Url = "https://github.com/swigerb/gh-ledger/releases/latest/download/rates.json";

    // The embedded rates.json text, exactly as built into CopilotLedger.Core.
    public static string Embedded()
    {
        using Stream stream = typeof(JsonRateCardCalculator).Assembly
            .GetManifestResourceStream(JsonRateCardCalculator.EmbeddedResourceName)!;
        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }

    // The embedded rates with "ratesVersion": "2026-11-23" replaced by ratesVersion.
    public static string WithVersion(string ratesVersion) =>
        Embedded().Replace(
            "\"ratesVersion\": \"2026-11-23\"",
            $"\"ratesVersion\": \"{ratesVersion}\"",
            StringComparison.Ordinal);

    // WithVersion(ratesVersion) with claude-opus-5.5's inputPer1M set to 8 and outputPer1M to 40 (twice the built-in rates).
    public static string DoubledOpus(string ratesVersion)
    {
        JsonNode document = JsonNode.Parse(WithVersion(ratesVersion))!;
        JsonNode tier = document["githubAiCredits"]!["models"]!.AsArray()
            .Single(model => model!["id"]!.GetValue<string>() == "claude-opus-5.5")!["windows"]![0]!["tiers"]![0]!;
        tier["inputPer1M"] = 8m;
        tier["outputPer1M"] = 40m;
        return document.ToJsonString();
    }

    public static byte[] Utf8(string text) => new UTF8Encoding(false).GetBytes(text);

    // WithVersion(ratesVersion) plus a GitHub model gpt-new, alias gpt-preview, at $1 input, $0.10 cached input, and
    // $4 output per 1M tokens.
    public static string WithGptNew(string ratesVersion)
    {
        JsonNode document = JsonNode.Parse(WithVersion(ratesVersion))!;
        document["githubAiCredits"]!["models"]!.AsArray().Add(JsonNode.Parse(
            """
            { "id": "gpt-new", "aliases": ["gpt-preview"], "windows": [ { "effectiveFrom": null, "effectiveUntil": null, "tiers": [ { "name": "default", "maxInputTokens": null, "inputPer1M": 1, "cachedInputPer1M": 0.1, "cacheWritePer1M": null, "outputPer1M": 4 } ] } ] }
            """));
        return document.ToJsonString();
    }

    // An overlay that adds the GitHub model gpt-preview. It's valid with the built-in rates and conflicts with WithGptNew.
    public const string GptPreviewOverlay = """
        { "schemaVersion": 1, "githubAiCredits": { "models": [ { "id": "gpt-preview", "aliases": [], "windows": [ { "effectiveFrom": null, "effectiveUntil": null, "tiers": [ { "name": "default", "maxInputTokens": null, "inputPer1M": 1, "cachedInputPer1M": 0.1, "cacheWritePer1M": null, "outputPer1M": 4 } ] } ] } ] } }
        """;
}
