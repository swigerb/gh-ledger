using System.Globalization;
using System.Text.Json;

namespace CopilotLedger.Core.Pricing;

internal static class RateCardReader
{
    // Reads a rates document into the RateCardDocument classes. isBase selects which properties are required.
    // Throws InvalidDataException with a "<location>: <problem>" or "<problem>" message; the caller adds "<source>: ".
    public static RateCardDocument Read(string json, bool isBase)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(
                json,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = 64,
                });
        }
        catch (JsonException ex)
        {
            long line = ex.LineNumber + 1 ?? 1;
            long column = ex.BytePositionInLine + 1 ?? 1;
            throw Error(
                string.Empty,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"line {line}, column {column}: invalid JSON"),
                ex);
        }

        using (document)
        {
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw Error(string.Empty, "must contain a JSON object");
            }

            int schemaVersion = ValidateSchemaVersion(root);
            return ReadRoot(root, isBase, schemaVersion);
        }
    }

    private static RateCardDocument ReadRoot(JsonElement root, bool isBase, int schemaVersion)
    {
        string? ratesVersion = null;
        RateCardSource? source = null;
        IReadOnlyList<string>? notes = null;
        PricedRateSection? githubAiCredits = null;
        FoundryRateSection? foundryByok = null;
        LocalRateSection? localOffload = null;
        ApiEquivalentSection? apiEquivalent = null;
        HashSet<string> seen = new(StringComparer.Ordinal);
        string[] schema =
        [
            "schemaVersion",
            "ratesVersion",
            "source",
            "notes",
            "githubAiCredits",
            "foundryByok",
            "localOffload",
            "apiEquivalent",
        ];

        foreach (JsonProperty property in root.EnumerateObject())
        {
            (string name, string path) = ReadProperty(property, string.Empty, schema, seen);
            switch (name)
            {
                case "schemaVersion":
                    if (property.Value.ValueKind != JsonValueKind.Null)
                    {
                        _ = ReadInt32(property.Value, path);
                    }

                    break;
                case "ratesVersion":
                    ratesVersion = ReadOptionalString(property.Value, path);
                    break;
                case "source":
                    source = property.Value.ValueKind == JsonValueKind.Null
                        ? null
                        : ReadSource(property.Value, path, isBase);
                    break;
                case "notes":
                    notes = property.Value.ValueKind == JsonValueKind.Null
                        ? null
                        : ReadStringArray(property.Value, path);
                    break;
                case "githubAiCredits":
                    githubAiCredits = property.Value.ValueKind == JsonValueKind.Null
                        ? null
                        : ReadGithubSection(property.Value, path, isBase);
                    break;
                case "foundryByok":
                    foundryByok = property.Value.ValueKind == JsonValueKind.Null
                        ? null
                        : ReadFoundrySection(property.Value, path, isBase);
                    break;
                case "localOffload":
                    localOffload = property.Value.ValueKind == JsonValueKind.Null
                        ? null
                        : ReadLocalSection(property.Value, path, isBase);
                    break;
                case "apiEquivalent":
                    if (schemaVersion < 2)
                    {
                        throw Error(path, "is allowed only when schemaVersion is 2");
                    }

                    apiEquivalent = property.Value.ValueKind == JsonValueKind.Null
                        ? null
                        : ReadApiEquivalentSection(property.Value, path, isBase);
                    break;
            }
        }

        if (isBase)
        {
            Require(ratesVersion is not null, "ratesVersion", "is required");
            Require(source is not null, "source", "is required");
            Require(githubAiCredits is not null, "githubAiCredits", "is required");
            Require(foundryByok is not null, "foundryByok", "is required");
            Require(localOffload is not null, "localOffload", "is required");
            Require(schemaVersion < 2 || apiEquivalent is not null, "apiEquivalent", "is required");
        }

        return new RateCardDocument
        {
            SchemaVersion = schemaVersion,
            RatesVersion = ratesVersion,
            Source = source,
            Notes = notes,
            GithubAiCredits = githubAiCredits,
            FoundryByok = foundryByok,
            LocalOffload = localOffload,
            ApiEquivalent = apiEquivalent,
        };
    }

    private static RateCardSource ReadSource(JsonElement value, string path, bool isBase)
    {
        RequireObject(value, path);
        string? url = null;
        string? retrievedOn = null;
        HashSet<string> seen = new(StringComparer.Ordinal);
        string[] schema = ["url", "retrievedOn"];
        foreach (JsonProperty property in value.EnumerateObject())
        {
            (string name, string propertyPath) = ReadProperty(property, path, schema, seen);
            switch (name)
            {
                case "url":
                    url = ReadOptionalString(property.Value, propertyPath);
                    break;
                case "retrievedOn":
                    retrievedOn = ReadOptionalString(property.Value, propertyPath);
                    break;
            }
        }

        if (isBase)
        {
            Require(url is not null, Join(path, "url"), "is required");
            Require(retrievedOn is not null, Join(path, "retrievedOn"), "is required");
        }

        return new RateCardSource { Url = url, RetrievedOn = retrievedOn };
    }

    private static PricedRateSection ReadGithubSection(JsonElement value, string path, bool isBase)
    {
        RequireObject(value, path);
        IReadOnlyList<string>? hostSuffixes = null;
        IReadOnlyList<PricedModelDocument>? models = null;
        HashSet<string> seen = new(StringComparer.Ordinal);
        string[] schema = ["hostSuffixes", "models"];
        foreach (JsonProperty property in value.EnumerateObject())
        {
            (string name, string propertyPath) = ReadProperty(property, path, schema, seen);
            switch (name)
            {
                case "hostSuffixes":
                    hostSuffixes = property.Value.ValueKind == JsonValueKind.Null
                        ? null
                        : ReadStringArray(property.Value, propertyPath);
                    break;
                case "models":
                    models = property.Value.ValueKind == JsonValueKind.Null
                        ? null
                        : ReadPricedModels(property.Value, propertyPath);
                    break;
            }
        }

        if (isBase)
        {
            Require(hostSuffixes is not null, Join(path, "hostSuffixes"), "is required");
            Require(models is not null, Join(path, "models"), "is required");
        }

        return new PricedRateSection { HostSuffixes = hostSuffixes, Models = models };
    }

    private static FoundryRateSection ReadFoundrySection(JsonElement value, string path, bool isBase)
    {
        RequireObject(value, path);
        IReadOnlyList<string>? hostSuffixes = null;
        IReadOnlyList<string>? providerNames = null;
        IReadOnlyList<PricedModelDocument>? models = null;
        HashSet<string> seen = new(StringComparer.Ordinal);
        string[] schema = ["hostSuffixes", "providerNames", "models"];
        foreach (JsonProperty property in value.EnumerateObject())
        {
            (string name, string propertyPath) = ReadProperty(property, path, schema, seen);
            switch (name)
            {
                case "hostSuffixes":
                    hostSuffixes = property.Value.ValueKind == JsonValueKind.Null
                        ? null
                        : ReadStringArray(property.Value, propertyPath);
                    break;
                case "providerNames":
                    providerNames = property.Value.ValueKind == JsonValueKind.Null
                        ? null
                        : ReadStringArray(property.Value, propertyPath);
                    break;
                case "models":
                    models = property.Value.ValueKind == JsonValueKind.Null
                        ? null
                        : ReadPricedModels(property.Value, propertyPath);
                    break;
            }
        }

        if (isBase)
        {
            Require(hostSuffixes is not null, Join(path, "hostSuffixes"), "is required");
            Require(providerNames is not null, Join(path, "providerNames"), "is required");
            Require(models is not null, Join(path, "models"), "is required");
        }

        return new FoundryRateSection
        {
            HostSuffixes = hostSuffixes,
            ProviderNames = providerNames,
            Models = models,
        };
    }

    private static LocalRateSection ReadLocalSection(JsonElement value, string path, bool isBase)
    {
        RequireObject(value, path);
        string? baselineModel = null;
        IReadOnlyList<LocalModelDocument>? models = null;
        IReadOnlyList<string>? additionalHosts = null;
        IReadOnlyList<string>? providerNames = null;
        HashSet<string> seen = new(StringComparer.Ordinal);
        string[] schema = ["baselineModel", "models", "additionalHosts", "providerNames"];
        foreach (JsonProperty property in value.EnumerateObject())
        {
            (string name, string propertyPath) = ReadProperty(property, path, schema, seen);
            switch (name)
            {
                case "baselineModel":
                    baselineModel = ReadOptionalString(property.Value, propertyPath);
                    break;
                case "models":
                    models = property.Value.ValueKind == JsonValueKind.Null
                        ? null
                        : ReadLocalModels(property.Value, propertyPath);
                    break;
                case "additionalHosts":
                    additionalHosts = property.Value.ValueKind == JsonValueKind.Null
                        ? null
                        : ReadStringArray(property.Value, propertyPath);
                    break;
                case "providerNames":
                    providerNames = property.Value.ValueKind == JsonValueKind.Null
                        ? null
                        : ReadStringArray(property.Value, propertyPath);
                    break;
            }
        }

        if (isBase)
        {
            Require(baselineModel is not null, Join(path, "baselineModel"), "is required");
            Require(models is not null, Join(path, "models"), "is required");
            Require(additionalHosts is not null, Join(path, "additionalHosts"), "is required");
            Require(providerNames is not null, Join(path, "providerNames"), "is required");
        }

        return new LocalRateSection
        {
            BaselineModel = baselineModel,
            Models = models,
            AdditionalHosts = additionalHosts,
            ProviderNames = providerNames,
        };
    }

    private static ApiEquivalentSection ReadApiEquivalentSection(JsonElement value, string path, bool isBase)
    {
        RequireObject(value, path);
        string? description = null;
        string? localBaselineModel = null;
        IReadOnlyList<PricedModelDocument>? models = null;
        HashSet<string> seen = new(StringComparer.Ordinal);
        string[] schema = ["description", "localBaselineModel", "models"];
        foreach (JsonProperty property in value.EnumerateObject())
        {
            (string name, string propertyPath) = ReadProperty(property, path, schema, seen);
            switch (name)
            {
                case "description":
                    description = ReadOptionalString(property.Value, propertyPath);
                    break;
                case "localBaselineModel":
                    localBaselineModel = ReadOptionalString(property.Value, propertyPath);
                    break;
                case "models":
                    models = property.Value.ValueKind == JsonValueKind.Null
                        ? null
                        : ReadPricedModels(property.Value, propertyPath, retail: true);
                    break;
            }
        }

        if (isBase)
        {
            Require(models is not null, Join(path, "models"), "is required");
        }

        return new ApiEquivalentSection
        {
            Description = description,
            LocalBaselineModel = localBaselineModel,
            Models = models,
        };
    }

    // retail: an apiEquivalent model, which may also have vendor and sourceUrl.
    private static IReadOnlyList<PricedModelDocument> ReadPricedModels(JsonElement value, string path, bool retail = false)
    {
        RequireArray(value, path);
        List<PricedModelDocument> models = [];
        int index = 0;
        foreach (JsonElement item in value.EnumerateArray())
        {
            string itemPath = $"{path}[{index++}]";
            models.Add(ReadPricedModel(item, itemPath, retail));
        }

        return models;
    }

    private static PricedModelDocument ReadPricedModel(JsonElement value, string path, bool retail)
    {
        RequireObject(value, path);
        string? id = null;
        string? vendor = null;
        string? sourceUrl = null;
        IReadOnlyList<string> aliases = [];
        IReadOnlyList<RateWindowDocument>? windows = null;
        HashSet<string> seen = new(StringComparer.Ordinal);
        string[] schema = retail
            ? ["id", "vendor", "sourceUrl", "aliases", "windows"]
            : ["id", "aliases", "windows"];
        foreach (JsonProperty property in value.EnumerateObject())
        {
            (string name, string propertyPath) = ReadProperty(property, path, schema, seen);
            switch (name)
            {
                case "id":
                    id = ReadString(property.Value, propertyPath);
                    break;
                case "vendor":
                    vendor = ReadOptionalString(property.Value, propertyPath);
                    break;
                case "sourceUrl":
                    sourceUrl = ReadOptionalString(property.Value, propertyPath);
                    break;
                case "aliases":
                    aliases = ReadStringArray(property.Value, propertyPath);
                    break;
                case "windows":
                    windows = ReadWindows(property.Value, propertyPath);
                    break;
            }
        }

        Require(id is not null, Join(path, "id"), "is required");
        Require(windows is not null, Join(path, "windows"), "is required");
        return new PricedModelDocument
        {
            Id = id!,
            Vendor = vendor,
            SourceUrl = sourceUrl,
            Aliases = aliases,
            Windows = windows!,
        };
    }

    private static IReadOnlyList<RateWindowDocument> ReadWindows(JsonElement value, string path)
    {
        RequireArray(value, path);
        List<RateWindowDocument> windows = [];
        int index = 0;
        foreach (JsonElement item in value.EnumerateArray())
        {
            string itemPath = $"{path}[{index++}]";
            windows.Add(ReadWindow(item, itemPath));
        }

        return windows;
    }

    private static RateWindowDocument ReadWindow(JsonElement value, string path)
    {
        RequireObject(value, path);
        DateTimeOffset? effectiveFrom = null;
        DateTimeOffset? effectiveUntil = null;
        IReadOnlyList<RateTierDocument>? tiers = null;
        HashSet<string> seen = new(StringComparer.Ordinal);
        string[] schema = ["effectiveFrom", "effectiveUntil", "tiers"];
        foreach (JsonProperty property in value.EnumerateObject())
        {
            (string name, string propertyPath) = ReadProperty(property, path, schema, seen);
            switch (name)
            {
                case "effectiveFrom":
                    effectiveFrom = ReadOptionalDateTime(property.Value, propertyPath);
                    break;
                case "effectiveUntil":
                    effectiveUntil = ReadOptionalDateTime(property.Value, propertyPath);
                    break;
                case "tiers":
                    tiers = ReadTiers(property.Value, propertyPath);
                    break;
            }
        }

        Require(tiers is not null, Join(path, "tiers"), "is required");
        return new RateWindowDocument
        {
            EffectiveFrom = effectiveFrom,
            EffectiveUntil = effectiveUntil,
            Tiers = tiers!,
        };
    }

    private static IReadOnlyList<RateTierDocument> ReadTiers(JsonElement value, string path)
    {
        RequireArray(value, path);
        List<RateTierDocument> tiers = [];
        int index = 0;
        foreach (JsonElement item in value.EnumerateArray())
        {
            string itemPath = $"{path}[{index++}]";
            tiers.Add(ReadTier(item, itemPath));
        }

        return tiers;
    }

    private static RateTierDocument ReadTier(JsonElement value, string path)
    {
        RequireObject(value, path);
        string? name = null;
        long? maxInputTokens = null;
        decimal? inputPer1M = null;
        decimal? cachedInputPer1M = null;
        decimal? cacheWritePer1M = null;
        decimal? outputPer1M = null;
        HashSet<string> seen = new(StringComparer.Ordinal);
        string[] schema =
        [
            "name",
            "maxInputTokens",
            "inputPer1M",
            "cachedInputPer1M",
            "cacheWritePer1M",
            "outputPer1M",
        ];
        foreach (JsonProperty property in value.EnumerateObject())
        {
            (string propertyName, string propertyPath) = ReadProperty(property, path, schema, seen);
            switch (propertyName)
            {
                case "name":
                    name = ReadString(property.Value, propertyPath);
                    break;
                case "maxInputTokens":
                    maxInputTokens = ReadOptionalInt64(property.Value, propertyPath);
                    break;
                case "inputPer1M":
                    inputPer1M = ReadNumber(property.Value, propertyPath);
                    break;
                case "cachedInputPer1M":
                    cachedInputPer1M = ReadOptionalNumber(property.Value, propertyPath);
                    break;
                case "cacheWritePer1M":
                    cacheWritePer1M = ReadOptionalNumber(property.Value, propertyPath);
                    break;
                case "outputPer1M":
                    outputPer1M = ReadNumber(property.Value, propertyPath);
                    break;
            }
        }

        Require(name is not null, Join(path, "name"), "is required");
        Require(inputPer1M is not null, Join(path, "inputPer1M"), "is required");
        Require(outputPer1M is not null, Join(path, "outputPer1M"), "is required");
        return new RateTierDocument
        {
            Name = name!,
            MaxInputTokens = maxInputTokens,
            InputPer1M = inputPer1M!.Value,
            CachedInputPer1M = cachedInputPer1M,
            CacheWritePer1M = cacheWritePer1M,
            OutputPer1M = outputPer1M!.Value,
        };
    }

    private static IReadOnlyList<LocalModelDocument> ReadLocalModels(JsonElement value, string path)
    {
        RequireArray(value, path);
        List<LocalModelDocument> models = [];
        int index = 0;
        foreach (JsonElement item in value.EnumerateArray())
        {
            string itemPath = $"{path}[{index++}]";
            models.Add(ReadLocalModel(item, itemPath));
        }

        return models;
    }

    private static LocalModelDocument ReadLocalModel(JsonElement value, string path)
    {
        RequireObject(value, path);
        string? id = null;
        IReadOnlyList<string> aliases = [];
        HashSet<string> seen = new(StringComparer.Ordinal);
        string[] schema = ["id", "aliases"];
        foreach (JsonProperty property in value.EnumerateObject())
        {
            (string name, string propertyPath) = ReadProperty(property, path, schema, seen);
            switch (name)
            {
                case "id":
                    id = ReadString(property.Value, propertyPath);
                    break;
                case "aliases":
                    aliases = ReadStringArray(property.Value, propertyPath);
                    break;
            }
        }

        Require(id is not null, Join(path, "id"), "is required");
        return new LocalModelDocument { Id = id!, Aliases = aliases };
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement value, string path)
    {
        RequireArray(value, path);
        List<string> values = [];
        int index = 0;
        foreach (JsonElement item in value.EnumerateArray())
        {
            values.Add(ReadString(item, $"{path}[{index++}]"));
        }

        return values;
    }

    private static (string Name, string Path) ReadProperty(
        JsonProperty property,
        string parentPath,
        IReadOnlyList<string> schema,
        HashSet<string> seen)
    {
        string name;
        try
        {
            name = property.Name;
        }
        catch (InvalidOperationException ex)
        {
            throw Error(parentPath, "contains a property name that is not valid text", ex);
        }

        string path = Join(parentPath, name);
        if (!schema.Contains(name, StringComparer.Ordinal))
        {
            string? suggestion = schema.FirstOrDefault(candidate =>
                candidate.Equals(name, StringComparison.OrdinalIgnoreCase));
            string problem = suggestion is null
                ? "is not a known property"
                : $"is not a known property; did you mean {suggestion}?";
            throw Error(path, problem);
        }

        if (!seen.Add(name))
        {
            throw Error(path, "is defined more than once");
        }

        return (name, path);
    }

    // The document's schema version, 1 or 2. Throws InvalidDataException for anything else.
    private static int ValidateSchemaVersion(JsonElement root)
    {
        JsonElement value;
        try
        {
            if (!root.TryGetProperty("schemaVersion", out value) ||
                value.ValueKind == JsonValueKind.Null)
            {
                throw Error("schemaVersion", "is required");
            }
        }
        catch (InvalidOperationException ex)
        {
            throw Error(string.Empty, "contains a property name that is not valid text", ex);
        }

        if (value.ValueKind != JsonValueKind.Number ||
            !value.TryGetInt32(out int schemaVersion))
        {
            throw Error("schemaVersion", "must be an integer");
        }

        if (schemaVersion is not (1 or 2))
        {
            throw Error("schemaVersion", "must be 1 or 2");
        }

        return schemaVersion;
    }

    private static string? ReadOptionalString(JsonElement value, string path) =>
        value.ValueKind == JsonValueKind.Null ? null : ReadString(value, path);

    private static string ReadString(JsonElement value, string path)
    {
        if (value.ValueKind != JsonValueKind.String)
        {
            throw Error(path, "must be a string");
        }

        try
        {
            return value.GetString()!;
        }
        catch (InvalidOperationException ex)
        {
            throw Error(path, "is not valid text", ex);
        }
    }

    private static int ReadInt32(JsonElement value, string path)
    {
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out int result))
        {
            throw Error(path, "must be an integer");
        }

        return result;
    }

    private static long? ReadOptionalInt64(JsonElement value, string path) =>
        value.ValueKind == JsonValueKind.Null ? null : ReadInt64(value, path);

    private static long ReadInt64(JsonElement value, string path)
    {
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out long result))
        {
            throw Error(path, "must be an integer");
        }

        return result;
    }

    private static decimal? ReadOptionalNumber(JsonElement value, string path) =>
        value.ValueKind == JsonValueKind.Null ? null : ReadNumber(value, path);

    private static decimal ReadNumber(JsonElement value, string path)
    {
        if (value.ValueKind != JsonValueKind.Number)
        {
            throw Error(path, "must be a number");
        }

        return value.TryGetDecimal(out decimal result)
            ? result
            : throw Error(path, "is too large");
    }

    private static DateTimeOffset? ReadOptionalDateTime(JsonElement value, string path)
    {
        if (value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            try
            {
                if (value.TryGetDateTimeOffset(out DateTimeOffset result))
                {
                    return result;
                }
            }
            catch (InvalidOperationException)
            {
            }
        }

        throw Error(path, "must be a date and time, such as 2027-05-17T00:00:00Z");
    }

    private static void RequireObject(JsonElement value, string path)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw Error(path, "must be an object");
        }
    }

    private static void RequireArray(JsonElement value, string path)
    {
        if (value.ValueKind != JsonValueKind.Array)
        {
            throw Error(path, "must be an array");
        }
    }

    private static void Require(bool condition, string path, string problem)
    {
        if (!condition)
        {
            throw Error(path, problem);
        }
    }

    private static string Join(string path, string name) =>
        path.Length == 0 ? name : $"{path}.{name}";

    private static InvalidDataException Error(string path, string problem, Exception? innerException = null)
    {
        string message = path.Length == 0 ? problem : $"{path}: {problem}";
        return new InvalidDataException(message, innerException);
    }
}
