using CopilotLedger.Core.Git;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Core.Telemetry;
using CopilotLedger.Otlp.Decoding;

namespace CopilotLedger.Otlp.Normalization;

public static class GenAiSpanNormalizer
{
    // Largest token count accepted after normalization; a larger count rejects the span.
    public const long MaxTokenCount = 1_000_000_000;

    // Throws ArgumentNullException for a null request; otherwise never throws.
    public static NormalizedTraceBatch Normalize(OtlpTraceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        List<NormalizedGenAiSpan> inferenceSpans = [];
        List<GenAiSpanLink> links = [];
        int received = 0;
        int rejected = 0;
        string? firstRejectionReason = null;

        foreach (OtlpResourceSpans resourceSpans in request.ResourceSpans)
        {
            bool isCopilotResource = IsCopilotResource(resourceSpans.ResourceAttributes);
            string? workingDirectory = FindWorkingDirectory(resourceSpans.ResourceAttributes);
            foreach (OtlpSpan span in resourceSpans.Spans)
            {
                received++;
                if (!TryNormalize(
                    span,
                    isCopilotResource,
                    workingDirectory,
                    out NormalizedGenAiSpan? inferenceSpan,
                    out GenAiSpanLink? link,
                    out string? rejectionReason))
                {
                    rejected++;
                    firstRejectionReason ??= rejectionReason;
                    continue;
                }

                if (link is not null)
                {
                    links.Add(link);
                }

                if (inferenceSpan is not null)
                {
                    inferenceSpans.Add(inferenceSpan);
                }
            }
        }

        return new NormalizedTraceBatch
        {
            InferenceSpans = inferenceSpans,
            Links = links,
            ReceivedSpanCount = received,
            RejectedSpanCount = rejected,
            FirstRejectionReason = firstRejectionReason,
        };
    }

