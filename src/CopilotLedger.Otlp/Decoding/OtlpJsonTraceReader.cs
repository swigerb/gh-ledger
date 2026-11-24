using System.Text;
using System.Globalization;
using System.Text.Json;
using CopilotLedger.Core.Pricing;

namespace CopilotLedger.Otlp.Decoding;

internal static class OtlpJsonTraceReader
{
    private static readonly JsonElement EmptyArray = JsonDocument.Parse("[]").RootElement.Clone();

    public static OtlpTraceRequest Decode(ReadOnlyMemory<byte> utf8Json)
    {
        if (utf8Json.Length >= 3 &&
            utf8Json.Span[0] == 0xef &&
            utf8Json.Span[1] == 0xbb &&
            utf8Json.Span[2] == 0xbf)
        {
            utf8Json = utf8Json[3..];
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(
                utf8Json,
                new JsonDocumentOptions
                {
                    MaxDepth = 64,
                    CommentHandling = JsonCommentHandling.Disallow,
                    AllowTrailingCommas = false,
                });

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new OtlpDecodeException("OTLP trace request must be an object");
            }

            List<OtlpResourceSpans> resourceSpans = [];
            JsonElement rootResourceSpans = GetArray(document.RootElement, "resourceSpans");
            foreach (JsonElement resourceSpan in rootResourceSpans.EnumerateArray())
            {
                RequireObject(resourceSpan, "resourceSpans element");
                IReadOnlyList<OtlpAttribute> resourceAttributes = [];
                if (TryFindProperty(resourceSpan, "resource", out JsonElement resource))
                {
                    if (resource.ValueKind != JsonValueKind.Null)
                    {
                        RequireObject(resource, "resource");
                        resourceAttributes = ReadAttributes(GetArray(resource, "attributes"));
                    }
                }

                List<OtlpSpan> spans = [];
                JsonElement scopeSpans = GetArray(resourceSpan, "scopeSpans");
                foreach (JsonElement scopeSpan in scopeSpans.EnumerateArray())
                {
                    RequireObject(scopeSpan, "scopeSpans element");
                    foreach (JsonElement span in GetArray(scopeSpan, "spans").EnumerateArray())
                    {
                        RequireObject(span, "spans element");
                        spans.Add(ReadSpan(span));
                    }
                }

                resourceSpans.Add(new OtlpResourceSpans(resourceAttributes, spans));
            }

            return new OtlpTraceRequest(resourceSpans);
        }
        catch (JsonException exception)
        {
            throw new OtlpDecodeException("Invalid JSON: " + exception.Message, exception);
        }
    }

    private static OtlpSpan ReadSpan(JsonElement element)
    {
        return new OtlpSpan
        {
            TraceId = ReadId(element, "traceId", 32),
            SpanId = ReadId(element, "spanId", 16),
            ParentSpanId = ReadId(element, "parentSpanId", 16),
            Name = ReadStringProperty(element, "name") ?? "",
            StartTimeUnixNano = ReadUInt64Property(element, "startTimeUnixNano"),
            EndTimeUnixNano = ReadUInt64Property(element, "endTimeUnixNano"),
            StatusCode = ReadStatusCode(element),
            Attributes = ReadAttributes(GetArray(element, "attributes")),
        };
    }

    private static int ReadStatusCode(JsonElement element)
    {
        if (!TryFindProperty(element, "status", out JsonElement status) ||
            status.ValueKind != JsonValueKind.Object ||
            !TryFindProperty(status, "code", out JsonElement code) ||
            code.ValueKind != JsonValueKind.Number ||
            !code.TryGetInt32(out int value))
        {
            return 0;
        }

        return value is 1 or 2 ? value : 0;
    }

    private static IReadOnlyList<OtlpAttribute> ReadAttributes(JsonElement attributes)
    {
        List<OtlpAttribute> result = [];
        foreach (JsonElement attribute in attributes.EnumerateArray())
        {
            if (attribute.ValueKind != JsonValueKind.Object ||
                !TryReadStringProperty(attribute, "key", out string? key) ||
                string.IsNullOrEmpty(key))
            {
                continue;
            }

            OtlpAnyValue value = OtlpAnyValue.Empty;
            if (TryFindProperty(attribute, "value", out JsonElement valueElement) &&
                valueElement.ValueKind == JsonValueKind.Object)
            {
                value = ReadAnyValue(valueElement);
            }

            result.Add(new OtlpAttribute(key, value));
        }

        return result;
    }

    private static OtlpAnyValue ReadAnyValue(JsonElement element)
    {
        foreach (JsonProperty property in element.EnumerateObject())
        {
            JsonElement value = property.Value;
            if (NameIs(property, "stringValue"))
            {
                return TryGetString(value, out string? text)
                    ? new OtlpAnyValue { Kind = OtlpValueKind.String, StringValue = text }
                    : OtlpAnyValue.Empty;
            }

            if (NameIs(property, "boolValue"))
            {
                return value.ValueKind is JsonValueKind.True or JsonValueKind.False
                    ? new OtlpAnyValue { Kind = OtlpValueKind.Bool, BoolValue = value.GetBoolean() }
                    : OtlpAnyValue.Empty;
            }

            if (NameIs(property, "intValue"))
            {
                if (TryGetInt64(value, out long number))
                {
                    return new OtlpAnyValue { Kind = OtlpValueKind.Int, IntValue = number };
                }

                return OtlpAnyValue.Empty;
            }

            if (NameIs(property, "doubleValue"))
            {
                if (value.ValueKind == JsonValueKind.Number &&
                    NanoAiu.TryFromJsonNumber(Encoding.UTF8.GetBytes(value.GetRawText()), out long nanoAiu))
                {
                    return new OtlpAnyValue
                    {
                        Kind = OtlpValueKind.Double,
                        DoubleAsNanoAiu = nanoAiu,
                    };
                }

                return new OtlpAnyValue { Kind = OtlpValueKind.Double };
            }

            if (NameIs(property, "arrayValue"))
            {
                return new OtlpAnyValue { Kind = OtlpValueKind.Array };
            }

            if (NameIs(property, "kvlistValue"))
            {
                return new OtlpAnyValue { Kind = OtlpValueKind.KeyValueList };
            }

            if (NameIs(property, "bytesValue"))
            {
                return new OtlpAnyValue { Kind = OtlpValueKind.Bytes };
            }
        }

        return OtlpAnyValue.Empty;
    }

    private static JsonElement GetArray(JsonElement parent, string propertyName)
    {
        if (!TryFindProperty(parent, propertyName, out JsonElement value) ||
            value.ValueKind == JsonValueKind.Null)
        {
            return EmptyArray;
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            throw new OtlpDecodeException($"{propertyName} must be an array");
        }

        return value;
    }

    private static void RequireObject(JsonElement element, string description)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new OtlpDecodeException($"{description} must be an object");
        }
    }

    private static string? ReadId(JsonElement element, string propertyName, int length)
    {
        string? value = ReadStringProperty(element, propertyName);
        if (value is null ||
            value.Length != length ||
            !value.All(IsHex) ||
            value.All(character => character == '0'))
        {
            return null;
        }

        return value.ToLowerInvariant();
    }

    private static bool IsHex(char character) =>
        character is (>= '0' and <= '9') or (>= 'a' and <= 'f') or (>= 'A' and <= 'F');

    private static string? ReadStringProperty(JsonElement element, string propertyName) =>
        TryReadStringProperty(element, propertyName, out string? value) ? value : null;

    private static bool TryReadStringProperty(JsonElement element, string propertyName, out string? value)
    {
        value = null;
        return TryFindProperty(element, propertyName, out JsonElement property) &&
               TryGetString(property, out value);
    }

    private static bool TryGetString(JsonElement element, out string? value)
    {
        value = null;
        if (element.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        try
        {
            value = element.GetString()!;
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static ulong ReadUInt64Property(JsonElement element, string propertyName)
    {
        if (!TryFindProperty(element, propertyName, out JsonElement property))
        {
            return 0;
        }

        if (TryGetString(property, out string? text) &&
            ulong.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out ulong parsedString))
        {
            return parsedString;
        }

        return property.ValueKind == JsonValueKind.Number && property.TryGetUInt64(out ulong parsedNumber)
            ? parsedNumber
            : 0;
    }

    // NameEquals throws InvalidOperationException for some escaped lone surrogates; such a name is an unknown property.
    private static bool NameIs(JsonProperty property, string name)
    {
        try
        {
            return property.NameEquals(name);
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    // Like TryGetProperty, including returning the last match for a repeated name, but it never throws for an unreadable name.
    private static bool TryFindProperty(JsonElement element, string name, out JsonElement value)
    {
        value = default;
        bool found = false;
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (NameIs(property, name))
            {
                value = property.Value;
                found = true;
            }
        }

        return found;
    }

    private static bool TryGetInt64(JsonElement element, out long value)
    {
        value = 0;
        if (TryGetString(element, out string? text))
        {
            return long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value);
        }

        return element.ValueKind == JsonValueKind.Number && element.TryGetInt64(out value);
    }

}
