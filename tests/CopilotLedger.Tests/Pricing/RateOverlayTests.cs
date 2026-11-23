using CopilotLedger.Core.Pricing;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Pricing;

public sealed class RateOverlayTests
{
    private const string OverlayA = """
        {
          "schemaVersion": 1,
          "ratesVersion": "contoso-2026-11",
          "notes": ["Azure prices from our November 2026 invoice."],
          "foundryByok": {
            "models": [
              {
                "id": "gpt-6.1-sol",
                "aliases": ["contoso-gpt61-prod"],
                "windows": [
                  { "tiers": [ { "name": "default", "inputPer1M": 1.75, "cachedInputPer1M": 0.175, "outputPer1M": 14.00 } ] }
                ]
              }
            ]
          }
        }
        """;

    // Each expected message uses <file> for the overlay's path.
    public static TheoryData<string, string> InvalidOverlays => new()
    {
        // Sprint 1's invalid overlays, now with exact messages.
        { """{ "schemaVersion": 3 }""", "<file>: schemaVersion: must be 1 or 2" },
        { """{ "schemaVersion": 1, "foundryBYOK": { } }""", "<file>: foundryBYOK: is not a known property; did you mean foundryByok?" },
        { """{ "schemaVersion": 1, "localOffload": { "baselineModel": "gpt-missing" } }""", "<file>: localOffload.baselineModel: does not resolve to a githubAiCredits model in the merged rates: gpt-missing" },
        { """{ "schemaVersion": 1, "foundryByok": { "models": [ { "id": "gpt-6.1-sol", "windows": [ { "tiers": [ { "name": "default", "inputPer1M": 1.75 } ] } ] } ] } }""", "<file>: foundryByok.models[0].windows[0].tiers[0].outputPer1M: is required" },
        { """{ }""", "<file>: schemaVersion: is required" },
        { """{ "schemaVersion": 1, "localOffload": { "models": [ { "id": "my-local", "aliases": ["qwen3.8:27b"] } ] } }""", "<file>: localOffload.models[0].aliases[0]: duplicate model identity in the merged rates: qwen3.8:27b (built-in model qwen3.8-27b)" },

        // Invalid JSON, and a root that isn't an object.
        { "{ \"schemaVersion\": 1,\n  \"foundryByok\": { \"models\": [ }\n}\n", "<file>: line 2, column 32: invalid JSON" },
        { "{ \"schemaVersion\": 1,\r\n  \"foundryByok\": { \"models\": [ }\r\n}\r\n", "<file>: line 2, column 32: invalid JSON" },
        { """{ "schemaVersion": 1, }""", "<file>: line 1, column 23: invalid JSON" },
        { "", "<file>: line 1, column 1: invalid JSON" },
        { """{ "schemaVersion": 1 } // note""", "<file>: line 1, column 24: invalid JSON" },
        { "[]", "<file>: must contain a JSON object" },

        // schemaVersion is checked first.
        { """{ "schemaVersion": null }""", "<file>: schemaVersion: is required" },
        { """{ "schemaVersion": "1" }""", "<file>: schemaVersion: must be an integer" },
        { """{ "foundryBYOK": { }, "schemaVersion": 3 }""", "<file>: schemaVersion: must be 1 or 2" },

        // Unknown and repeated properties.
        { """{ "schemaVersion": 1, "currency": "USD" }""", "<file>: currency: is not a known property" },
        { """{ "schemaVersion": 1, "foundryByok": { "models": [ { "id": "gpt-6.1-sol", "windows": [ { "tiers": [ { "name": "default", "inputPer1m": 1.75, "outputPer1M": 14 } ] } ] } ] } }""", "<file>: foundryByok.models[0].windows[0].tiers[0].inputPer1m: is not a known property; did you mean inputPer1M?" },
        { """{ "schemaVersion": 1, "localOffload": { }, "localOffload": { } }""", "<file>: localOffload: is defined more than once" },
        { """{ "schemaVersion": 1, "githubAiCredits": { "models": [ { "id": "contoso-new", "windows": [ { "tiers": [ { "name": "default", "name": "other", "inputPer1M": 1, "outputPer1M": 1 } ] } ] } ] } }""", "<file>: githubAiCredits.models[0].windows[0].tiers[0].name: is defined more than once" },

        // A missing required property is reported at its own path.
        { """{ "schemaVersion": 1, "githubAiCredits": { "models": [ { "id": "contoso-new" } ] } }""", "<file>: githubAiCredits.models[0].windows: is required" },
        { """{ "schemaVersion": 1, "githubAiCredits": { "models": [ { "id": "contoso-new", "windows": [ { "effectiveFrom": null } ] } ] } }""", "<file>: githubAiCredits.models[0].windows[0].tiers: is required" },
        { """{ "schemaVersion": 1, "localOffload": { "models": [ { "aliases": [] } ] } }""", "<file>: localOffload.models[0].id: is required" },

        // Values of the wrong kind.
        { """{ "schemaVersion": 1, "githubAiCredits": [] }""", "<file>: githubAiCredits: must be an object" },
        { """{ "schemaVersion": 1, "foundryByok": { "hostSuffixes": "contoso.example" } }""", "<file>: foundryByok.hostSuffixes: must be an array" },
        { """{ "schemaVersion": 1, "foundryByok": { "providerNames": [1] } }""", "<file>: foundryByok.providerNames[0]: must be a string" },
        { """{ "schemaVersion": 1, "githubAiCredits": { "models": [ { "id": "contoso-new", "windows": [ { "tiers": [ { "name": "default", "inputPer1M": "1.75", "outputPer1M": 1 } ] } ] } ] } }""", "<file>: githubAiCredits.models[0].windows[0].tiers[0].inputPer1M: must be a number" },
        { """{ "schemaVersion": 1, "githubAiCredits": { "models": [ { "id": "contoso-new", "windows": [ { "tiers": [ { "name": "small", "maxInputTokens": 1.5, "inputPer1M": 1, "outputPer1M": 1 }, { "name": "large", "inputPer1M": 2, "outputPer1M": 2 } ] } ] } ] } }""", "<file>: githubAiCredits.models[0].windows[0].tiers[0].maxInputTokens: must be an integer" },
        { """{ "schemaVersion": 1, "githubAiCredits": { "models": [ { "id": "contoso-new", "windows": [ { "effectiveFrom": "May 2027", "tiers": [ { "name": "default", "inputPer1M": 1, "outputPer1M": 1 } ] } ] } ] } }""", "<file>: githubAiCredits.models[0].windows[0].effectiveFrom: must be a date and time, such as 2027-05-17T00:00:00Z" },
        { """{ "schemaVersion": 1, "githubAiCredits": { "models": [ { "id": null, "windows": [] } ] } }""", "<file>: githubAiCredits.models[0].id: must be a string" },
        { """{ "schemaVersion": 1, "localOffload": { "models": [ null ] } }""", "<file>: localOffload.models[0]: must be an object" },
        { """{ "schemaVersion": 1, "githubAiCredits": { "models": [ { "id": "contoso-new", "windows": [ { "tiers": [ { "name": "default", "inputPer1M": 1e30, "outputPer1M": 1 } ] } ] } ] } }""", "<file>: githubAiCredits.models[0].windows[0].tiers[0].inputPer1M: is too large" },
        { """{ "schemaVersion": 1, "githubAiCredits": { "models": [ { "id": "contoso-new", "windows": [ { "tiers": [ { "name": "default", "inputPer1M": 1, "outputPer1M": null } ] } ] } ] } }""", "<file>: githubAiCredits.models[0].windows[0].tiers[0].outputPer1M: must be a number" },
        { """{ "schemaVersion": 1, "localOffload": { "models": [ { "id": "\ud800" } ] } }""", "<file>: localOffload.models[0].id: is not valid text" },
        { """{ "schemaVersion": 1, "localOffload": { "\ud800": 1 } }""", "<file>: localOffload: contains a property name that is not valid text" },
        { """{ "schemaVersion": 1, "localOffload": { "models": [ { "id": "my-local", "aliases": null } ] } }""", "<file>: localOffload.models[0].aliases: must be an array" },
        { """{ "schemaVersion": 1, "notes": [null] }""", "<file>: notes[0]: must be a string" },
        { """{ "schemaVersion": 1, "ratesVersion": 5 }""", "<file>: ratesVersion: must be a string" },

        // Rule errors at paths into the overlay. Sprint 1 reported most of these at merged indexes.
        { """{ "schemaVersion": 1, "localOffload": { "models": [ { "id": " " } ] } }""", "<file>: localOffload.models[0].id: must not be blank" },
        { """{ "schemaVersion": 1, "githubAiCredits": { "models": [ { "id": "contoso-new", "windows": [ { "tiers": [ { "name": "default", "inputPer1M": 1, "outputPer1M": 1.0000001 } ] } ] } ] } }""", "<file>: githubAiCredits.models[0].windows[0].tiers[0].outputPer1M: must have no more than 6 decimal places" },
        { """{ "schemaVersion": 1, "githubAiCredits": { "models": [ { "id": "mai-code-2", "windows": [ { "effectiveFrom": "2027-05-17T00:00:00Z", "tiers": [ { "name": "default", "inputPer1M": 1, "outputPer1M": 5 } ] }, { "effectiveUntil": "2027-05-17T00:00:00Z", "tiers": [ { "name": "default", "inputPer1M": 0.5, "outputPer1M": 2.5 } ] } ] } ] } }""", "<file>: githubAiCredits.models[0].windows[1].effectiveFrom: windows must be sorted and must not overlap" },
        { """{ "schemaVersion": 1, "foundryByok": { "hostSuffixes": [".contoso.example"] } }""", "<file>: foundryByok.hostSuffixes[0]: must be a host suffix without a leading '.', '/', or ':'" },
        { """{ "schemaVersion": 1, "localOffload": { "baselineModel": " " } }""", "<file>: localOffload.baselineModel: must not be blank" },
        { """{ "schemaVersion": 1, "githubAiCredits": { "models": [ { "id": "contoso-new", "windows": [ { "tiers": [ { "name": "default", "maxInputTokens": 1000, "inputPer1M": 1, "outputPer1M": 1 } ] } ] } ] } }""", "<file>: githubAiCredits.models[0].windows[0].tiers[0].maxInputTokens: the last tier must be unbounded" },
        { """{ "schemaVersion": 1, "githubAiCredits": { "models": [ { "id": "contoso-new", "windows": [ { "tiers": [ { "name": "default", "inputPer1M": -1, "outputPer1M": 1 } ] } ] } ] } }""", "<file>: githubAiCredits.models[0].windows[0].tiers[0].inputPer1M: must be greater than or equal to 0" },
        { """{ "schemaVersion": 1, "githubAiCredits": { "models": [ { "id": "contoso-new", "windows": [ { "tiers": [ { "name": "default", "inputPer1M": 1000000.000001, "outputPer1M": 1 } ] } ] } ] } }""", "<file>: githubAiCredits.models[0].windows[0].tiers[0].inputPer1M: must be less than or equal to 1000000" },
        { """{ "schemaVersion": 1, "githubAiCredits": { "models": [ { "id": "contoso-new", "aliases": ["Contoso New"], "windows": [ { "tiers": [ { "name": "default", "inputPer1M": 1, "outputPer1M": 1 } ] } ] } ] } }""", "<file>: githubAiCredits.models[0].aliases[0]: duplicate model identity: contoso-new" },

        // Checks against the merged rates.
        { """{ "schemaVersion": 1, "localOffload": { "models": [ { "id": "Qwen-3.8-27B" } ] } }""", "<file>: localOffload.models[0].id: duplicate model identity in the merged rates: qwen-3.8-27b (built-in model qwen3.8-27b)" },
        { """{ "schemaVersion": 1, "githubAiCredits": { "models": [ { "id": "claude-opus-5.5", "aliases": ["MAI Code 2"], "windows": [ { "tiers": [ { "name": "default", "inputPer1M": 4, "outputPer1M": 20 } ] } ] } ] } }""", "<file>: githubAiCredits.models[0].aliases[0]: duplicate model identity in the merged rates: mai-code-2 (built-in model mai-code-2)" },
        { """{ "schemaVersion": 1, "foundryByok": { "models": [ { "id": "gpt-6.1-sol", "windows": [ { "tiers": [ { "name": "default", "inputPer1M": 1.75, "outputPer1M": 14 } ] } ] } ] }, "githubAiCredits": { "models": [ { "id": "my-sol", "aliases": ["gpt-6.1-sol"], "windows": [ { "tiers": [ { "name": "default", "inputPer1M": 1, "outputPer1M": 1 } ] } ] } ] } }""", "<file>: githubAiCredits.models[0].aliases[0]: duplicate model identity in the merged rates: gpt-6.1-sol (built-in model gpt-6.1-sol)" },

        // Reporting order: reading, then the file's own rules, then the merged rates; document order while reading.
        { """{ "schemaVersion": 1, "localOffload": { "additionalHosts": [" "], "providerNames": [1] } }""", "<file>: localOffload.providerNames[0]: must be a string" },
        { """{ "schemaVersion": 1, "localOffload": { "baselineModel": "gpt-missing", "additionalHosts": [" "] } }""", "<file>: localOffload.additionalHosts[0]: must not be blank" },
        { """{ "schemaVersion": 1, "githubAiCredits": { "models": [ { "id": "contoso-new", "windows": [ { "tiers": [ { "outputPer1M": "x", "inputPer1M": "y", "name": "default" } ] } ] } ] } }""", "<file>: githubAiCredits.models[0].windows[0].tiers[0].outputPer1M: must be a number" },
        { """{ "schemaVersion": 1, "githubAiCredits": { "models": [ { "id": "contoso-new", "windows": [ { "tiers": [ { "name": "default", "inputPer1M": 1 } ] } ] } ], "hostSuffixes": [".bad"] } }""", "<file>: githubAiCredits.models[0].windows[0].tiers[0].outputPer1M: is required" },

        // Architect amendments (PR #15 review).
        { """{ "schemaVersion": null, "schemaVersion": 1 }""", "<file>: schemaVersion: is defined more than once" },
        { """{ "schemaVersion": 1, "\ud800abcdefgh": 1 }""", "<file>: contains a property name that is not valid text" },
        { """{ "schemaVersion": 1, "githubAiCredits": { "models": [ { "id": "contoso-new", "windows": [ { "effectiveFrom": "2027-05-17\ud800", "tiers": [ { "name": "default", "inputPer1M": 1, "outputPer1M": 1 } ] } ] } ] } }""", "<file>: githubAiCredits.models[0].windows[0].effectiveFrom: must be a date and time, such as 2027-05-17T00:00:00Z" },
    };

