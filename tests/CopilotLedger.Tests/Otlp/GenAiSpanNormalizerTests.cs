using CopilotLedger.Core.Telemetry;
using CopilotLedger.Otlp.Decoding;
using CopilotLedger.Otlp.Normalization;
using CopilotLedger.Tests.TestSupport;
using static CopilotLedger.Tests.TestSupport.OtlpRequests;

namespace CopilotLedger.Tests.Otlp;

public sealed class GenAiSpanNormalizerTests
{
    [Fact]
    public void NZ1_CopilotJson_ProducesOneInferenceSpanAndTwoLinks()
    {
        NormalizedTraceBatch batch = GenAiSpanNormalizer.Normalize(
            OtlpTraceDecoder.DecodeJson(OtlpFixtures.Utf8(OtlpFixtures.CopilotChatTraceJson)));

        Assert.Equal(2, batch.ReceivedSpanCount);
        Assert.Equal(0, batch.RejectedSpanCount);
        Assert.Null(batch.FirstRejectionReason);
        NormalizedGenAiSpan span = Assert.Single(batch.InferenceSpans);
        Assert.Equal("5b8efff798038103d269b633813fc60c", span.TraceId);
        Assert.Equal("eee19b7ec3c1b174", span.SpanId);
        Assert.Equal("eee19b7ec3c1b173", span.ParentSpanId);
        Assert.Equal("chat", span.OperationName);
        Assert.Equal(new DateTimeOffset(2026, 11, 20, 18, 33, 24, 707, TimeSpan.Zero), span.StartTime);
        Assert.Equal(new DateTimeOffset(2026, 11, 20, 18, 33, 26, 429, TimeSpan.Zero), span.EndTime);
        Assert.Equal("github", span.ProviderName);
        Assert.Equal("mai-code-2", span.RequestModel);
        Assert.Equal("mai-code-2", span.ResponseModel);
        Assert.Null(span.AgentName);
        Assert.Equal("0f8fad5b-d9cb-469f-a165-70867728950e", span.ConversationId);
        Assert.Null(span.ServerAddress);
        Assert.Null(span.ServerPort);
        Assert.Null(span.WorkingDirectory);
        Assert.Equal(17194, span.InputTokens);
        Assert.Equal(5, span.OutputTokens);
        Assert.Equal(3584, span.CacheReadInputTokens);
        Assert.Equal(0, span.CacheWriteInputTokens);

        Assert.Collection(
            batch.Links,
            link =>
            {
                Assert.Equal("eee19b7ec3c1b174", link.SpanId);
                Assert.Equal("eee19b7ec3c1b173", link.ParentSpanId);
                Assert.Equal("chat", link.OperationName);
                Assert.Null(link.AgentName);
                Assert.Equal("0f8fad5b-d9cb-469f-a165-70867728950e", link.ConversationId);
                AssertGitNull(link);
                Assert.Equal(new DateTimeOffset(2026, 11, 20, 18, 33, 24, 707, TimeSpan.Zero), link.StartTime);
            },
            link =>
            {
                Assert.Equal("eee19b7ec3c1b173", link.SpanId);
                Assert.Null(link.ParentSpanId);
                Assert.Equal("invoke_agent", link.OperationName);
                Assert.Null(link.AgentName);
                Assert.Equal("0f8fad5b-d9cb-469f-a165-70867728950e", link.ConversationId);
                AssertGitNull(link);
                Assert.Equal(new DateTimeOffset(2026, 11, 20, 18, 33, 24, 679, TimeSpan.Zero), link.StartTime);
            });
    }

    [Fact]
    public void NZ2_GoldenProtobuf_ProducesExpectedInferenceSpanAndLink()
    {
        NormalizedTraceBatch batch = GenAiSpanNormalizer.Normalize(
            OtlpTraceDecoder.DecodeProtobuf(OtlpFixtures.PGold1));

        NormalizedGenAiSpan span = Assert.Single(batch.InferenceSpans);
        Assert.Null(span.ProviderName);
        Assert.Null(span.ConversationId);
        Assert.Null(span.ResponseModel);
        Assert.Equal("mai-code-2", span.RequestModel);
        Assert.Equal(17194, span.InputTokens);
        Assert.Equal(5, span.OutputTokens);
        Assert.Equal(3584, span.CacheReadInputTokens);
        Assert.Equal(0, span.CacheWriteInputTokens);
        Assert.Equal("chat", Assert.Single(batch.Links).OperationName);
    }

