using CopilotLedger.Core.Pricing;
using CopilotLedger.Core.Telemetry;

namespace CopilotLedger.Tests.Telemetry;

public sealed class UsageIdentityTests
{
    private const string ServiceRequestId = "1f0e9c2a-7b3d-4e5f-8a9b-0c1d2e3f4a5b";

    [Theory]
    [InlineData("1f0e9c2a-7b3d-4e5f-8a9b-0c1d2e3f4a5b", "e60cf189bf4c90ec992e7763f23f3af37a01a61852a1e3ec40acaf1618ab6ae6")]
    [InlineData("A0B1C2D3-E4F5-4A6B-8C7D-9E0F1A2B3C4D", "314c4e22f41a318c9f76f436ee6f115046a33276156c566526f215c3028be845")]
    [InlineData("a0b1c2d3-e4f5-4a6b-8c7d-9e0f1a2b3c4d", "314c4e22f41a318c9f76f436ee6f115046a33276156c566526f215c3028be845")]
    public void UI1_AGitHubBilledRequestIsIdentifiedByItsServiceRequestId(string serviceRequestId, string expected)
    {
        Assert.Equal(
            expected,
            UsageIdentity.CanonicalUsageId("github", serviceRequestId, BillingChannel.GitHubAiCredits));
    }

    [Theory]
    [InlineData("github", BillingChannel.FoundryByok)]
    [InlineData("github", BillingChannel.Local)]
    [InlineData("github", BillingChannel.Unpriced)]
    [InlineData("GitHub", BillingChannel.GitHubAiCredits)]
    [InlineData(null, BillingChannel.GitHubAiCredits)]
    public void UI2_OnlyGitHubBilledRequestsOfProviderGitHubAreIdentified(string? provider, BillingChannel channel)
    {
        Assert.Null(UsageIdentity.CanonicalUsageId(provider, ServiceRequestId, channel));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{1f0e9c2a-7b3d-4e5f-8a9b-0c1d2e3f4a5b}")]
    [InlineData(" 1f0e9c2a-7b3d-4e5f-8a9b-0c1d2e3f4a5")]
    [InlineData("1f0e9c2a-7b3d-4e5f-8a9b-0c1d2e3f4a5")]
    [InlineData("1f0e9c2a7b3d4e5f8a9b0c1d2e3f4a5b")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("+0x0e9c2a-7b3d-4e5f-8a9b-0c1d2e3f4a5b")]
    [InlineData("1f0e9c2a-7b3d-4e5f-8a9b-0c1d2e3f4a5g")]
    public void UI3_AnythingButAUuidInDFormIsNoIdentity(string? serviceRequestId)
    {
        Assert.Null(UsageIdentity.NormalizeServiceRequestId(serviceRequestId));
        Assert.Null(UsageIdentity.CanonicalUsageId("github", serviceRequestId, BillingChannel.GitHubAiCredits));
    }

    [Fact]
    public void UI4_PartsAreLengthPrefixedSoTheyNeverRunTogether()
    {
        Assert.Equal(
            "2463cb7a5fc6adaca7c5ce7d37ffce1c4169f9bde5fe73d3cabeec39da7c9c38",
            UsageIdentity.Sha256Hex("a|b", "c"));
        Assert.Equal(
            "71f0f46e8bc7128a5509db6e0eae771abee3a213b39be06c01dec4ac6d8d5db8",
            UsageIdentity.Sha256Hex("a", "b|c"));
        Assert.Equal(
            "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            UsageIdentity.Sha256Hex());
        Assert.Equal(
            "1f0e9c2a-7b3d-4e5f-8a9b-0c1d2e3f4a5b",
            UsageIdentity.NormalizeServiceRequestId("1F0E9C2A-7B3D-4E5F-8A9B-0C1D2E3F4A5B"));
    }
}
