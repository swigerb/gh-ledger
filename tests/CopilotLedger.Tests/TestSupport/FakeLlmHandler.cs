using System.Net;
using System.Text;
using System.Text.Json;

namespace CopilotLedger.Tests.TestSupport;

// One request that audit --local-llm sent.
public sealed record LlmCall(
    string Method,
    string Uri,
    string? Host,
    string? ContentType,
    string? Authorization,
    string Accept,
    string UserAgent,
    string Body);

// A local model for audit --local-llm's tests: records each request and answers with respond(request, token), which may wait
// on the token or throw. No socket is opened.
public sealed class FakeLlmHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
{
    public List<LlmCall> Calls { get; } = [];

    // Answers every request with response().
    public static FakeLlmHandler Returning(Func<HttpResponseMessage> response) => new((_, _) => Task.FromResult(response()));

    // 200 with an OpenAI-compatible chat completion whose choices[0].message.content is content.
    public static HttpResponseMessage Answer(string content) => Json(
        JsonSerializer.Serialize(new
        {
            id = "chatcmpl-1",
            @object = "chat.completion",
            choices = new[] { new { index = 0, message = new { role = "assistant", content }, finish_reason = "stop" } },
        }));

    public static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Calls.Add(new LlmCall(
            request.Method.Method,
            request.RequestUri!.AbsoluteUri,
            request.Headers.Host,
            request.Content?.Headers.ContentType?.ToString(),
            request.Headers.Authorization?.ToString(),
            request.Headers.Accept.ToString(),
            request.Headers.UserAgent.ToString(),
            request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken)));
        return await respond(request, cancellationToken);
    }
}