    [Theory]
    [InlineData(300, 0, 300)]
    [InlineData(0, 200, 200)]
    [InlineData(300, 200, 300)]
    [InlineData(0, 0, 0)]
    public void NZ3_CacheWrite_FallsBackOnlyWhenPrimaryIsAbsent(
        long cacheWrite,
        long cacheCreation,
        long expected)
    {
        List<OtlpAttribute> attributes =
        [
            Str(GenAiAttributeNames.OperationName, "chat"),
            Int(GenAiAttributeNames.InputTokens, 1000),
        ];
        if (cacheWrite != 0)
        {
            attributes.Add(Int(GenAiAttributeNames.CacheWriteInputTokens, cacheWrite));
        }

        if (cacheCreation != 0)
        {
            attributes.Add(Int(GenAiAttributeNames.LegacyCacheCreationInputTokens, cacheCreation));
        }

        NormalizedGenAiSpan span = Assert.Single(GenAiSpanNormalizer.Normalize(
            Request(CopilotResource, Span(attributes: [.. attributes]))).InferenceSpans);

        Assert.Equal(expected, span.CacheWriteInputTokens);
        Assert.Equal(1000, span.InputTokens);
    }

    [Fact]
    public void NZ4_Input_IsExtendedWhenItExcludesCacheTokens()
    {
        NormalizedGenAiSpan span = Assert.Single(GenAiSpanNormalizer.Normalize(
            Request(
                CopilotResource,
                Span(attributes:
                [
                    Str(GenAiAttributeNames.OperationName, "chat"),
                    Int(GenAiAttributeNames.InputTokens, 100),
                    Int(GenAiAttributeNames.CacheReadInputTokens, 80),
                    Int(GenAiAttributeNames.CacheWriteInputTokens, 30),
                ]))).InferenceSpans);

        Assert.Equal(210, span.InputTokens);
        Assert.Equal(80, span.CacheReadInputTokens);
        Assert.Equal(30, span.CacheWriteInputTokens);
    }

    [Theory]
    [InlineData(0, "invalid traceId")]
    [InlineData(1, "invalid spanId")]
    [InlineData(2, "missing startTimeUnixNano")]
    [InlineData(3, "gen_ai.usage.input_tokens is not a non-negative integer")]
    [InlineData(4, "gen_ai.usage.output_tokens is not a non-negative integer")]
    [InlineData(5, "gen_ai.usage.cache_read.input_tokens is not a non-negative integer")]
    [InlineData(6, "token counts overflow")]
    public void NZ5_InvalidSpan_RejectsWithFirstOrderedReason(int caseNumber, string reason)
    {
        OtlpTraceRequest request = caseNumber switch
        {
            0 => Request(CopilotResource, Span(
                traceId: null,
                attributes: [Str(GenAiAttributeNames.OperationName, "chat")])),
            1 => Request(CopilotResource, Span(
                spanId: null,
                attributes: [Str(GenAiAttributeNames.OperationName, "chat")])),
            2 => Request(CopilotResource, Span(
                startNs: 0,
                attributes: [Str(GenAiAttributeNames.OperationName, "chat")])),
            3 => Request(CopilotResource, Span(attributes:
                [
                    Str(GenAiAttributeNames.OperationName, "chat"),
                    Dbl(GenAiAttributeNames.InputTokens),
                ])),
            4 => Request(CopilotResource, Span(attributes:
                [
                    Str(GenAiAttributeNames.OperationName, "chat"),
                    Int(GenAiAttributeNames.OutputTokens, -1),
                ])),
            5 => Request(CopilotResource, Span(attributes:
                [
                    Str(GenAiAttributeNames.OperationName, "chat"),
                    Str(GenAiAttributeNames.CacheReadInputTokens, "5"),
                ])),
            6 => Request(CopilotResource, Span(attributes:
                [
                    Str(GenAiAttributeNames.OperationName, "chat"),
                    Int(GenAiAttributeNames.InputTokens, 1),
                    Int(GenAiAttributeNames.CacheReadInputTokens, long.MaxValue),
                    Int(GenAiAttributeNames.CacheWriteInputTokens, 1),
                ])),
            _ => throw new ArgumentOutOfRangeException(nameof(caseNumber)),
        };

        NormalizedTraceBatch batch = GenAiSpanNormalizer.Normalize(request);

        Assert.Equal(1, batch.ReceivedSpanCount);
        Assert.Equal(1, batch.RejectedSpanCount);
        Assert.Equal(reason, batch.FirstRejectionReason);
        Assert.Empty(batch.InferenceSpans);
        Assert.Empty(batch.Links);
    }

