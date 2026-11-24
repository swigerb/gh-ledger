using System.Security.Cryptography.X509Certificates;
using CopilotLedger.Cli.Audit;

namespace CopilotLedger.Tests.Cli;

// audit --local-llm's endpoint policy and its default handler.
public sealed class LocalEndpointPolicyTests
{
    public static TheoryData<string, string, string?, string> Accepted => new()
    {
        { "http://localhost:11434/v1", "http://127.0.0.1:11434/v1/chat/completions", "localhost:11434", "127.0.0.1" },
        { "http://LOCALHOST:11434/v1/", "http://127.0.0.1:11434/v1/chat/completions", "localhost:11434", "127.0.0.1" },
        { "http://127.0.0.1:5273/v1", "http://127.0.0.1:5273/v1/chat/completions", null, "127.0.0.1" },
        { "http://[::1]:8080/v1", "http://[::1]:8080/v1/chat/completions", null, "::1" },
        { "https://localhost/v1", "https://127.0.0.1/v1/chat/completions", "localhost", "127.0.0.1" },
        { "http://127.255.255.254:11434", "http://127.255.255.254:11434/chat/completions", null, "127.255.255.254" },
        { "http://2130706433:11434/v1", "http://127.0.0.1:11434/v1/chat/completions", null, "127.0.0.1" },
        { "http://[0:0:0:0:0:0:0:1]:8080/v1", "http://[::1]:8080/v1/chat/completions", null, "::1" },
    };

    [Theory]
    [MemberData(nameof(Accepted))]
    public void LE1_LoopbackUrls_ArePinnedToAnAddress(string url, string requestUri, string? hostHeader, string address)
    {
        Assert.True(LocalEndpointPolicy.TryValidate(url, out LocalEndpoint? endpoint, out string? error));

        Assert.Null(error);
        Assert.Equal(requestUri, endpoint.RequestUri.AbsoluteUri);
        Assert.Equal(hostHeader, endpoint.HostHeader);
        Assert.Equal(address, endpoint.Address.ToString());
        Assert.Equal(new Uri(url).AbsoluteUri, endpoint.BaseUri.AbsoluteUri);
    }

    public static TheoryData<string, int> Rejected => new()
    {
        { "https://api.openai.com/v1", 6 },
        { "http://10.0.0.5:11434/v1", 6 },
        { "http://0.0.0.0/v1", 6 },
        { "http://[::]/v1", 6 },
        { "http://[::ffff:127.0.0.1]/v1", 6 },
        { "http://[::1%251]:8080/v1", 6 },
        { "http://localhost.example.com/v1", 6 },
        { "http://127.0.0.1.nip.io/v1", 6 },
        { "http://localhost./v1", 6 },
        { "http://127.0.0.1./v1", 6 },
        { "http://\u24DBocalhost/v1", 6 },
        { "http://user:pass@localhost/v1", 3 },
        { "http://user@127.0.0.1/v1", 3 },
        { "ftp://localhost", 2 },
        { "file://localhost/x", 2 },
        { "http://127.0.0.1:11434/v1?x=1", 4 },
        { "http://127.0.0.1/v1#frag", 4 },
        { "http://127.0.0.1:0/v1", 5 },
        { "http://127.0.0.1:65536/v1", 1 },
        { "localhost:11434", 1 },
        { "/v1/chat", 1 },
        { "", 1 },
    };

    [Theory]
    [MemberData(nameof(Rejected))]
    public void LE2_OtherUrls_AreRejectedWithTheFirstFailedCheck(string url, int check)
    {
        string[] reasons =
        [
            "use an absolute http or https URL, such as http://127.0.0.1:11434/v1.",
            "use http or https.",
            "remove the user name and password.",
            "remove the query and the fragment.",
            "use a port from 1 to 65535.",
            "use localhost, an address in 127.0.0.0/8, or [::1]; other hosts aren't allowed.",
        ];

        Assert.False(LocalEndpointPolicy.TryValidate(url, out LocalEndpoint? endpoint, out string? error));

        Assert.Null(endpoint);
        Assert.Equal(reasons[check - 1], error);
    }

    [Fact]
    public void LE3_TheDefaultHandler_UsesNoProxyNoRedirectsNoCookiesAndNoCertificateDownloads()
    {
        using SocketsHttpHandler handler = LocalLlmAdvisor.CreateDefaultHandler();

        Assert.False(handler.UseProxy);
        Assert.False(handler.AllowAutoRedirect);
        Assert.False(handler.UseCookies);
        Assert.Equal(X509RevocationMode.NoCheck, handler.SslOptions.CertificateRevocationCheckMode);
        X509ChainPolicy policy = Assert.IsType<X509ChainPolicy>(handler.SslOptions.CertificateChainPolicy);
        Assert.True(policy.DisableCertificateDownloads);
        Assert.Equal(X509RevocationMode.NoCheck, policy.RevocationMode);
        Assert.Equal(X509ChainTrustMode.System, policy.TrustMode);
        Assert.Null(handler.SslOptions.RemoteCertificateValidationCallback);
        Assert.Null(handler.SslOptions.TargetHost);
    }
}