    public static TheoryData<string, decimal, decimal> GitHubModelOverlays => new()
    {
        { "example-new-model", 5.00m, 500m },
        { "claude-opus-5.5", 18.00m, 1800m },
    };

    [Theory]
    [InlineData("gpt-6.1-sol")]
    [InlineData("contoso-gpt61-prod")]
    public void Overlay_AddsByokModelAndAlias(string model)
    {
        using TempDirectory temp = new();
        string path = WriteOverlay(temp, OverlayA);
        CreditCalculationResult result = JsonRateCardCalculator.LoadEmbeddedWithOverlay(path).Calculate(
            TestSpans.Chat(
                serverAddress: "contoso.openai.azure.com",
                requestModel: model,
                inputTokens: 100_000,
                outputTokens: 10_000));

        Assert.Equal(BillingChannel.FoundryByok, result.Channel);
        Assert.Equal(PricingStatus.Priced, result.Status);
        Assert.Equal("gpt-6.1-sol", result.ModelId);
        Assert.Equal(0.315m, result.UsdCost);
        Assert.Equal(0m, result.AiCredits);
        Assert.Equal("2026-11-23+contoso-2026-11", result.RatesVersion);
    }

    [Fact]
    public void Overlay_DoesNotChangeGitHubPrices()
    {
        using TempDirectory temp = new();
        JsonRateCardCalculator calculator = JsonRateCardCalculator.LoadEmbeddedWithOverlay(WriteOverlay(temp, OverlayA));
        CreditCalculationResult result = calculator.Calculate(
            TestSpans.Chat(
                requestModel: "claude-sonnet-5.5",
                inputTokens: 10_000,
                outputTokens: 2_000));
        Assert.Equal(BillingChannel.GitHubAiCredits, result.Channel);
        Assert.Equal(PricingStatus.Priced, result.Status);
        Assert.Equal(0.04m, result.UsdCost);
    }