    [Fact]
    public void NZ6_NonCopilotSpanWithoutOperation_IsIgnored()
    {
        NormalizedTraceBatch batch = GenAiSpanNormalizer.Normalize(
            Request([Str(GenAiAttributeNames.ServiceName, "my-app")], Span(attributes: [])));

        Assert.Equal(1, batch.ReceivedSpanCount);
        Assert.Equal(0, batch.RejectedSpanCount);
        Assert.Empty(batch.InferenceSpans);
        Assert.Empty(batch.Links);
    }

    [Fact]
    public void NZ7_CopilotSpanLinks_AllIndexedOperationsWithoutPricingTokenErrors()
    {
        NormalizedTraceBatch batch = GenAiSpanNormalizer.Normalize(
            Request(
                CopilotResource,
                Span(name: "plan", attributes: []),
                Span(
                    spanId: "eee19b7ec3c1b173",
                    attributes:
                    [
                        Str(GenAiAttributeNames.OperationName, "execute_tool"),
                        Dbl(GenAiAttributeNames.InputTokens),
                    ])));

        Assert.Collection(
            batch.Links,
            link => Assert.Null(link.OperationName),
            link => Assert.Equal("execute_tool", link.OperationName));
        Assert.Equal(0, batch.RejectedSpanCount);
        Assert.Empty(batch.InferenceSpans);
    }

    [Fact]
    public void NZ8_GitAttributes_UseCurrentThenLegacyAndSanitizeValues()
    {
        NormalizedTraceBatch batch = GenAiSpanNormalizer.Normalize(
            Request(
                [Str(GenAiAttributeNames.ServiceName, "copilot-chat")],
                Span(attributes:
                [
                    Str(GenAiAttributeNames.OperationName, "invoke_agent"),
                    Str(GenAiAttributeNames.AgentName, "GitHub Copilot Chat"),
                    Str(GenAiAttributeNames.GitRepository, "https://user:s3cr3t@github.com/contoso/webapp.git"),
                    Str(GenAiAttributeNames.GitBranch, "feature/login"),
                    Str(GenAiAttributeNames.GitCommitSha, "0123456789ABCDEF0123456789ABCDEF01234567"),
                ]),
                Span(
                    spanId: "eee19b7ec3c1b173",
                    attributes:
                    [
                        Str(GenAiAttributeNames.OperationName, "invoke_agent"),
                        Str(GenAiAttributeNames.LegacyRepoRemoteUrl, "git@github.com:contoso/api.git"),
                        Str(GenAiAttributeNames.LegacyRepoHeadBranchName, "main"),
                        Str(GenAiAttributeNames.LegacyRepoHeadCommitHash, "not-a-sha"),
                    ])));

        Assert.Collection(
            batch.Links,
            link =>
            {
                Assert.Equal("contoso/webapp", link.GitRepositorySlug);
                Assert.Equal("feature/login", link.GitBranch);
                Assert.Equal("0123456789abcdef0123456789abcdef01234567", link.GitCommitSha);
                Assert.Equal("GitHub Copilot Chat", link.AgentName);
            },
            link =>
            {
                Assert.Equal("contoso/api", link.GitRepositorySlug);
                Assert.Equal("main", link.GitBranch);
                Assert.Null(link.GitCommitSha);
            });
    }

