using CopilotLedger.Core.Pricing;
using CopilotLedger.Core.Telemetry;
using CopilotLedger.Otlp.Decoding;
using CopilotLedger.Otlp.Normalization;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Otlp;

public sealed class NanoAiuAttributeTests
{
    [Fact]
    public void NO1_JsonDoubleValue_ExactInteger_IsValid()
    {
        Assert.Equal(
            ReportedCharge.Valid(47_388_600),
            NormalizeJson("chat", """{"doubleValue":47388600}""").ReportedCharge);
    }

    public static TheoryData<string> NO2Values => new()
    {
        """{"intValue":"47388600"}""",
        """{"intValue":47388600}""",
    };

    [Theory]
    [MemberData(nameof(NO2Values))]
    public void NO2_JsonIntValue_IsValid(string valueJson)
    {
        Assert.Equal(
            ReportedCharge.Valid(47_388_600),
            NormalizeJson("chat", valueJson).ReportedCharge);
    }

    public static TheoryData<string> NO3Values => new()
    {
        """{"doubleValue":0.5}""",
        """{"doubleValue":-1}""",
        """{"doubleValue":9007199254740993}""",
        """{"doubleValue":"47388600"}""",
        """{"doubleValue":"NaN"}""",
        """{"doubleValue":"Infinity"}""",
    };

    [Theory]
    [MemberData(nameof(NO3Values))]
    public void NO3_JsonDoubleValue_InvalidInteger_IsMalformed(string valueJson)
    {
        Assert.Equal(ReportedCharge.Malformed, NormalizeJson("chat", valueJson).ReportedCharge);
    }

    public static TheoryData<string> NO4Values => new()
    {
        """{"stringValue":"47388600"}""",
        """{"boolValue":true}""",
    };

    [Theory]
    [MemberData(nameof(NO4Values))]
    public void NO4_JsonNonNumericValue_IsMalformed(string valueJson)
    {
        Assert.Equal(ReportedCharge.Malformed, NormalizeJson("chat", valueJson).ReportedCharge);
    }

    [Fact]
    public void NO5_ProtobufDoubleValue_ExactInteger_IsValid()
    {
        Assert.Equal(
            ReportedCharge.Valid(47_388_600),
            NormalizeProtobuf(new OtlpProtoWriter().Fixed64(4, 0x418698BDC0000000)).ReportedCharge);
    }

    public static TheoryData<ulong> NO6Bits => new()
    {
        0x7FF8000000000000,
        0x3FE0000000000000,
        0xBFF0000000000000,
        0x4340000000000001,
    };

    [Theory]
    [MemberData(nameof(NO6Bits))]
    public void NO6_ProtobufDoubleValue_InvalidInteger_IsMalformed(ulong bits)
    {
        Assert.Equal(
            ReportedCharge.Malformed,
            NormalizeProtobuf(new OtlpProtoWriter().Fixed64(4, bits)).ReportedCharge);
    }

    [Fact]
    public void NO7_ProtobufNegativeIntValue_IsMalformed()
    {
        Assert.Equal(
            ReportedCharge.Malformed,
            NormalizeProtobuf(new OtlpProtoWriter().Varint(3, unchecked((ulong)-5))).ReportedCharge);
    }

    [Fact]
    public void NO8_NonInferenceSpan_DoesNotReadReportedCharge()
    {
        NormalizedTraceBatch batch = NormalizeJsonBatch("invoke_agent", """{"doubleValue":47388600}""");

        Assert.Empty(batch.InferenceSpans);
        Assert.Single(batch.Links);
    }

    [Fact]
    public void NO9_ChatSpanWithoutAttribute_HasAbsentReportedCharge()
    {
        Assert.Equal(ReportedCharge.Absent, NormalizeJson("chat", valueJson: null).ReportedCharge);
    }

    [Fact]
    public void NO10_JsonFractionalDoubleValue_IsRecognizedWithoutNanoAiu()
    {
        OtlpTraceRequest request = DecodeJson("chat", """{"doubleValue":1.5}""");
        OtlpAttribute attribute = Assert.Single(
            Assert.Single(Assert.Single(request.ResourceSpans).Spans).Attributes,
            candidate => candidate.Key == GenAiAttributeNames.NanoAiu);

        Assert.Equal(OtlpValueKind.Double, attribute.Value.Kind);
        Assert.Null(attribute.Value.DoubleAsNanoAiu);
    }

    private static NormalizedGenAiSpan NormalizeJson(string operation, string? valueJson) =>
        Assert.Single(NormalizeJsonBatch(operation, valueJson).InferenceSpans);

    private static NormalizedTraceBatch NormalizeJsonBatch(string operation, string? valueJson) =>
        GenAiSpanNormalizer.Normalize(DecodeJson(operation, valueJson));

    private static OtlpTraceRequest DecodeJson(string operation, string? valueJson)
    {
        string reportedChargeAttribute = valueJson is null
            ? ""
            : """,{"key":"github.copilot.nano_aiu","value":""" + valueJson + "}";
        string json = """
            {"resourceSpans":[{"scopeSpans":[{"spans":[{"traceId":"5b8efff798038103d269b633813fc60c","spanId":"eee19b7ec3c1b174","startTimeUnixNano":"1795199604707000000","attributes":[{"key":"gen_ai.operation.name","value":{"stringValue":"__OPERATION__"}}__REPORTED__]}]}]}]}
            """
            .Replace("__OPERATION__", operation, StringComparison.Ordinal)
            .Replace("__REPORTED__", reportedChargeAttribute, StringComparison.Ordinal);

        return OtlpTraceDecoder.DecodeJson(OtlpFixtures.Utf8(json));
    }

    private static NormalizedGenAiSpan NormalizeProtobuf(OtlpProtoWriter anyValue)
    {
        OtlpProtoWriter operationAttribute = new OtlpProtoWriter()
            .String(1, GenAiAttributeNames.OperationName)
            .Message(2, new OtlpProtoWriter().String(1, "chat"));
        OtlpProtoWriter nanoAiuAttribute = new OtlpProtoWriter()
            .String(1, GenAiAttributeNames.NanoAiu)
            .Message(2, anyValue);
        OtlpProtoWriter span = new OtlpProtoWriter()
            .Bytes(1, Convert.FromHexString("5b8efff798038103d269b633813fc60c"))
            .Bytes(2, Convert.FromHexString("eee19b7ec3c1b174"))
            .Fixed64(7, 1)
            .Message(9, operationAttribute)
            .Message(9, nanoAiuAttribute);
        OtlpProtoWriter scopeSpans = new OtlpProtoWriter().Message(2, span);
        OtlpProtoWriter resourceSpans = new OtlpProtoWriter().Message(2, scopeSpans);
        byte[] payload = new OtlpProtoWriter().Message(1, resourceSpans).ToArray();

        return Assert.Single(
            GenAiSpanNormalizer.Normalize(OtlpTraceDecoder.DecodeProtobuf(payload)).InferenceSpans);
    }
}