    [Fact]
    public void Overlay_CanReplaceLocalBaseline()
    {
        using TempDirectory temp = new();
        JsonRateCardCalculator calculator = JsonRateCardCalculator.LoadEmbeddedWithOverlay(
            WriteOverlay(temp, """{ "schemaVersion": 1, "localOffload": { "baselineModel": "claude-sonnet-5.5" } }"""));

        CreditCalculationResult result = calculator.Calculate(TestSpans.Chat(
            serverAddress: "127.0.0.1",
            requestModel: "qwen3.8:27b",
            inputTokens: 400_000,
            outputTokens: 12_000));
        Assert.Equal(PricingStatus.LocalOffload, result.Status);
        Assert.Equal(0.92m, result.EstimatedCloudSavedUsd);
        Assert.Equal("claude-sonnet-5.5", result.RateCardModelId);
        Assert.Equal("2026-11-23+user", result.RatesVersion);
    }

    [Fact]
    public void Overlay_CanAddLocalHost()
    {
        using TempDirectory temp = new();
        JsonRateCardCalculator calculator = JsonRateCardCalculator.LoadEmbeddedWithOverlay(
            WriteOverlay(temp, """{ "schemaVersion": 1, "localOffload": { "additionalHosts": ["gpu-box.lan"] } }"""));
        CreditCalculationResult result = calculator.Calculate(
            TestSpans.Chat(
                serverAddress: "gpu-box.lan",
                requestModel: "qwen3.8:27b",
                inputTokens: 400_000,
                outputTokens: 12_000));
        Assert.Equal(BillingChannel.Local, result.Channel);
        Assert.Equal(PricingStatus.LocalOffload, result.Status);
        Assert.Equal(0.23m, result.EstimatedCloudSavedUsd);
    }

