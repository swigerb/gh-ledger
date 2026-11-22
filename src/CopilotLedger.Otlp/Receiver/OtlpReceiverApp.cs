using System.IO.Compression;
using CopilotLedger.Otlp.Decoding;
using CopilotLedger.Otlp.Ingestion;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace CopilotLedger.Otlp.Receiver;

public static class OtlpReceiverApp
{
    // Serves the OTLP endpoints on localhost (127.0.0.1 and ::1) at options.Port.
    // configureWebHost runs after the defaults; tests pass builder => builder.UseTestServer().
    // ingestFailed receives each exception from IngestAsync that turns a request into a 503, so the caller can log it.
    public static WebApplication Build(
        OtlpReceiverOptions options,
        ILedgerIngestor ingestor,
        Action<IWebHostBuilder>? configureWebHost = null,
        Action<Exception>? ingestFailed = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(ingestor);
        if (options.Port is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Port must be from 1 to 65535.");
        }

        if (options.MaxRequestBodyBytes < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "MaxRequestBodyBytes must be greater than or equal to 1.");
        }

        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
        builder.WebHost.UseKestrelCore();
        builder.WebHost.ConfigureKestrel(k =>
        {
            k.ListenLocalhost(options.Port);
            k.Limits.MaxRequestBodySize = options.MaxRequestBodyBytes;
            k.AddServerHeader = false;
        });
        builder.Services.AddRoutingCore();
        configureWebHost?.Invoke(builder.WebHost);
        WebApplication app = builder.Build();

        app.MapPost("/v1/traces", context => HandleAsync(context, options, ingestor, ingestFailed, traces: true));
        app.MapPost("/v1/metrics", context => HandleAsync(context, options, ingestor, ingestFailed, traces: false));
        app.MapPost("/v1/logs", context => HandleAsync(context, options, ingestor, ingestFailed, traces: false));
        return app;
    }

    private static async Task HandleAsync(
        HttpContext context,
        OtlpReceiverOptions options,
        ILedgerIngestor ingestor,
        Action<Exception>? ingestFailed,
        bool traces)
    {
        OtlpEncoding encoding = OtlpHttp.EncodingFromContentType(
            context.Request.ContentType,
            out string mediaType);
        if (!IsLoopbackHost(context.Request.Host.Host))
        {
            string host = string.IsNullOrEmpty(context.Request.Host.Host)
                ? "(none)"
                : context.Request.Host.Host;
            await OtlpHttp.WriteErrorAsync(
                context.Response,
                encoding,
                StatusCodes.Status400BadRequest,
                $"Host {host} is not a loopback name.");
            return;
        }

        if (!OtlpHttp.IsSupportedContentType(mediaType))
        {
            string shownType = string.IsNullOrEmpty(mediaType) ? "(none)" : mediaType;
            await OtlpHttp.WriteErrorAsync(
                context.Response,
                encoding,
                StatusCodes.Status415UnsupportedMediaType,
                $"Unsupported Content-Type {shownType}. Use application/x-protobuf or application/json.");
            return;
        }

        string contentEncoding = (context.Request.Headers.ContentEncoding.ToString()).Trim();
        bool gzip = contentEncoding.Equals("gzip", StringComparison.OrdinalIgnoreCase);
        if (contentEncoding.Length != 0
            && !contentEncoding.Equals("identity", StringComparison.OrdinalIgnoreCase)
            && !gzip)
        {
            await OtlpHttp.WriteErrorAsync(
                context.Response,
                encoding,
                StatusCodes.Status415UnsupportedMediaType,
                $"Unsupported Content-Encoding {contentEncoding}. Use gzip or no encoding.");
            return;
        }

        if (context.Request.ContentLength > options.MaxRequestBodyBytes)
        {
            await WriteBodyTooLargeAsync(context.Response, encoding, options.MaxRequestBodyBytes);
            return;
        }

        byte[] body;
        try
        {
            body = await ReadBodyAsync(context, gzip, options.MaxRequestBodyBytes);
        }
        catch (BodyTooLargeException)
        {
            await WriteBodyTooLargeAsync(context.Response, encoding, options.MaxRequestBodyBytes);
            return;
        }
        catch (BadHttpRequestException ex) when (ex.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            await WriteBodyTooLargeAsync(context.Response, encoding, options.MaxRequestBodyBytes);
            return;
        }
        catch (InvalidDataException)
        {
            await OtlpHttp.WriteErrorAsync(
                context.Response,
                encoding,
                StatusCodes.Status400BadRequest,
                "Invalid gzip body.");
            return;
        }

        if (!traces)
        {
            await OtlpHttp.WriteEmptySuccessAsync(context.Response, encoding);
            return;
        }

        OtlpTraceRequest request;
        try
        {
            request = encoding == OtlpEncoding.Protobuf
                ? OtlpTraceDecoder.DecodeProtobuf(body)
                : OtlpTraceDecoder.DecodeJson(body);
        }
        catch (OtlpDecodeException ex)
        {
            string format = encoding == OtlpEncoding.Protobuf ? "protobuf" : "JSON";
            await OtlpHttp.WriteErrorAsync(
                context.Response,
                encoding,
                StatusCodes.Status400BadRequest,
                $"Invalid OTLP {format} payload: {ex.Message}");
            return;
        }

        IngestResult result;
        try
        {
            result = await ingestor.IngestAsync(request, context.RequestAborted);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            ingestFailed?.Invoke(ex);
            context.Response.Headers.RetryAfter = "5";
            await OtlpHttp.WriteErrorAsync(
                context.Response,
                encoding,
                StatusCodes.Status503ServiceUnavailable,
                "The ledger is unavailable; retry later.");
            return;
        }

        if (result.RejectedSpans == 0)
        {
            await OtlpHttp.WriteEmptySuccessAsync(context.Response, encoding);
        }
        else
        {
            await OtlpHttp.WritePartialSuccessAsync(
                context.Response,
                encoding,
                result.RejectedSpans,
                result.FirstRejectionReason);
        }
    }

    private static async Task<byte[]> ReadBodyAsync(
        HttpContext context,
        bool gzip,
        long maximumLength)
    {
        Stream source = context.Request.Body;
        GZipStream? gzipStream = null;
        if (gzip)
        {
            gzipStream = new GZipStream(source, CompressionMode.Decompress, leaveOpen: true);
            source = gzipStream;
        }

        try
        {
            using MemoryStream output = new();
            byte[] buffer = new byte[81_920];
            long total = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, context.RequestAborted)) > 0)
            {
                total += read;
                if (total > maximumLength)
                {
                    throw new BodyTooLargeException();
                }

                output.Write(buffer, 0, read);
            }

            return output.ToArray();
        }
        finally
        {
            gzipStream?.Dispose();
        }
    }

    private static Task WriteBodyTooLargeAsync(
        HttpResponse response,
        OtlpEncoding encoding,
        long maximumLength) =>
        OtlpHttp.WriteErrorAsync(
            response,
            encoding,
            StatusCodes.Status413PayloadTooLarge,
            $"Request body exceeds {maximumLength} bytes.");

    private static bool IsLoopbackHost(string host) =>
        host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
        || host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
        || host.Equals("[::1]", StringComparison.OrdinalIgnoreCase)
        || host.Equals("::1", StringComparison.OrdinalIgnoreCase);

    private sealed class BodyTooLargeException : Exception;
}