    [Fact]
    public void NZ9_WorkingDirectoryAndServerPort_AcceptOnlyValidValues()
    {
        NormalizedGenAiSpan valid = Assert.Single(GenAiSpanNormalizer.Normalize(
            Request(
                [
                    Str(
                        GenAiAttributeNames.ProcessWorkingDirectory,
                        Path.GetTempPath()),
                ],
                Span(attributes:
                [
                    Str(GenAiAttributeNames.OperationName, "chat"),
                    Str(GenAiAttributeNames.ServerAddress, "127.0.0.1"),
                    Int(GenAiAttributeNames.ServerPort, 11434),
                ]))).InferenceSpans);
        NormalizedGenAiSpan invalid = Assert.Single(GenAiSpanNormalizer.Normalize(
            Request(
                [Str(GenAiAttributeNames.ProcessWorkingDirectory, "src/app")],
                Span(attributes:
                [
                    Str(GenAiAttributeNames.OperationName, "chat"),
                    Str(GenAiAttributeNames.ServerAddress, "127.0.0.1"),
                    Int(GenAiAttributeNames.ServerPort, 70000),
                ]))).InferenceSpans);

        Assert.Equal(Path.GetTempPath(), valid.WorkingDirectory);
        Assert.Equal("127.0.0.1", valid.ServerAddress);
        Assert.Equal(11434, valid.ServerPort);
        Assert.Null(invalid.WorkingDirectory);
        Assert.Null(invalid.ServerPort);
    }

    [Fact]
    public void NZ10_OrderingInferenceEndTimeAndFirstTextValue_AreHandled()
    {
        NormalizedGenAiSpan endBeforeStart = Assert.Single(GenAiSpanNormalizer.Normalize(
            Request(
                CopilotResource,
                Span(
                    endNs: 1795199604706999999,
                    attributes: [Str(GenAiAttributeNames.OperationName, "chat")]))).InferenceSpans);
        NormalizedTraceBatch inferenceOperations = GenAiSpanNormalizer.Normalize(
            Request(
                CopilotResource,
                Span(
                    attributes: [Str(GenAiAttributeNames.OperationName, "text_completion")]),
                Span(
                    spanId: "eee19b7ec3c1b173",
                    attributes: [Str(GenAiAttributeNames.OperationName, "generate_content")]),
                Span(
                    spanId: "eee19b7ec3c1b172",
                    attributes:
                    [
                        Str(GenAiAttributeNames.OperationName, "chat"),
                        Str(GenAiAttributeNames.RequestModel, "first"),
                        Str(GenAiAttributeNames.RequestModel, "second"),
                        Str(GenAiAttributeNames.AgentName, "   "),
                    ])));

        Assert.Equal(endBeforeStart.StartTime, endBeforeStart.EndTime);
        Assert.Collection(
            inferenceOperations.InferenceSpans,
            span => Assert.Equal("text_completion", span.OperationName),
            span => Assert.Equal("generate_content", span.OperationName),
            span =>
            {
                Assert.Equal("first", span.RequestModel);
                Assert.Null(span.AgentName);
            });
    }

    [Theory]
    [InlineData(0, 1, 0)]
    [InlineData(1, 0, 1000000000L)]
    [InlineData(2, 1, 0)]
    public void NZ11_TokenCap_RejectsOnlyCountsAboveOneBillion(
        int caseNumber,
        int expectedRejected,
        long expectedInput)
    {
        OtlpAttribute[] attributes = caseNumber switch
        {
            0 =>
            [
                Str(GenAiAttributeNames.OperationName, "chat"),
                Int(GenAiAttributeNames.OutputTokens, 1000000001),
            ],
            1 =>
            [
                Str(GenAiAttributeNames.OperationName, "chat"),
                Int(GenAiAttributeNames.InputTokens, 1000000000),
            ],
            2 =>
            [
                Str(GenAiAttributeNames.OperationName, "chat"),
                Int(GenAiAttributeNames.InputTokens, 100),
                Int(GenAiAttributeNames.CacheReadInputTokens, 600000000),
                Int(GenAiAttributeNames.CacheWriteInputTokens, 500000000),
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(caseNumber)),
        };

        NormalizedTraceBatch batch = GenAiSpanNormalizer.Normalize(
            Request(CopilotResource, Span(attributes: attributes)));

        Assert.Equal(1, batch.ReceivedSpanCount);
        Assert.Equal(expectedRejected, batch.RejectedSpanCount);
        if (expectedRejected == 1)
        {
            Assert.Empty(batch.InferenceSpans);
            Assert.Empty(batch.Links);
            Assert.Equal("token count exceeds 1000000000", batch.FirstRejectionReason);
        }
        else
        {
            Assert.Equal(expectedInput, Assert.Single(batch.InferenceSpans).InputTokens);
            Assert.Null(batch.FirstRejectionReason);
        }
    }