    [Fact]
    public void Overlay_LocalHostsAreExactAndMayContainColons()
    {
        using TempDirectory temp = new();
        JsonRateCardCalculator calculator = JsonRateCardCalculator.LoadEmbeddedWithOverlay(
            WriteOverlay(temp, """{ "schemaVersion": 1, "localOffload": { "additionalHosts": ["lan", "fd00::5"] } }"""));

        CreditCalculationResult subdomain = calculator.Calculate(
            TestSpans.Chat(
                serverAddress: "foo.lan",
                requestModel: "phi-local-test",
                inputTokens: 400_000,
                outputTokens: 12_000));
        CreditCalculationResult ipv6 = calculator.Calculate(
            TestSpans.Chat(
                serverAddress: "fd00::5",
                requestModel: "phi-local-test",
                inputTokens: 400_000,
                outputTokens: 12_000));

        Assert.Equal(BillingChannel.Unpriced, subdomain.Channel);
        Assert.Equal(PricingStatus.UnrecognizedEndpoint, subdomain.Status);
        Assert.Equal(BillingChannel.Local, ipv6.Channel);
        Assert.Equal(PricingStatus.LocalOffload, ipv6.Status);
    }

    [Theory]
    [MemberData(nameof(GitHubModelOverlays))]
    public void Overlay_CanAddOrReplaceGitHubModel(string model, decimal usd, decimal credits)
    {
        string overlay = model == "example-new-model"
            ? """{ "schemaVersion": 1, "githubAiCredits": { "models": [ { "id": "example-new-model", "windows": [ { "tiers": [ { "name": "default", "inputPer1M": 1.00, "outputPer1M": 4.00 } ] } ] } ] } }"""
            : """{ "schemaVersion": 1, "githubAiCredits": { "models": [ { "id": "claude-opus-5.5", "windows": [ { "tiers": [ { "name": "default", "inputPer1M": 3.00, "cachedInputPer1M": 0.30, "cacheWritePer1M": 3.75, "outputPer1M": 15.00 } ] } ] } ] } }""";
        using TempDirectory temp = new();
        CreditCalculationResult result = JsonRateCardCalculator.LoadEmbeddedWithOverlay(WriteOverlay(temp, overlay)).Calculate(
            TestSpans.Chat(
                requestModel: model,
                inputTokens: 1_000_000,
                outputTokens: 1_000_000));

        Assert.Equal(BillingChannel.GitHubAiCredits, result.Channel);
        Assert.Equal(PricingStatus.Priced, result.Status);
        Assert.Equal(usd, result.UsdCost);
        Assert.Equal(credits, result.AiCredits);
    }

