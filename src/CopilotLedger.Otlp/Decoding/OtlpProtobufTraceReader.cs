using System.Text;
using CopilotLedger.Core.Pricing;

namespace CopilotLedger.Otlp.Decoding;

internal static class OtlpProtobufTraceReader
{
    public static OtlpTraceRequest Decode(ReadOnlySpan<byte> payload)
    {
        List<OtlpResourceSpans> resourceSpans = [];
        ProtoReader reader = new(payload);
        while (reader.TryReadTag(out int field, out int wireType))
        {
            if (field == 1 && wireType == 2)
            {
                resourceSpans.Add(ReadResourceSpans(reader.ReadLengthDelimited()));
            }
            else
            {
                reader.Skip(wireType);
            }
        }

        return new OtlpTraceRequest(resourceSpans);
    }

    private static OtlpResourceSpans ReadResourceSpans(ReadOnlySpan<byte> payload)
    {
        IReadOnlyList<OtlpAttribute> resourceAttributes = [];
        List<OtlpSpan> spans = [];
        ProtoReader reader = new(payload);
        while (reader.TryReadTag(out int field, out int wireType))
        {
            if (wireType != 2)
            {
                reader.Skip(wireType);
                continue;
            }

            ReadOnlySpan<byte> value = reader.ReadLengthDelimited();
            if (field == 1)
            {
                resourceAttributes = ReadResource(value);
            }
            else if (field == 2)
            {
                spans.AddRange(ReadScopeSpans(value));
            }
        }

        return new OtlpResourceSpans(resourceAttributes, spans);
    }

    private static IReadOnlyList<OtlpAttribute> ReadResource(ReadOnlySpan<byte> payload)
    {
        List<OtlpAttribute> attributes = [];
        ProtoReader reader = new(payload);
        while (reader.TryReadTag(out int field, out int wireType))
        {
            if (field == 1 && wireType == 2)
            {
                OtlpAttribute? attribute = ReadAttribute(reader.ReadLengthDelimited());
                if (attribute is not null)
                {
                    attributes.Add(attribute);
                }
            }
            else
            {
                reader.Skip(wireType);
            }
        }

        return attributes;
    }

    private static IReadOnlyList<OtlpSpan> ReadScopeSpans(ReadOnlySpan<byte> payload)
    {
        List<OtlpSpan> spans = [];
        ProtoReader reader = new(payload);
        while (reader.TryReadTag(out int field, out int wireType))
        {
            if (field == 2 && wireType == 2)
            {
                spans.Add(ReadSpan(reader.ReadLengthDelimited()));
            }
            else
            {
                reader.Skip(wireType);
            }
        }

        return spans;
    }

    private static OtlpSpan ReadSpan(ReadOnlySpan<byte> payload)
    {
        string? traceId = null;
        string? spanId = null;
        string? parentSpanId = null;
        string name = "";
        ulong start = 0;
        ulong end = 0;
        int statusCode = 0;
        List<OtlpAttribute> attributes = [];
        ProtoReader reader = new(payload);
        while (reader.TryReadTag(out int field, out int wireType))
        {
            if ((field == 1 || field == 2 || field == 4 || field == 5 || field == 9 || field == 15) && wireType == 2)
            {
                ReadOnlySpan<byte> value = reader.ReadLengthDelimited();
                switch (field)
                {
                    case 1:
                        traceId = ReadId(value, 16);
                        break;
                    case 2:
                        spanId = ReadId(value, 8);
                        break;
                    case 4:
                        parentSpanId = ReadId(value, 8);
                        break;
                    case 5:
                        name = Encoding.UTF8.GetString(value);
                        break;
                    case 9:
                        OtlpAttribute? attribute = ReadAttribute(value);
                        if (attribute is not null)
                        {
                            attributes.Add(attribute);
                        }

                        break;
                    case 15:
                        statusCode = ReadStatus(value);
                        break;
                }
            }
            else if ((field == 7 || field == 8) && wireType == 1)
            {
                ulong value = reader.ReadFixed64();
                if (field == 7)
                {
                    start = value;
                }
                else
                {
                    end = value;
                }
            }
            else
            {
                reader.Skip(wireType);
            }
        }

        return new OtlpSpan
        {
            TraceId = traceId,
            SpanId = spanId,
            ParentSpanId = parentSpanId,
            Name = name,
            StartTimeUnixNano = start,
            EndTimeUnixNano = end,
            StatusCode = statusCode,
            Attributes = attributes,
        };
    }