    private static bool TryNormalize(
        OtlpSpan span,
        bool isCopilotResource,
        string? workingDirectory,
        out NormalizedGenAiSpan? inferenceSpan,
        out GenAiSpanLink? link,
        out string? rejectionReason)
    {
        inferenceSpan = null;
        link = null;
        rejectionReason = null;

        if (span.TraceId is null)
        {
            rejectionReason = "invalid traceId";
            return false;
        }

        if (span.SpanId is null)
        {
            rejectionReason = "invalid spanId";
            return false;
        }

        if (span.StartTimeUnixNano == 0)
        {
            rejectionReason = "missing startTimeUnixNano";
            return false;
        }

        IReadOnlyList<OtlpAttribute> attributes = span.Attributes;
        string? operation = Text(attributes, GenAiAttributeNames.OperationName);
        bool isInference = GenAiOperationNames.IsInference(operation);
        long input = 0;
        long output = 0;
        long cacheRead = 0;
        long cacheWrite = 0;
        CacheCounterState cacheReadState = CacheCounterState.Absent;
        CacheCounterState cacheWriteState = CacheCounterState.Absent;

        if (isInference)
        {
            if (!TryToken(attributes, GenAiAttributeNames.InputTokens, out input, out rejectionReason) ||
                !TryToken(attributes, GenAiAttributeNames.OutputTokens, out output, out rejectionReason) ||
                !TryToken(
                    attributes,
                    GenAiAttributeNames.CacheReadInputTokens,
                    out cacheRead,
                    out cacheReadState,
                    out rejectionReason) ||
                !TryToken(
                    attributes,
                    FindAttribute(attributes, GenAiAttributeNames.CacheWriteInputTokens) is not null
                        ? GenAiAttributeNames.CacheWriteInputTokens
                        : GenAiAttributeNames.LegacyCacheCreationInputTokens,
                    out cacheWrite,
                    out cacheWriteState,
                    out rejectionReason))
            {
                return false;
            }

            try
            {
                checked
                {
                    if (cacheRead + cacheWrite > input)
                    {
                        input = input + cacheRead + cacheWrite;
                    }
                }
            }
            catch (OverflowException)
            {
                rejectionReason = "token counts overflow";
                return false;
            }

            if (input > MaxTokenCount ||
                output > MaxTokenCount ||
                cacheRead > MaxTokenCount ||
                cacheWrite > MaxTokenCount)
            {
                rejectionReason = $"token count exceeds {MaxTokenCount}";
                return false;
            }
        }

        DateTimeOffset start = FromUnixNano(span.StartTimeUnixNano);
        DateTimeOffset end = span.EndTimeUnixNano >= span.StartTimeUnixNano
            ? FromUnixNano(span.EndTimeUnixNano)
            : start;

        if (operation is not null || isCopilotResource)
        {
            string? remoteUrl = Text(attributes, GenAiAttributeNames.GitRepository)
                ?? Text(attributes, GenAiAttributeNames.LegacyRepoRemoteUrl);
            string? commitSha = Text(attributes, GenAiAttributeNames.GitCommitSha)
                ?? Text(attributes, GenAiAttributeNames.LegacyRepoHeadCommitHash);
            link = new GenAiSpanLink
            {
                TraceId = span.TraceId,
                SpanId = span.SpanId,
                ParentSpanId = span.ParentSpanId,
                OperationName = operation,
                AgentName = Text(attributes, GenAiAttributeNames.AgentName),
                ConversationId = Text(attributes, GenAiAttributeNames.ConversationId),
                GitRepositorySlug = remoteUrl is null
                    ? null
                    : GitRemoteUrl.GetRepositorySlug(GitRemoteUrl.Sanitize(remoteUrl)),
                GitBranch = Text(attributes, GenAiAttributeNames.GitBranch)
                    ?? Text(attributes, GenAiAttributeNames.LegacyRepoHeadBranchName),
                GitCommitSha = IsCommitSha(commitSha) ? commitSha!.ToLowerInvariant() : null,
                StartTime = start,
                ToolName = string.Equals(operation, GenAiOperationNames.ExecuteTool, StringComparison.Ordinal)
                    ? Text(attributes, GenAiAttributeNames.ToolName)
                    : null,
                ToolExitCode = string.Equals(operation, GenAiOperationNames.ExecuteTool, StringComparison.Ordinal)
                    ? ReadInt(attributes, GenAiAttributeNames.ProcessExitCode)
                    : null,
                StatusCode = span.StatusCode,
            };
        }

        if (isInference)
        {
            inferenceSpan = new NormalizedGenAiSpan
            {
                TraceId = span.TraceId,
                SpanId = span.SpanId,
                ParentSpanId = span.ParentSpanId,
                OperationName = operation!,
                StartTime = start,
                EndTime = end,
                ProviderName = Text(attributes, GenAiAttributeNames.ProviderName),
                RequestModel = Text(attributes, GenAiAttributeNames.RequestModel),
                ResponseModel = Text(attributes, GenAiAttributeNames.ResponseModel),
                AgentName = Text(attributes, GenAiAttributeNames.AgentName),
                ConversationId = Text(attributes, GenAiAttributeNames.ConversationId),
                ServerAddress = Text(attributes, GenAiAttributeNames.ServerAddress),
                ServerPort = ReadServerPort(attributes),
                WorkingDirectory = workingDirectory,
                InputTokens = input,
                OutputTokens = output,
                CacheReadInputTokens = cacheRead,
                CacheWriteInputTokens = cacheWrite,
                CacheReadState = cacheReadState,
                CacheWriteState = cacheWriteState,
                ServiceRequestId = Text(attributes, GenAiAttributeNames.ServiceRequestId),
                ReportedCharge = ReadReportedCharge(attributes),
            };
        }

        return true;
    }

    private static bool IsCopilotResource(IReadOnlyList<OtlpAttribute> attributes)
    {
        OtlpAttribute? serviceNameAttribute = FindAttribute(
            attributes,
            GenAiAttributeNames.ServiceName);
        string? serviceName = serviceNameAttribute?.Value.Kind == OtlpValueKind.String
            ? serviceNameAttribute.Value.StringValue
            : null;
        return string.Equals(serviceName, "github-copilot", StringComparison.Ordinal) ||
               string.Equals(serviceName, "copilot-chat", StringComparison.Ordinal);
    }