    [Fact]
    public void Overlay_ReplacesLocalModelAliases()
    {
        using TempDirectory temp = new();
        JsonRateCardCalculator calculator = JsonRateCardCalculator.LoadEmbeddedWithOverlay(
            WriteOverlay(temp, """{ "schemaVersion": 1, "localOffload": { "models": [ { "id": "qwen3.8-27b", "aliases": ["my-qwen-build"] } ] } }"""));
        CreditCalculationResult result = calculator.Calculate(
            TestSpans.Chat(
                requestModel: "my-qwen-build",
                inputTokens: 400_000,
                outputTokens: 12_000));

        Assert.Equal(BillingChannel.Local, result.Channel);
        Assert.Equal("qwen3.8-27b", result.ModelId);
        Assert.Equal(0.23m, result.EstimatedCloudSavedUsd);
    }

    [Theory]
    [MemberData(nameof(InvalidOverlays))]
    public void InvalidOverlay_ReportsExactMessage(string overlay, string expected)
    {
        using TempDirectory temp = new();
        string path = WriteOverlay(temp, overlay);
        InvalidDataException exception = Assert.Throws<InvalidDataException>(
            () => JsonRateCardCalculator.LoadEmbeddedWithOverlay(path));
        Assert.Equal(expected.Replace("<file>", path, StringComparison.Ordinal), exception.Message);
    }

