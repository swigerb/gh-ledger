using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CopilotLedger.Core.Git;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Otlp.Attribution;
using CopilotLedger.Otlp.Decoding;
using CopilotLedger.Otlp.Ingestion;
using CopilotLedger.Otlp.Receiver;
using CopilotLedger.Storage;
using CopilotLedger.Tests.TestSupport;
using Microsoft.AspNetCore.TestHost;

namespace CopilotLedger.Tests.Otlp;

public sealed class OtlpReceiverTests
{
    [Fact]
    public async Task RC1_ProtobufTrace_IsIngested()
    {
        RecordingIngestor ingestor = new();
        await using var app = OtlpReceiverApp.Build(
            new OtlpReceiverOptions(),
            ingestor,
            builder => builder.UseTestServer());
        await app.StartAsync();
        using HttpClient client = app.GetTestClient();
        using HttpResponseMessage response = await client.SendAsync(
            Request("/v1/traces", "application/x-protobuf", OtlpFixtures.PGold1));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/x-protobuf", response.Content.Headers.ContentType?.ToString());
        Assert.Empty(await response.Content.ReadAsByteArrayAsync());
        OtlpTraceRequest request = Assert.Single(ingestor.Requests);
        Assert.Equal(1, request.SpanCount);
        Assert.Equal(
            "5b8efff798038103d269b633813fc60c",
            request.ResourceSpans[0].Spans[0].TraceId);
    }

