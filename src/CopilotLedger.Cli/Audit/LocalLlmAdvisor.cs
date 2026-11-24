using System.Net.Http.Headers;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

namespace CopilotLedger.Cli.Audit;

// The local model's answer: its sanitized suggestions, or the warning line that says why there are none. Exactly one is set.
internal sealed record LocalLlmAnswer(string? Suggestions, string? Warning);

// Asks an OpenAI-compatible chat-completions endpoint on this machine for suggestions about an audit, once, without
// credentials, and never follows a redirect. Its answer is only displayed. Public for tests.
public static class LocalLlmAdvisor
{
    public const int MaxResponseBytes = 262_144;
    public const int MaxSuggestionBytes = 16_384;

    // No proxy (not even the system's), no redirects, no cookies, and no network use while validating an https server's
    // certificate: no issuer certificate downloads (AIA) and no revocation checks. The chain is still built against the
    // system's trusted roots, and the host name still checked (SslStream does both whatever the chain policy).
    public static SocketsHttpHandler CreateDefaultHandler() => new()
    {
        UseProxy = false,
        AllowAutoRedirect = false,
        UseCookies = false,
        SslOptions = new SslClientAuthenticationOptions
        {
            CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
            CertificateChainPolicy = new X509ChainPolicy
            {
                DisableCertificateDownloads = true,
                RevocationMode = X509RevocationMode.NoCheck,
            },
        },
    };

    // POSTs body to endpoint.RequestUri through handler, which it disposes, and reads at most MaxResponseBytes of the answer.
    // timeoutSeconds covers sending and reading. Every failure becomes a warning; no exception or response text reaches it.
    internal static async Task<LocalLlmAnswer> AskAsync(
        HttpMessageHandler handler,
        LocalEndpoint endpoint,
        byte[] body,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        string url = endpoint.BaseUri.AbsoluteUri;
        using HttpClient client = new(handler, disposeHandler: true) { Timeout = Timeout.InfiniteTimeSpan };
        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        try
        {
            using HttpRequestMessage request = new(HttpMethod.Post, endpoint.RequestUri) { Content = new ByteArrayContent(body) };
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("gh-ledger", GhLedgerCli.Version));
            if (endpoint.HostHeader is not null)
            {
                request.Headers.Host = endpoint.HostHeader;
            }

            using HttpResponseMessage response =
                await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            if (!response.IsSuccessStatusCode)
            {
                return Warn($"the local model at {url} answered HTTP {(int)response.StatusCode}; no suggestions.");
            }

            byte[]? answer = await ReadAtMostAsync(response.Content, MaxResponseBytes, deadline.Token);
            if (answer is null)
            {
                return Warn($"the local model at {url} sent more than 256 KiB; no suggestions.");
            }

            string text = Content(answer) is string content ? AdvisoryText.Sanitize(content).TrimEnd() : string.Empty;
            return text.Length == 0
                ? Warn($"the local model at {url} answered without text in choices[0].message.content; no suggestions.")
                : new LocalLlmAnswer(AdvisoryText.Cap(text, MaxSuggestionBytes), null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Warn($"the local model at {url} didn't answer within {timeoutSeconds} seconds; no suggestions.");
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException)
        {
            return Warn($"couldn't reach the local model at {url}; no suggestions.");
        }
    }

    private static LocalLlmAnswer Warn(string warning) => new(null, "Warning: " + warning);

    // The content's bytes, or null when there are more than limit.
    private static async Task<byte[]?> ReadAtMostAsync(HttpContent content, int limit, CancellationToken cancellationToken)
    {
        await using Stream stream = await content.ReadAsStreamAsync(cancellationToken);
        byte[] buffer = new byte[limit + 1];
        int total = 0;
        while (total < buffer.Length)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(total), cancellationToken);
            if (read == 0)
            {
                break;
            }

            total += read;
        }

        return total > limit ? null : buffer[..total];
    }

    // choices[0].message.content when the answer is JSON and it's a string; else null.
    private static string? Content(byte[] answer)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(answer);
            return document.RootElement.ValueKind == JsonValueKind.Object &&
                   document.RootElement.TryGetProperty("choices", out JsonElement choices) &&
                   choices.ValueKind == JsonValueKind.Array &&
                   choices.GetArrayLength() > 0 &&
                   choices[0].ValueKind == JsonValueKind.Object &&
                   choices[0].TryGetProperty("message", out JsonElement message) &&
                   message.ValueKind == JsonValueKind.Object &&
                   message.TryGetProperty("content", out JsonElement content) &&
                   content.ValueKind == JsonValueKind.String
                ? content.GetString()
                : null;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            return null;
        }
    }
}
