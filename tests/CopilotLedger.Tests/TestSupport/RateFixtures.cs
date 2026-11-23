namespace CopilotLedger.Tests.TestSupport;

public static class RateFixtures
{
    public const string ByokTestRatesJson = """
        {
          "schemaVersion": 1,
          "ratesVersion": "test-fixture",
          "source": { "url": "https://example.invalid/rates", "retrievedOn": "2026-11-20" },
          "notes": ["Fictional rates for tests only."],
          "githubAiCredits": {
            "hostSuffixes": ["githubcopilot.com"],
            "models": [
              {
                "id": "claude-sonnet-5.5",
                "aliases": [],
                "windows": [
                  {
                    "effectiveFrom": null,
                    "effectiveUntil": null,
                    "tiers": [
                      { "name": "default", "maxInputTokens": null, "inputPer1M": 2.00, "cachedInputPer1M": 0.10, "cacheWritePer1M": 2.50, "outputPer1M": 10.00 }
                    ]
                  }
                ]
              }
            ]
          },
          "foundryByok": {
            "hostSuffixes": ["services.ai.azure.com", "openai.azure.com", "cognitiveservices.azure.com"],
            "providerNames": ["azure.ai.inference"],
            "models": [
              {
                "id": "gpt-6.1-sol",
                "aliases": [],
                "windows": [
                  {
                    "effectiveFrom": null,
                    "effectiveUntil": null,
                    "tiers": [
                      { "name": "default", "maxInputTokens": null, "inputPer1M": 1.75, "cachedInputPer1M": 0.175, "cacheWritePer1M": null, "outputPer1M": 14.00 }
                    ]
                  }
                ]
              }
            ]
          },
          "localOffload": {
            "baselineModel": "claude-sonnet-5.5",
            "models": [],
            "additionalHosts": [],
            "providerNames": []
          }
        }
        """;

    // A schemaVersion 2 card with fictional rates. GitHub prices test-retail and the local baseline test-baseline, and
    // Foundry BYOK prices gpt-6.1-sol (alias contoso-gpt61-prod). apiEquivalent prices test-retail in two windows with two
    // tiers each, test-baseline, test-no-cache without cache rates, and gpt-6.1-sol from 2026-06-01.
    public const string RetailTestRatesJson = """
        {
          "schemaVersion": 2,
          "ratesVersion": "retail-fixture",
          "source": { "url": "https://example.invalid/rates", "retrievedOn": "2026-11-23" },
          "notes": ["Fictional rates for tests only."],
          "githubAiCredits": {
            "hostSuffixes": ["githubcopilot.com"],
            "models": [
              { "id": "test-retail", "aliases": [], "windows": [ { "effectiveFrom": null, "effectiveUntil": null, "tiers": [ { "name": "default", "maxInputTokens": null, "inputPer1M": 1.00, "cachedInputPer1M": 0.10, "cacheWritePer1M": 1.25, "outputPer1M": 5.00 } ] } ] },
              { "id": "test-baseline", "aliases": [], "windows": [ { "effectiveFrom": null, "effectiveUntil": null, "tiers": [ { "name": "default", "maxInputTokens": null, "inputPer1M": 0.50, "cachedInputPer1M": 0.05, "cacheWritePer1M": null, "outputPer1M": 2.50 } ] } ] }
            ]
          },
          "foundryByok": {
            "hostSuffixes": ["services.ai.azure.com", "openai.azure.com", "cognitiveservices.azure.com"],
            "providerNames": ["azure.ai.inference"],
            "models": [
              { "id": "gpt-6.1-sol", "aliases": ["contoso-gpt61-prod"], "windows": [ { "effectiveFrom": null, "effectiveUntil": null, "tiers": [ { "name": "default", "maxInputTokens": null, "inputPer1M": 1.75, "cachedInputPer1M": 0.175, "cacheWritePer1M": null, "outputPer1M": 14.00 } ] } ] }
            ]
          },
          "localOffload": {
            "baselineModel": "test-baseline",
            "models": [],
            "additionalHosts": [],
            "providerNames": ["ollama"]
          },
          "apiEquivalent": {
            "description": "Fictional retail prices for tests only.",
            "models": [
              {
                "id": "test-retail",
                "vendor": "test-vendor",
                "sourceUrl": "https://example.invalid/pricing",
                "aliases": ["test-retail-2026-11-01"],
                "windows": [
                  {
                    "effectiveFrom": null,
                    "effectiveUntil": "2026-12-01T00:00:00Z",
                    "tiers": [
                      { "name": "default", "maxInputTokens": 200000, "inputPer1M": 3.00, "cachedInputPer1M": 0.30, "cacheWritePer1M": 3.75, "outputPer1M": 15.00 },
                      { "name": "long_context", "maxInputTokens": null, "inputPer1M": 6.00, "cachedInputPer1M": 0.60, "cacheWritePer1M": 7.50, "outputPer1M": 22.50 }
                    ]
                  },
                  {
                    "effectiveFrom": "2026-12-01T00:00:00Z",
                    "effectiveUntil": null,
                    "tiers": [
                      { "name": "default", "maxInputTokens": 200000, "inputPer1M": 4.00, "cachedInputPer1M": 0.40, "cacheWritePer1M": 5.00, "outputPer1M": 20.00 },
                      { "name": "long_context", "maxInputTokens": null, "inputPer1M": 8.00, "cachedInputPer1M": 0.80, "cacheWritePer1M": 10.00, "outputPer1M": 30.00 }
                    ]
                  }
                ]
              },
              { "id": "test-baseline", "vendor": "test-vendor", "sourceUrl": "https://example.invalid/pricing", "aliases": [], "windows": [ { "effectiveFrom": null, "effectiveUntil": null, "tiers": [ { "name": "default", "maxInputTokens": null, "inputPer1M": 0.80, "cachedInputPer1M": 0.08, "cacheWritePer1M": 1.00, "outputPer1M": 3.20 } ] } ] },
              { "id": "test-no-cache", "vendor": "test-vendor", "sourceUrl": "https://example.invalid/pricing", "aliases": [], "windows": [ { "effectiveFrom": null, "effectiveUntil": null, "tiers": [ { "name": "default", "maxInputTokens": null, "inputPer1M": 2.00, "cachedInputPer1M": null, "cacheWritePer1M": null, "outputPer1M": 8.00 } ] } ] },
              { "id": "gpt-6.1-sol", "vendor": "test-vendor", "sourceUrl": "https://example.invalid/pricing", "aliases": [], "windows": [ { "effectiveFrom": "2026-06-01T00:00:00Z", "effectiveUntil": null, "tiers": [ { "name": "default", "maxInputTokens": null, "inputPer1M": 2.00, "cachedInputPer1M": 0.10, "cacheWritePer1M": 2.50, "outputPer1M": 10.00 } ] } ] }
            ]
          }
        }
        """;
}