    [Fact]
    public async Task RC2_JsonTrace_IsIngested()
    {
        RecordingIngestor ingestor = new();
        await using var app = Build(ingestor);
        await app.StartAsync();
        using HttpClient client = app.GetTestClient();
        using HttpResponseMessage response = await client.SendAsync(
            Request(
                "/v1/traces",
                "application/json",
                Encoding.UTF8.GetBytes(OtlpFixtures.CopilotChatTraceJson)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.ToString());
        Assert.Equal("{}", await response.Content.ReadAsStringAsync());
        Assert.Equal(2, Assert.Single(ingestor.Requests).SpanCount);
    }

    public static TheoryData<string> CompressedCases => new()
    {
        "protobuf-gzip",
        "protobuf-GZIP",
        "json-parameters",
    };

    [Theory]
    [MemberData(nameof(CompressedCases))]
    public async Task RC3_GzipAndContentTypeParameters_AreAccepted(string scenario)
    {
        RecordingIngestor ingestor = new();
        await using var app = Build(ingestor);
        await app.StartAsync();
        using HttpClient client = app.GetTestClient();
        HttpRequestMessage request = scenario switch
        {
            "json-parameters" => Request(
                "/v1/traces",
                "application/json; charset=utf-8",
                Encoding.UTF8.GetBytes(OtlpFixtures.CopilotChatTraceJson)),
            _ => Request("/v1/traces", "application/x-protobuf", Gzip(OtlpFixtures.PGold1)),
        };
        if (scenario != "json-parameters")
        {
            request.Content!.Headers.TryAddWithoutValidation(
                "Content-Encoding",
                scenario.EndsWith("GZIP", StringComparison.Ordinal) ? "GZIP" : "gzip");
        }

        using HttpResponseMessage response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    public static TheoryData<string?, string?, string> UnsupportedCases => new()
    {
        { "text/plain", null, "Unsupported Content-Type text/plain. Use application/x-protobuf or application/json." },
        { null!, null, "Unsupported Content-Type (none). Use application/x-protobuf or application/json." },
        { "application/x-protobuf", "br", "Unsupported Content-Encoding br. Use gzip or no encoding." },
    };

    [Theory]
    [MemberData(nameof(UnsupportedCases))]
    public async Task RC4_UnsupportedTypes_Return415(
        string? contentType,
        string? contentEncoding,
        string expectedMessage)
    {
        RecordingIngestor ingestor = new();
        await using var app = Build(ingestor);
        await app.StartAsync();
        using HttpClient client = app.GetTestClient();
        HttpRequestMessage request = Request(
            "/v1/traces",
            contentType,
            []);
        if (contentEncoding is not null)
        {
            request.Content!.Headers.TryAddWithoutValidation("Content-Encoding", contentEncoding);
        }

        using HttpResponseMessage response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        Assert.Equal(
            expectedMessage,
            await ReadProtobufOrJsonErrorAsync(
                response,
                contentType ?? "application/json"));
        Assert.Empty(ingestor.Requests);
    }

    [Fact]
    public async Task RC5_RawAndDecompressedBodies_EnforceTheLimit()
    {
        RecordingIngestor ingestor = new();
        await using var app = Build(
            ingestor,
            new OtlpReceiverOptions { MaxRequestBodyBytes = 4096 });
        await app.StartAsync();
        using HttpClient client = app.GetTestClient();
        byte[] large = new byte[8192];
        byte[] compressed = Gzip(new byte[1_048_576]);
        foreach (byte[] body in new[] { large, compressed })
        {
            HttpRequestMessage request = Request("/v1/traces", "application/x-protobuf", body);
            if (ReferenceEquals(body, compressed))
            {
                request.Content!.Headers.TryAddWithoutValidation("Content-Encoding", "gzip");
            }

            using HttpResponseMessage response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
            Assert.Equal(
                "12205265717565737420626f6479206578636565647320343039362062797465732e",
                Convert.ToHexStringLower(await response.Content.ReadAsByteArrayAsync()));
        }
    }

    public static TheoryData<string, byte[], string?, string> InvalidPayloads => new()
    {
        { "application/x-protobuf", [0x0B], null, "Invalid OTLP protobuf payload: " },
        { "application/json", Encoding.UTF8.GetBytes("{"), null, "Invalid OTLP JSON payload: " },
        { "application/x-protobuf", Encoding.UTF8.GetBytes("not gzip"), "gzip", "Invalid gzip body." },
    };

    [Theory]
    [MemberData(nameof(InvalidPayloads))]
    public async Task RC6_UndecodablePayloads_Return400(
        string contentType,
        byte[] body,
        string? contentEncoding,
        string expected)
    {
        RecordingIngestor ingestor = new();
        await using var app = Build(ingestor);
        await app.StartAsync();
        using HttpClient client = app.GetTestClient();
        HttpRequestMessage request = Request("/v1/traces", contentType, body);
        if (contentEncoding is not null)
        {
            request.Content!.Headers.TryAddWithoutValidation("Content-Encoding", contentEncoding);
        }

        using HttpResponseMessage response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        string message = await ReadProtobufOrJsonErrorAsync(response, contentType);
        if (expected.EndsWith(": ", StringComparison.Ordinal))
        {
            Assert.StartsWith(expected, message);
        }
        else
        {
            Assert.Equal(expected, message);
        }
    }

    public static TheoryData<string, HttpStatusCode, string?> Hosts => new()
    {
        { "evil.example:4318", HttpStatusCode.BadRequest, "Host evil.example is not a loopback name." },
        { "127.0.0.1:4318", HttpStatusCode.OK, null },
        { "[::1]:4318", HttpStatusCode.OK, null },
        { "LOCALHOST:4318", HttpStatusCode.OK, null },
    };

    [Theory]
    [MemberData(nameof(Hosts))]
    public async Task RC7_OnlyLoopbackHost_IsAccepted(
        string host,
        HttpStatusCode status,
        string? error)
    {
        RecordingIngestor ingestor = new();
        await using var app = Build(ingestor);
        await app.StartAsync();
        using HttpClient client = app.GetTestClient();
        HttpRequestMessage request = Request("/v1/traces", "application/json", "{}"u8.ToArray());
        request.Headers.Host = host;
        using HttpResponseMessage response = await client.SendAsync(request);
        Assert.Equal(status, response.StatusCode);
        if (error is not null)
        {
            Assert.Equal(error, await ReadJsonErrorAsync(response));
        }
    }

    [Fact]
    public async Task RC8_IngestFailure_ReturnsRetryable503()
    {
        InvalidOperationException failure = new();
        RecordingIngestor ingestor = new() { Exception = failure };
        List<Exception> reported = [];
        await using var app = OtlpReceiverApp.Build(
            new OtlpReceiverOptions(),
            ingestor,
            builder => builder.UseTestServer(),
            reported.Add);
        await app.StartAsync();
        using HttpClient client = app.GetTestClient();
        using HttpResponseMessage response = await client.SendAsync(
            Request("/v1/traces", "application/json", "{}"u8.ToArray()));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("5", response.Headers.GetValues("Retry-After").Single());
        Assert.Equal(
            "The ledger is unavailable; retry later.",
            await ReadJsonErrorAsync(response));
        Assert.Same(failure, Assert.Single(reported));
    }

    [Fact]
    public async Task RC9_PartialSuccess_UsesBothEncodings()
    {
        RecordingIngestor ingestor = new()
        {
            Result = IngestResult(2, "invalid traceId"),
        };
        await using var app = Build(ingestor);
        await app.StartAsync();
        using HttpClient client = app.GetTestClient();
        using HttpResponseMessage protobuf = await client.SendAsync(
            Request("/v1/traces", "application/x-protobuf", OtlpFixtures.PGold1));
        Assert.Equal(
            "0a270802122332207370616e2873292072656a65637465643a20696e76616c69642074726163654964",
            Convert.ToHexStringLower(await protobuf.Content.ReadAsByteArrayAsync()));

        using HttpResponseMessage json = await client.SendAsync(
            Request(
                "/v1/traces",
                "application/json",
                Encoding.UTF8.GetBytes(OtlpFixtures.PGold1Json)));
        using JsonDocument document = JsonDocument.Parse(
            await json.Content.ReadAsStringAsync());
        JsonElement partial = document.RootElement.GetProperty("partialSuccess");
        Assert.Equal("2", partial.GetProperty("rejectedSpans").GetString());
        Assert.Equal(
            "2 span(s) rejected: invalid traceId",
            partial.GetProperty("errorMessage").GetString());
    }

    [Fact]
    public async Task RC10_Routing_Statuses_ArePreserved()
    {
        RecordingIngestor ingestor = new();
        await using var app = Build(ingestor);
        await app.StartAsync();
        using HttpClient client = app.GetTestClient();
        using HttpResponseMessage get = await client.GetAsync("/v1/traces");
        using HttpResponseMessage unknown = await client.PostAsync(
            "/v1/unknown",
            new ByteArrayContent([]));
        Assert.Equal(HttpStatusCode.MethodNotAllowed, get.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    [Fact]
    public async Task RC11_MetricsAndLogs_AreDiscarded()
    {
        RecordingIngestor ingestor = new();
        await using var app = Build(ingestor);
        await app.StartAsync();
        using HttpClient client = app.GetTestClient();
        using HttpResponseMessage metrics = await client.SendAsync(
            Request("/v1/metrics", "application/x-protobuf", new byte[10]));
        using HttpResponseMessage logs = await client.SendAsync(
            Request(
                "/v1/logs",
                "application/json",
                Encoding.UTF8.GetBytes("""{"resourceLogs":[]}""")));
        Assert.Equal(HttpStatusCode.OK, metrics.StatusCode);
        Assert.Equal(
            "application/x-protobuf",
            metrics.Content.Headers.ContentType?.ToString());
        Assert.Empty(await metrics.Content.ReadAsByteArrayAsync());
        Assert.Equal(HttpStatusCode.OK, logs.StatusCode);
        Assert.Equal("application/json", logs.Content.Headers.ContentType?.ToString());
        Assert.Equal("{}", await logs.Content.ReadAsStringAsync());
        Assert.Empty(ingestor.Requests);
    }

    [Fact]
    public async Task RC12_RealIngestor_DoesNotDuplicateTheSpan()
    {
        using var temp = new TempDirectory();
        SqliteLedgerStore store = new(Path.Combine(temp.Path, "ledger.db"));
        await store.InitializeAsync();
        LedgerIngestor ingestor = new(
            store,
            JsonRateCardCalculator.LoadEmbedded(),
            new CachingGitContextResolver(new FileSystemGitContextResolver(temp.Path)),
            new CopilotSessionWorkspaceLocator(Path.Combine(temp.Path, "copilot-home")));
        await using var app = Build(ingestor);
        await app.StartAsync();
        using HttpClient client = app.GetTestClient();
        using HttpResponseMessage first = await client.SendAsync(
            Request(
                "/v1/traces",
                "application/json",
                Encoding.UTF8.GetBytes(OtlpFixtures.CopilotChatTraceJson)));
        using HttpResponseMessage duplicate = await client.SendAsync(
            Request("/v1/traces", "application/x-protobuf", OtlpFixtures.PGold1));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
        var rows = await store.GetRollupAsync(LedgerGrouping.Model, LedgerFilter.All);
        var row = Assert.Single(rows);
        Assert.Equal("mai-code-2", row.Model);
        Assert.Equal(0.69071m, row.AiCredits);
        Assert.Equal(1, row.SpanCount);
    }

    private static Microsoft.AspNetCore.Builder.WebApplication Build(
        ILedgerIngestor ingestor,
        OtlpReceiverOptions? options = null) =>
        OtlpReceiverApp.Build(
            options ?? new OtlpReceiverOptions(),
            ingestor,
            builder => builder.UseTestServer());

    private static HttpRequestMessage Request(
        string path,
        string? contentType,
        byte[] body)
    {
        HttpRequestMessage request = new(HttpMethod.Post, path)
        {
            Content = new ByteArrayContent(body),
        };
        if (contentType is not null)
        {
            request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        }

        return request;
    }

    private static byte[] Gzip(byte[] input)
    {
        using MemoryStream output = new();
        using (GZipStream gzip = new(output, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            gzip.Write(input);
        }

        return output.ToArray();
    }

    private static async Task<string> ReadJsonErrorAsync(HttpResponseMessage response)
    {
        using JsonDocument document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("message").GetString()!;
    }

    private static async Task<string> ReadProtobufOrJsonErrorAsync(
        HttpResponseMessage response,
        string contentType)
    {
        if (contentType != "application/x-protobuf")
        {
            return await ReadJsonErrorAsync(response);
        }

        byte[] bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(0x12, bytes[0]);
        Assert.Equal(bytes.Length - 2, bytes[1]);
        return Encoding.UTF8.GetString(bytes[2..]);
    }

    private static IngestResult IngestResult(int rejected, string? reason) =>
        new()
        {
            ReceivedSpans = 0,
            RejectedSpans = rejected,
            FirstRejectionReason = reason,
            InferenceSpans = 0,
            RecordedSpans = 0,
        };

    private sealed class RecordingIngestor : ILedgerIngestor
    {
        public List<OtlpTraceRequest> Requests { get; } = [];
        public Exception? Exception { get; set; }
        public IngestResult Result { get; set; } = IngestResult(0, null);

        public Task<IngestResult> IngestAsync(
            OtlpTraceRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Exception is null
                ? Task.FromResult(Result)
                : Task.FromException<IngestResult>(Exception);
        }
    }
}
