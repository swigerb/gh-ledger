using System.Globalization;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Core.Telemetry;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Pricing;

public sealed class ReportedChargePricingTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse(
        "2026-11-20T00:00:00Z",
        CultureInfo.InvariantCulture);

    private static readonly JsonRateCardCalculator Calculator = JsonRateCardCalculator.LoadEmbedded();

    [Fact]
    public void RCP1_AValidReportedChargeReplacesTheGitHubChargeAndKeepsTheListPrice()
    {
        CreditCalculationResult rateCard = Calculator.Calculate(Chat());
        CreditCalculationResult result = Calculator.Calculate(Chat() with
        {
            ReportedCharge = ReportedCharge.Valid(47_388_600),
        });

        Assert.Equal(BillingChannel.GitHubAiCredits, result.Channel);
        Assert.Equal(PricingStatus.Priced, result.Status);
        Assert.Equal(0.000473886m, result.UsdCost);
        Assert.Equal(0.0473886m, result.AiCredits);
        Assert.Equal(ChargeSource.Reported, result.ChargeSource);
        Assert.Equal(rateCard.UsdCost, result.ListUsdCost);
        Assert.Equal(rateCard.Breakdown, result.Breakdown);
    }

    [Fact]
    public void RCP2_AnAbsentReportedChargeUsesTheRateCard()
    {
        CreditCalculationResult result = Calculator.Calculate(Chat());

        Assert.Equal(ChargeSource.RateCard, result.ChargeSource);
        Assert.Equal(result.UsdCost, result.ListUsdCost);
    }

    [Fact]
    public void RCP3_AMalformedReportedChargeIsRejectedAndUsesTheRateCard()
    {
        CreditCalculationResult rateCard = Calculator.Calculate(Chat());
        CreditCalculationResult result = Calculator.Calculate(Chat() with
        {
            ReportedCharge = ReportedCharge.Malformed,
        });

        Assert.Equal(rateCard with { ChargeSource = ChargeSource.Rejected }, result);
    }

    [Fact]
    public void RCP4_ANonInferenceSpanDoesNotCountAReportedCharge()
    {
        CreditCalculationResult result = Calculator.Calculate(Chat() with
        {
            OperationName = "invoke_agent",
            ReportedCharge = ReportedCharge.Valid(47_388_600),
        });

        Assert.Equal(PricingStatus.NotBillableOperation, result.Status);
        Assert.Equal(0m, result.UsdCost);
        Assert.Equal(ChargeSource.RateCard, result.ChargeSource);
    }

    [Fact]
    public void RCP5_ALocalRequestAcceptsZeroButRejectsAPositiveReportedCharge()
    {
        NormalizedGenAiSpan local = Chat() with { ServerAddress = "localhost" };

        CreditCalculationResult zero = Calculator.Calculate(local with
        {
            ReportedCharge = ReportedCharge.Valid(0),
        });
        CreditCalculationResult positive = Calculator.Calculate(local with
        {
            ReportedCharge = ReportedCharge.Valid(5),
        });

        Assert.Equal(ChargeSource.RateCard, zero.ChargeSource);
        Assert.Equal(0m, zero.UsdCost);
        Assert.Equal(ChargeSource.Rejected, positive.ChargeSource);
        Assert.Equal(0m, positive.UsdCost);
    }

    [Fact]
    public void RCP6_AnUnknownModelRejectsAReportedCharge()
    {
        CreditCalculationResult result = Calculator.Calculate(Chat() with
        {
            RequestModel = "zz-unknown",
            ResponseModel = "zz-unknown",
            ReportedCharge = ReportedCharge.Valid(47_388_600),
        });

        Assert.Equal(PricingStatus.UnknownModel, result.Status);
        Assert.Equal(ChargeSource.Rejected, result.ChargeSource);
        Assert.Equal(0m, result.UsdCost);
    }

    [Fact]
    public void RCP7_TheMaximumReportedChargeIsConvertedExactly()
    {
        CreditCalculationResult result = Calculator.Calculate(Chat() with
        {
            ReportedCharge = ReportedCharge.Valid(NanoAiu.MaxExact),
        });

        Assert.Equal(90071.99254740992m, result.UsdCost);
        Assert.Equal(9007199.254740992m, result.AiCredits);
    }

    [Fact]
    public void RCP8_AProviderBilledKnownLocalModelIsLocal()
    {
        CreditCalculationResult result = Calculator.Calculate(Provider("openai", "qwen3.8-27b-cuda-gpu:1"));

        Assert.Equal(BillingChannel.Local, result.Channel);
        Assert.Equal(PricingStatus.LocalOffload, result.Status);
        Assert.Equal("qwen3.8-27b", result.ModelId);
        Assert.True(result.EstimatedCloudSavedUsd > 0m);
        Assert.Equal(0m, result.UsdCost);
    }

    [Fact]
    public void RCP9_AProviderBilledUnknownLocalModelIsOtherProvider()
    {
        CreditCalculationResult result = Calculator.Calculate(Provider("openai", "gpt-oss-unlisted"));

        Assert.Equal(PricingStatus.OtherProvider, result.Status);
    }

    [Fact]
    public void RCP10_AFoundryProviderStaysFoundryWhenItsModelIsLocal()
    {
        JsonRateCardCalculator calculator = JsonRateCardCalculator.Parse(
            PublishedRates.Embedded(),
            """{ "schemaVersion": 1, "foundryByok": { "providerNames": ["contoso"] } }""");

        CreditCalculationResult result = calculator.Calculate(Provider("contoso", "qwen3.8-27b"));

        Assert.Equal(BillingChannel.FoundryByok, result.Channel);
    }

    private static NormalizedGenAiSpan Chat() =>
        TestSpans.Chat(
            startTime: Start,
            operationName: "chat",
            requestModel: "claude-haiku-4.5",
            responseModel: "claude-haiku-4.5",
            inputTokens: 1000,
            outputTokens: 100) with
        {
            BillingSource = BillingSource.Endpoint,
        };

    private static NormalizedGenAiSpan Provider(string provider, string model) =>
        Chat() with
        {
            BillingSource = BillingSource.Provider,
            ProviderName = provider,
            RequestModel = model,
            ResponseModel = model,
        };
}
