using System.Text.Json;
using CopilotLedger.Core.Pricing;

namespace CopilotLedger.Core.Import;

// Deny-by-default reading of one JSON Lines event with Utf8JsonReader. ReadType finds the top-level "type" without reading
// any other value; Read then takes only the allowlisted paths of a known type, skipping everything else unread. Exception
// messages are never kept, because they can quote the line.
internal sealed class JsonFields
{
    private static readonly JsonReaderOptions Options = new() { MaxDepth = 256 };

    private readonly Dictionary<string, Value> _values = new(StringComparer.Ordinal);

    private JsonFields()
    {
    }

    // The path of the first allowlisted property that appeared twice in one object, or null.
    internal string? Duplicate { get; private set; }

    // The index in types of the line's string "type", -1 for a type that isn't in types, or null when the line isn't one JSON
    // object with exactly one string "type" (a malformed line). Reads no other value.
    internal static int? ReadType(ReadOnlySpan<byte> line, IReadOnlyList<string> types)
    {
        try
        {
            Utf8JsonReader reader = new(line, Options);
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
            {
                return null;
            }

            int? found = null;
            while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
            {
                bool isType = TextEquals(ref reader, "type");
                reader.Read();
                if (!isType)
                {
                    reader.Skip();
                    continue;
                }

                if (found is not null || reader.TokenType != JsonTokenType.String)
                {
                    return null;
                }

                found = -1;
                for (int index = 0; index < types.Count; index++)
                {
                    if (TextEquals(ref reader, types[index]))
                    {
                        found = index;
                        break;
                    }
                }
            }

            // Content after the object makes the next Read throw.
            return reader.Read() ? null : found;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // The index in types of the top-level "type" string in a JSON object prefix, -1 for another string, or null when the
    // prefix doesn't contain a complete type value. The first top-level "type" decides.
    internal static int? ReadTypePrefix(ReadOnlySpan<byte> prefix, IReadOnlyList<string> types)
    {
        Utf8JsonReader reader = new(prefix, isFinalBlock: false, new JsonReaderState(Options));
        try
        {
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
            {
                return null;
            }

            while (true)
            {
                if (!reader.Read() || reader.TokenType == JsonTokenType.EndObject)
                {
                    return null;
                }

                if (TextEquals(ref reader, "type"))
                {
                    break;
                }

                if (!reader.Read() || !reader.TrySkip())
                {
                    return null;
                }
            }

            if (!reader.Read() || reader.TokenType != JsonTokenType.String)
            {
                return null;
            }

            for (int index = 0; index < types.Count; index++)
            {
                if (TextEquals(ref reader, types[index]))
                {
                    return index;
                }
            }

            return -1;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // The allowlisted values of a line that ReadType accepted.
    internal static JsonFields Read(ReadOnlySpan<byte> line, PathTree paths)
    {
        JsonFields fields = new();
        try
        {
            Utf8JsonReader reader = new(line, Options);
            reader.Read();
            ReadObject(ref reader, paths.Root, fields);
        }
        catch (JsonException)
        {
            fields.Duplicate ??= "";
        }

        return fields;
    }

    internal bool IsPresent(string path) =>
        _values.TryGetValue(path, out Value value) && value.Token != JsonTokenType.Null;

    internal bool IsObject(string path) =>
        _values.TryGetValue(path, out Value value) && value.Token == JsonTokenType.StartObject;

    // A string of 1 to the path's maximum length without control characters, or null.
    internal string? String(string path) =>
        _values.TryGetValue(path, out Value value) && value.Token == JsonTokenType.String ? value.Text : null;

    // An integer that fits in a long, or null (1.0, 1e3, and strings are null).
    internal long? Integer(string path) =>
        _values.TryGetValue(path, out Value value) && value.Token == JsonTokenType.Number ? value.Integer : null;

    internal bool? Boolean(string path) =>
        _values.TryGetValue(path, out Value value)
            ? value.Token switch
            {
                JsonTokenType.True => true,
                JsonTokenType.False => false,
                _ => null,
            }
            : null;

    internal ReportedCharge NanoAiu(string path)
    {
        if (!_values.TryGetValue(path, out Value value) || value.Token == JsonTokenType.Null)
        {
            return ReportedCharge.Absent;
        }

        return value.Token == JsonTokenType.Number && value.NanoAiu is long nanoAiu
            ? ReportedCharge.Valid(nanoAiu)
            : ReportedCharge.Malformed;
    }

    private static void ReadObject(ref Utf8JsonReader reader, PathTree.Node node, JsonFields fields)
    {
        List<PathTree.Node>? seen = null;
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            PathTree.Node? child = null;
            foreach (PathTree.Node candidate in node.Children)
            {
                if (TextEquals(ref reader, candidate.Name))
                {
                    child = candidate;
                    break;
                }
            }

            reader.Read();
            if (child is null)
            {
                reader.Skip();
                continue;
            }

            seen ??= [];
            if (seen.Contains(child))
            {
                fields.Duplicate ??= child.Path;
                reader.Skip();
                continue;
            }

            seen.Add(child);
            if (child.Children.Count > 0 && reader.TokenType == JsonTokenType.StartObject)
            {
                fields._values[child.Path] = new Value(JsonTokenType.StartObject, null, null);
                ReadObject(ref reader, child, fields);
            }
            else
            {
                fields._values[child.Path] = ReadValue(ref reader, child.MaxLength);
            }
        }
    }

    private static Value ReadValue(ref Utf8JsonReader reader, int maxLength)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                if (maxLength == 0 || reader.ValueSpan.Length > 6 * maxLength)
                {
                    return new Value(JsonTokenType.String, null, null);
                }

                try
                {
                    string text = reader.GetString()!;
                    return new Value(
                        JsonTokenType.String,
                        text.Length >= 1 && text.Length <= maxLength && !text.Any(char.IsControl) ? text : null,
                        null);
                }
                catch (InvalidOperationException)
                {
                    return new Value(JsonTokenType.String, null, null);
                }

            case JsonTokenType.Number:
                return new Value(
                    JsonTokenType.Number,
                    null,
                    reader.TryGetInt64(out long number) ? number : null,
                    Pricing.NanoAiu.TryFromJsonNumber(reader.ValueSpan, out long nanoAiu) ? nanoAiu : null);
            case JsonTokenType.StartObject:
            case JsonTokenType.StartArray:
                JsonTokenType token = reader.TokenType;
                reader.Skip();
                return new Value(token, null, null);
            default:
                return new Value(reader.TokenType, null, null);
        }
    }

    // Whether the property name or string at the reader is text. ValueTextEquals throws InvalidOperationException for an
    // escaped lone surrogate, which it can't unescape; a name or string that holds one is never one of the parser's ASCII
    // names or types.
    private static bool TextEquals(ref Utf8JsonReader reader, string text)
    {
        try
        {
            return reader.ValueTextEquals(text);
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private readonly record struct Value(JsonTokenType Token, string? Text, long? Integer, long? NanoAiu = null);
}

// The allowlisted property paths of one event type ("data.usage.inputTokens"), each with the longest string it may hold
// (0 for numbers and booleans).
internal sealed class PathTree
{
    internal PathTree(params (string Path, int MaxLength)[] leaves)
    {
        foreach ((string path, int maxLength) in leaves)
        {
            Node node = Root;
            string[] names = path.Split('.');
            for (int index = 0; index < names.Length; index++)
            {
                Node? child = node.Children.Find(item => item.Name == names[index]);
                if (child is null)
                {
                    child = new Node(names[index], string.Join('.', names[..(index + 1)]));
                    node.Children.Add(child);
                }

                if (index == names.Length - 1)
                {
                    child.MaxLength = maxLength;
                }

                node = child;
            }
        }
    }

    internal Node Root { get; } = new("", "");

    internal sealed class Node(string name, string path)
    {
        internal string Name { get; } = name;
        internal string Path { get; } = path;
        internal int MaxLength { get; set; }
        internal List<Node> Children { get; } = [];
    }
}