    private static int ReadStatus(ReadOnlySpan<byte> payload)
    {
        int statusCode = 0;
        ProtoReader reader = new(payload);
        while (reader.TryReadTag(out int field, out int wireType))
        {
            if (field == 3 && wireType == 0)
            {
                ulong value = reader.ReadVarint();
                statusCode = value is 1 or 2 ? (int)value : 0;
            }
            else
            {
                reader.Skip(wireType);
            }
        }

        return statusCode;
    }

    private static OtlpAttribute? ReadAttribute(ReadOnlySpan<byte> payload)
    {
        string? key = null;
        OtlpAnyValue value = OtlpAnyValue.Empty;
        ProtoReader reader = new(payload);
        while (reader.TryReadTag(out int field, out int wireType))
        {
            if ((field == 1 || field == 2) && wireType == 2)
            {
                ReadOnlySpan<byte> fieldValue = reader.ReadLengthDelimited();
                if (field == 1)
                {
                    key = Encoding.UTF8.GetString(fieldValue);
                }
                else
                {
                    value = ReadAnyValue(fieldValue);
                }
            }
            else
            {
                reader.Skip(wireType);
            }
        }

        return string.IsNullOrEmpty(key) ? null : new OtlpAttribute(key, value);
    }

    private static OtlpAnyValue ReadAnyValue(ReadOnlySpan<byte> payload)
    {
        OtlpAnyValue value = OtlpAnyValue.Empty;
        ProtoReader reader = new(payload);
        while (reader.TryReadTag(out int field, out int wireType))
        {
            if (field == 1 && wireType == 2)
            {
                value = new OtlpAnyValue
                {
                    Kind = OtlpValueKind.String,
                    StringValue = Encoding.UTF8.GetString(reader.ReadLengthDelimited()),
                };
            }
            else if (field == 2 && wireType == 0)
            {
                value = new OtlpAnyValue { Kind = OtlpValueKind.Bool, BoolValue = reader.ReadVarint() != 0 };
            }
            else if (field == 3 && wireType == 0)
            {
                value = new OtlpAnyValue { Kind = OtlpValueKind.Int, IntValue = (long)reader.ReadVarint() };
            }
            else if (field == 4 && wireType == 1)
            {
                ulong bits = reader.ReadFixed64();
                value = NanoAiu.TryFromIeee754Bits(bits, out long nanoAiu)
                    ? new OtlpAnyValue
                    {
                        Kind = OtlpValueKind.Double,
                        DoubleAsNanoAiu = nanoAiu,
                    }
                    : new OtlpAnyValue { Kind = OtlpValueKind.Double };
            }
            else if (field == 5 && wireType == 2)
            {
                _ = reader.ReadLengthDelimited();
                value = new OtlpAnyValue { Kind = OtlpValueKind.Array };
            }
            else if (field == 6 && wireType == 2)
            {
                _ = reader.ReadLengthDelimited();
                value = new OtlpAnyValue { Kind = OtlpValueKind.KeyValueList };
            }
            else if (field == 7 && wireType == 2)
            {
                _ = reader.ReadLengthDelimited();
                value = new OtlpAnyValue { Kind = OtlpValueKind.Bytes };
            }
            else
            {
                reader.Skip(wireType);
            }
        }

        return value;
    }

    private static string? ReadId(ReadOnlySpan<byte> value, int expectedLength)
    {
        if (value.Length != expectedLength || value.IndexOfAnyExcept((byte)0) < 0)
        {
            return null;
        }

        return Convert.ToHexStringLower(value);
    }
}