    [Fact]
    public void MissingOverlay_ThrowsFileNotFoundException()
    {
        using TempDirectory temp = new();
        string path = System.IO.Path.Combine(temp.Path, "missing.json");
        Assert.Throws<FileNotFoundException>(() => JsonRateCardCalculator.LoadEmbeddedWithOverlay(path));
    }

    [Fact]
    public void Overlay_DuplicateNormalizedIdsThrow()
    {
        using TempDirectory temp = new();
        string path = WriteOverlay(
            temp,
            """{ "schemaVersion": 1, "githubAiCredits": { "models": [ { "id": "duplicate", "windows": [ { "tiers": [ { "name": "default", "inputPer1M": 1, "outputPer1M": 1 } ] } ] }, { "id": "Duplicate", "windows": [ { "tiers": [ { "name": "default", "inputPer1M": 1, "outputPer1M": 1 } ] } ] } ] } }""");

        InvalidDataException exception = Assert.Throws<InvalidDataException>(
            () => JsonRateCardCalculator.LoadEmbeddedWithOverlay(path));
        Assert.Equal($"{path}: githubAiCredits.models[1].id: duplicate model identity: duplicate", exception.Message);
    }

    [Fact]
    public void Overlay_ReplacingBuiltInModelFreesItsAliases()
    {
        using TempDirectory temp = new();
        JsonRateCardCalculator calculator = JsonRateCardCalculator.LoadEmbeddedWithOverlay(
            WriteOverlay(temp, """{ "schemaVersion": 1, "localOffload": { "models": [ { "id": "qwen3.8-27b", "aliases": ["my-qwen-build"] }, { "id": "my-local", "aliases": ["qwen3.8:27b"] } ] } }"""));

        CreditCalculationResult result = calculator.Calculate(
            TestSpans.Chat(requestModel: "qwen3.8:27b", inputTokens: 400_000, outputTokens: 12_000));

        Assert.Equal(PricingStatus.LocalOffload, result.Status);
        Assert.Equal("my-local", result.ModelId);
    }