    private static string? FindWorkingDirectory(IReadOnlyList<OtlpAttribute> attributes)
    {
        foreach (OtlpAttribute attribute in attributes)
        {
            if (attribute.Key == GenAiAttributeNames.ProcessWorkingDirectory &&
                attribute.Value.Kind == OtlpValueKind.String &&
                !string.IsNullOrWhiteSpace(attribute.Value.StringValue) &&
                Path.IsPathFullyQualified(attribute.Value.StringValue))
            {
                return attribute.Value.StringValue;
            }
        }

        return null;
    }

    private static string? Text(IReadOnlyList<OtlpAttribute> attributes, string key)
    {
        OtlpAttribute? attribute = FindAttribute(attributes, key);
        return attribute is not null &&
               attribute.Value.Kind == OtlpValueKind.String &&
               !string.IsNullOrWhiteSpace(attribute.Value.StringValue)
            ? attribute.Value.StringValue
            : null;
    }

    private static bool TryToken(
        IReadOnlyList<OtlpAttribute> attributes,
        string key,
        out long value,
        out string? rejectionReason)
    {
        OtlpAttribute? attribute = FindAttribute(attributes, key);
        if (attribute is null)
        {
            value = 0;
            rejectionReason = null;
            return true;
        }

        if (attribute.Value.Kind != OtlpValueKind.Int || attribute.Value.IntValue < 0)
        {
            value = 0;
            rejectionReason = $"{key} is not a non-negative integer";
            return false;
        }

        value = attribute.Value.IntValue;
        rejectionReason = null;
        return true;
    }

    // Like TryToken, and also says whether the span carried the attribute. A missing attribute is Absent with value 0.
    private static bool TryToken(
        IReadOnlyList<OtlpAttribute> attributes,
        string key,
        out long value,
        out CacheCounterState state,
        out string? rejectionReason)
    {
        state = FindAttribute(attributes, key) is null
            ? CacheCounterState.Absent
            : CacheCounterState.Reported;
        return TryToken(attributes, key, out value, out rejectionReason);
    }

    private static int? ReadServerPort(IReadOnlyList<OtlpAttribute> attributes)
    {
        OtlpAttribute? attribute = FindAttribute(attributes, GenAiAttributeNames.ServerPort);
        return attribute is not null &&
               attribute.Value.Kind == OtlpValueKind.Int &&
               attribute.Value.IntValue is >= 1 and <= 65535
            ? (int)attribute.Value.IntValue
            : null;
    }

    private static long? ReadInt(IReadOnlyList<OtlpAttribute> attributes, string key)
    {
        OtlpAttribute? attribute = FindAttribute(attributes, key);
        return attribute?.Value.Kind == OtlpValueKind.Int
            ? attribute.Value.IntValue
            : null;
    }

    private static ReportedCharge ReadReportedCharge(IReadOnlyList<OtlpAttribute> attributes)
    {
        OtlpAttribute? attribute = FindAttribute(attributes, GenAiAttributeNames.NanoAiu);
        if (attribute is null)
        {
            return ReportedCharge.Absent;
        }

        return attribute.Value.Kind switch
        {
            OtlpValueKind.Int when attribute.Value.IntValue is >= 0 and <= NanoAiu.MaxExact =>
                ReportedCharge.Valid(attribute.Value.IntValue),
            OtlpValueKind.Double when attribute.Value.DoubleAsNanoAiu is long nanoAiu =>
                ReportedCharge.Valid(nanoAiu),
            _ => ReportedCharge.Malformed,
        };
    }

    private static OtlpAttribute? FindAttribute(IReadOnlyList<OtlpAttribute> attributes, string key) =>
        attributes.FirstOrDefault(attribute => attribute.Key == key);

    private static bool IsCommitSha(string? commitSha) =>
        commitSha is { Length: 40 or 64 } && commitSha.All(Uri.IsHexDigit);

    private static DateTimeOffset FromUnixNano(ulong nanoseconds) =>
        DateTimeOffset.UnixEpoch.AddTicks((long)(nanoseconds / 100));
}