    [Fact]
    public void NZ12_ToolCallSignals_AreNormalizedOntoLinks()
    {
        const string hash =
            "c3d1fe831856c77818bb8c1242d32d6efa6c37366eb902e56c5fd0054c1dcbc3/918a44e755cc5eee09b08d214831e8339a645158ef1f831a0fa02bd43999700f";
        OtlpAttribute executeTool = Str("gen_ai.operation.name", "execute_tool");
        NormalizedTraceBatch batch = GenAiSpanNormalizer.Normalize(Request(
            CopilotResource,
            Span(
                spanId: "00000000000000a1",
                attributes:
                [
                    executeTool,
                    Str("gen_ai.tool.name", "powershell"),
                    Int("process.exit.code", 1),
                ]),
            Span(
                spanId: "00000000000000a2",
                attributes:
                [
                    executeTool,
                    Str("gen_ai.tool.name", "apply_patch"),
                ]),
            Span(
                spanId: "00000000000000a3",
                attributes:
                [
                    executeTool,
                    Str("gen_ai.tool.name", hash),
                ]) with
            {
                StatusCode = 2,
            },
            Span(
                spanId: "00000000000000a4",
                attributes:
                [
                    executeTool,
                    Str("gen_ai.tool.name", "powershell"),
                    Int("process.exit.code", -1073741510),
                ]),
            Span(
                spanId: "00000000000000a5",
                attributes:
                [
                    executeTool,
                    Str("gen_ai.tool.name", "powershell"),
                    Str("process.exit.code", "1"),
                ]),
            Span(
                spanId: "00000000000000a6",
                attributes:
                [
                    executeTool,
                    Int("gen_ai.tool.name", 7),
                ]),
            Span(
                spanId: "00000000000000c1",
                attributes:
                [
                    Str("gen_ai.operation.name", "chat"),
                    Str("gen_ai.request.model", "mai-code-2"),
                    Int("gen_ai.usage.input_tokens", 10),
                    Str("gen_ai.tool.name", "x"),
                    Int("process.exit.code", 3),
                ]) with
            {
                StatusCode = 1,
            },
            Span(spanId: "00000000000000b1", attributes: [])));

        Assert.Equal(8, batch.ReceivedSpanCount);
        Assert.Equal(0, batch.RejectedSpanCount);
        Assert.Equal("00000000000000c1", Assert.Single(batch.InferenceSpans).SpanId);
        Assert.Collection(
            batch.Links,
            link => AssertLink(link, "execute_tool", "powershell", 1, 0),
            link => AssertLink(link, "execute_tool", "apply_patch", null, 0),
            link => AssertLink(link, "execute_tool", hash, null, 2),
            link => AssertLink(link, "execute_tool", "powershell", -1073741510, 0),
            link => AssertLink(link, "execute_tool", "powershell", null, 0),
            link => AssertLink(link, "execute_tool", null, null, 0),
            link => AssertLink(link, "chat", null, null, 1),
            link => AssertLink(link, null, null, null, 0));
    }