    [Fact]
    public void Overlay_BaselineCanNameAnOverlayModelByAlias()
    {
        using TempDirectory temp = new();
        JsonRateCardCalculator calculator = JsonRateCardCalculator.LoadEmbeddedWithOverlay(
            WriteOverlay(temp, """{ "schemaVersion": 1, "githubAiCredits": { "models": [ { "id": "contoso-new", "aliases": ["contoso-alias"], "windows": [ { "tiers": [ { "name": "default", "inputPer1M": 1, "outputPer1M": 1 } ] } ] } ] }, "localOffload": { "baselineModel": "Contoso Alias" } }"""));

        CreditCalculationResult result = calculator.Calculate(
            TestSpans.Chat(
                serverAddress: "127.0.0.1",
                requestModel: "qwen3.8:27b",
                inputTokens: 400_000,
                outputTokens: 12_000));

        Assert.Equal("contoso-new", result.RateCardModelId);
        Assert.Equal(0.412m, result.EstimatedCloudSavedUsd);
    }

    [Fact]
    public void Overlay_RateAtMaximum_Loads()
    {
        using TempDirectory temp = new();
        JsonRateCardCalculator calculator = JsonRateCardCalculator.LoadEmbeddedWithOverlay(
            WriteOverlay(temp, """{ "schemaVersion": 1, "githubAiCredits": { "models": [ { "id": "contoso-new", "windows": [ { "tiers": [ { "name": "default", "inputPer1M": 1000000, "outputPer1M": 1 } ] } ] } ] } }"""));

        CreditCalculationResult result = calculator.Calculate(
            TestSpans.Chat(requestModel: "contoso-new", inputTokens: 1));

        Assert.Equal(PricingStatus.Priced, result.Status);
        Assert.Equal(1m, result.UsdCost);
    }

    [Fact]
    public void DocsExamples_MatchRealMessages()
    {
        string[] lines = File.ReadAllLines(
            System.IO.Path.Combine(AppContext.BaseDirectory, "docs", "configuration.md"));
        List<(IReadOnlyList<string> Json, string Message)> examples = [];
        for (int index = 0; index < lines.Length; index++)
        {
            if (lines[index] != "<!-- rates-error-example -->")
            {
                continue;
            }

            int end = Array.FindIndex(lines, index + 1, line => line == "<!-- /rates-error-example -->");
            Assert.True(end >= 0);
            int jsonStart = Array.FindIndex(lines, index + 1, end - index - 1, line => line == "```json");
            Assert.True(jsonStart >= 0);
            int jsonEnd = Array.FindIndex(lines, jsonStart + 1, end - jsonStart - 1, line => line == "```");
            Assert.True(jsonEnd >= 0);
            int textStart = Array.FindIndex(lines, jsonEnd + 1, end - jsonEnd - 1, line => line == "```text");
            Assert.True(textStart >= 0);
            int textEnd = Array.FindIndex(lines, textStart + 1, end - textStart - 1, line => line == "```");
            Assert.Equal(textStart + 2, textEnd);

            examples.Add((lines[(jsonStart + 1)..jsonEnd], lines[textStart + 1]));
            index = end;
        }

        Assert.Equal(3, examples.Count);
        foreach ((IReadOnlyList<string> json, string message) in examples)
        {
            using TempDirectory temp = new();
            string path = WriteOverlay(temp, string.Join('\n', json) + "\n");
            InvalidDataException exception = Assert.Throws<InvalidDataException>(
                () => JsonRateCardCalculator.LoadEmbeddedWithOverlay(path));
            Assert.Equal(
                message.Replace(
                    "/home/me/.copilot-ledger/rates.json",
                    path,
                    StringComparison.Ordinal),
                exception.Message);
        }
    }

    [Fact]
    public void Overlay_RatesVersionIsNotTrimmed()
    {
        using TempDirectory temp = new();
        JsonRateCardCalculator calculator = JsonRateCardCalculator.LoadEmbeddedWithOverlay(
            WriteOverlay(temp, """{ "schemaVersion": 1, "ratesVersion": " custom " }"""));

        Assert.Equal("2026-11-23+ custom ", calculator.RatesVersion);
    }

    private static string WriteOverlay(TempDirectory temp, string overlay)
    {
        string path = System.IO.Path.Combine(temp.Path, "rates.json");
        File.WriteAllText(path, overlay);
        return path;
    }
}