    [Theory]
    [InlineData(0, CacheCounterState.Reported, CacheCounterState.Reported, 300, 100)]
    [InlineData(1, CacheCounterState.Reported, CacheCounterState.Absent, 300, 0)]
    [InlineData(2, CacheCounterState.Absent, CacheCounterState.Reported, 0, 200)]
    [InlineData(3, CacheCounterState.Absent, CacheCounterState.Absent, 0, 0)]
    [InlineData(4, CacheCounterState.Reported, CacheCounterState.Absent, 0, 0)]
    [InlineData(5, CacheCounterState.Absent, CacheCounterState.Reported, 0, 0)]
    [InlineData(6, CacheCounterState.Absent, CacheCounterState.Reported, 0, 0)]
    public void NZ13_CacheCounterStates_SayWhichCountersTheSpanCarried(
        int caseNumber,
        CacheCounterState readState,
        CacheCounterState writeState,
        long cacheRead,
        long cacheWrite)
    {
        OtlpAttribute[] cache = caseNumber switch
        {
            0 =>
            [
                Int(GenAiAttributeNames.CacheReadInputTokens, 300),
                Int(GenAiAttributeNames.CacheWriteInputTokens, 100),
            ],
            1 => [Int(GenAiAttributeNames.CacheReadInputTokens, 300)],
            2 => [Int(GenAiAttributeNames.LegacyCacheCreationInputTokens, 200)],
            3 => [],
            4 => [Int(GenAiAttributeNames.CacheReadInputTokens, 0)],
            5 => [Int(GenAiAttributeNames.LegacyCacheCreationInputTokens, 0)],
            6 =>
            [
                Int(GenAiAttributeNames.CacheWriteInputTokens, 0),
                Int(GenAiAttributeNames.LegacyCacheCreationInputTokens, 50),
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(caseNumber)),
        };

        NormalizedGenAiSpan span = Assert.Single(GenAiSpanNormalizer.Normalize(
            Request(
                CopilotResource,
                Span(attributes:
                [
                    Str(GenAiAttributeNames.OperationName, "chat"),
                    Int(GenAiAttributeNames.InputTokens, 1000),
                    Int(GenAiAttributeNames.OutputTokens, 7),
                    .. cache,
                ]))).InferenceSpans);

        Assert.Equal(readState, span.CacheReadState);
        Assert.Equal(writeState, span.CacheWriteState);
        Assert.Equal(cacheRead, span.CacheReadInputTokens);
        Assert.Equal(cacheWrite, span.CacheWriteInputTokens);
        Assert.Equal(1000, span.InputTokens);
        Assert.Equal(7, span.OutputTokens);
    }

    [Fact]
    public void NZ14_CopilotCapture_LeavesOutZeroCacheCounters()
    {
        NormalizedTraceBatch batch = GenAiSpanNormalizer.Normalize(CopilotTraces.TraceBA());

        Assert.Collection(
            batch.InferenceSpans,
            span =>
            {
                Assert.Equal("00f067aa0ba902bb", span.SpanId);
                Assert.Equal(CacheCounterState.Absent, span.CacheReadState);
                Assert.Equal(CacheCounterState.Reported, span.CacheWriteState);
                Assert.Equal(0, span.CacheReadInputTokens);
                Assert.Equal(3662, span.CacheWriteInputTokens);
                Assert.True(CacheMetrics.IsReported(span.CacheReadState, span.CacheWriteState));
            },
            span =>
            {
                Assert.Equal("00f067aa0ba902bd", span.SpanId);
                Assert.Equal(CacheCounterState.Reported, span.CacheReadState);
                Assert.Equal(CacheCounterState.Reported, span.CacheWriteState);
                Assert.Equal(3662, span.CacheReadInputTokens);
                Assert.Equal(99, span.CacheWriteInputTokens);
            },
            span =>
            {
                Assert.Equal("00f067aa0ba902b8", span.SpanId);
                Assert.Equal(CacheCounterState.Reported, span.CacheReadState);
                Assert.Equal(CacheCounterState.Absent, span.CacheWriteState);
                Assert.Equal(15872, span.CacheReadInputTokens);
                Assert.Equal(0, span.CacheWriteInputTokens);
            });
        NormalizedGenAiSpan unreported = Assert.Single(
            GenAiSpanNormalizer.Normalize(CopilotTraces.TraceF()).InferenceSpans);
        Assert.Equal(CacheCounterState.Absent, unreported.CacheReadState);
        Assert.Equal(CacheCounterState.Absent, unreported.CacheWriteState);
        Assert.False(CacheMetrics.IsReported(unreported.CacheReadState, unreported.CacheWriteState));
    }

    private static void AssertLink(
        GenAiSpanLink link,
        string? operationName,
        string? toolName,
        long? toolExitCode,
        int statusCode)
    {
        Assert.Equal(operationName, link.OperationName);
        Assert.Equal(toolName, link.ToolName);
        Assert.Equal(toolExitCode, link.ToolExitCode);
        Assert.Equal(statusCode, link.StatusCode);
    }

    private static void AssertGitNull(GenAiSpanLink link)
    {
        Assert.Null(link.GitRepositorySlug);
        Assert.Null(link.GitBranch);
        Assert.Null(link.GitCommitSha);
    }
}
