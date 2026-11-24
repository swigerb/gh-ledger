using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using CopilotLedger.Core.Audit;
using CopilotLedger.Core.Telemetry;

namespace CopilotLedger.Core.Pricing;

public sealed class JsonRateCardCalculator : ICreditCalculator, IAuditPricing
{
    public const string EmbeddedResourceName = "CopilotLedger.Core.Pricing.rates.json";

    private const decimal MaxRatePer1M = 1_000_000m;

    private readonly IReadOnlyList<ModelRateCard> _githubModels;
    private readonly IReadOnlyList<ModelRateCard> _foundryModels;
    private readonly IReadOnlyList<LocalModelRate> _localModels;
    private readonly IReadOnlyList<string> _githubHosts;
    private readonly IReadOnlyList<string> _foundryHosts;
    private readonly IReadOnlyList<string> _foundryProviders;
    private readonly IReadOnlyList<string> _localHosts;
    private readonly IReadOnlyList<string> _localProviders;
    private readonly ModelRateCard _baselineCard;
    private readonly IReadOnlyList<ModelRateCard> _retailModels;
    private readonly ModelRateCard? _retailLocalBaseline;

    private JsonRateCardCalculator(
        string ratesVersion,
        IReadOnlyList<ModelRateCard> githubModels,
        IReadOnlyList<ModelRateCard> foundryModels,
        IReadOnlyList<LocalModelRate> localModels,
        IReadOnlyList<string> githubHosts,
        IReadOnlyList<string> foundryHosts,
        IReadOnlyList<string> foundryProviders,
        IReadOnlyList<string> localHosts,
        IReadOnlyList<string> localProviders,
        ModelRateCard baselineCard,
        int schemaVersion,
        IReadOnlyList<ModelRateCard> retailModels,
        ModelRateCard? retailLocalBaseline)
    {
        RatesVersion = ratesVersion;
        _githubModels = githubModels;
        _foundryModels = foundryModels;
        _localModels = localModels;
        _githubHosts = githubHosts;
        _foundryHosts = foundryHosts;
        _foundryProviders = foundryProviders;
        _localHosts = localHosts;
        _localProviders = localProviders;
        _baselineCard = baselineCard;
        _retailModels = retailModels;
        _retailLocalBaseline = retailLocalBaseline;
        SchemaVersion = schemaVersion;
        ApiRateCardSha256 = ApiRateCardHash(retailModels, retailLocalBaseline);
    }

    public string RatesVersion { get; }

    // 1, or 2 when the card can have an apiEquivalent section. A merged card has the higher of the two versions.
    public int SchemaVersion { get; }

    // The SHA-256 of the canonical API-equivalent card, in lowercase hex; see ApiRateCardHash.
    public string ApiRateCardSha256 { get; }

    public static JsonRateCardCalculator LoadEmbedded() =>
        Create(ReadEmbedded(), null, null);

    public static JsonRateCardCalculator LoadEmbeddedWithOverlay(string overlayPath)
    {
        ArgumentNullException.ThrowIfNull(overlayPath);

        if (!File.Exists(overlayPath))
        {
            throw new FileNotFoundException("The rates overlay file was not found.", overlayPath);
        }

        try
        {
            return Create(ReadEmbedded(), File.ReadAllText(overlayPath), overlayPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new InvalidDataException($"{overlayPath}: could not read rates overlay.", ex);
        }
    }

    // baseJson is a complete rates document, such as a downloaded rates.json; its errors have no prefix. When overlayPath
    // isn't null, that overlay is merged as LoadEmbeddedWithOverlay merges it, and its errors start with overlayPath.
    public static JsonRateCardCalculator LoadWithOverlay(string baseJson, string? overlayPath)
    {
        ArgumentNullException.ThrowIfNull(baseJson);
        if (overlayPath is null)
        {
            return Create(baseJson, null, null);
        }

        string overlayJson;
        try
        {
            overlayJson = File.ReadAllText(overlayPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new InvalidDataException($"{overlayPath}: could not read rates overlay.", ex);
        }

        return Create(baseJson, overlayJson, overlayPath);
    }

    public static JsonRateCardCalculator Parse(string json, string? overlayJson = null)
    {
        ArgumentNullException.ThrowIfNull(json);
        return Create(json, overlayJson, overlayJson is null ? null : "overlay");
    }

    public CreditCalculationResult Calculate(NormalizedGenAiSpan span)
    {
        ArgumentNullException.ThrowIfNull(span);

        // The reported model is the response model, or the request model when there's no response model. A priced
        // channel that has no model for it tries the request model too (see FindModel).
        string? model = NormalizeModel(
            !string.IsNullOrWhiteSpace(span.ResponseModel)
                ? span.ResponseModel
                : span.RequestModel);
        string? requestModel = NormalizeModel(span.RequestModel);

        if (!GenAiOperationNames.IsInference(span.OperationName))
        {
            return Result(span, BillingChannel.Unpriced, PricingStatus.NotBillableOperation, model);
        }

        if (span.InputTokens < 0 ||
            span.OutputTokens < 0 ||
            span.CacheReadInputTokens < 0 ||
            span.CacheWriteInputTokens < 0 ||
            span.CacheReadInputTokens > span.InputTokens ||
            span.CacheWriteInputTokens > span.InputTokens - span.CacheReadInputTokens)
        {
            return Result(span, BillingChannel.Unpriced, PricingStatus.InvalidUsage, model);
        }

        long uncached = span.InputTokens - span.CacheReadInputTokens - span.CacheWriteInputTokens;
        string host = NormalizeHost(span.ServerAddress);

        // An imported request that its provider billed (a Copilot BYOK or Codex request) is local or Foundry BYOK only when
        // the card lists its provider name; without an endpoint, it would otherwise be priced as GitHub's.
        if (span.BillingSource == BillingSource.Provider &&
            !MatchesValue(span.ProviderName, _localProviders) &&
            !MatchesValue(span.ProviderName, _foundryProviders))
        {
            return Result(span, BillingChannel.Unpriced, PricingStatus.OtherProvider, model, uncachedInputTokens: uncached);
        }

        LocalModelRate? localModel = model is null ? null : _localModels.FirstOrDefault(item => item.Matches(model));
        bool localByKnownModel = span.BillingSource == BillingSource.Endpoint &&
                                 string.IsNullOrWhiteSpace(host) &&
                                 localModel is not null;
        if (IsLoopback(host) ||
            MatchesValue(host, _localHosts) ||
            MatchesValue(span.ProviderName, _localProviders) ||
            localByKnownModel)
        {
            string? localModelId = localModel?.ModelId ?? model;
            PricedSelection? selection = Select(_baselineCard, span.StartTime, span.InputTokens);
            if (selection is null)
            {
                return Result(
                    span,
                    BillingChannel.Local,
                    PricingStatus.NoRateForDate,
                    localModelId,
                    uncachedInputTokens: uncached);
            }

            TokenCostBreakdown breakdown = Price(span, selection.Tier, uncached);
            return Result(
                span,
                BillingChannel.Local,
                PricingStatus.LocalOffload,
                localModelId,
                _baselineCard.ModelId,
                selection.Tier.Name,
                uncached,
                breakdown,
                estimatedCloudSavedUsd: breakdown.TotalUsd);
        }

        if (MatchesHost(host, _foundryHosts) || MatchesValue(span.ProviderName, _foundryProviders))
        {
            if (model is null)
            {
                return Result(
                    span,
                    BillingChannel.FoundryByok,
                    PricingStatus.UnknownModel,
                    null,
                    uncachedInputTokens: uncached);
            }

            ModelRateCard? card = FindModel(_foundryModels, model, requestModel);
            if (card is null)
            {
                return Result(
                    span,
                    BillingChannel.FoundryByok,
                    PricingStatus.ByokUnpriced,
                    model,
                    uncachedInputTokens: uncached);
            }

            PricedSelection? selection = Select(card, span.StartTime, span.InputTokens);
            if (selection is null)
            {
                return Result(
                    span,
                    BillingChannel.FoundryByok,
                    PricingStatus.NoRateForDate,
                    card.ModelId,
                    uncachedInputTokens: uncached);
            }

            TokenCostBreakdown breakdown = Price(span, selection.Tier, uncached);
            return Result(
                span,
                BillingChannel.FoundryByok,
                PricingStatus.Priced,
                card.ModelId,
                card.ModelId,
                selection.Tier.Name,
                uncached,
                breakdown,
                usdCost: breakdown.TotalUsd);
        }

        if (string.IsNullOrWhiteSpace(host) || MatchesHost(host, _githubHosts))
        {
            if (model is null)
            {
                return Result(
                    span,
                    BillingChannel.GitHubAiCredits,
                    PricingStatus.UnknownModel,
                    null,
                    uncachedInputTokens: uncached);
            }

            ModelRateCard? card = FindModel(_githubModels, model, requestModel);
            if (card is null)
            {
                return Result(
                    span,
                    BillingChannel.GitHubAiCredits,
                    PricingStatus.UnknownModel,
                    model,
                    uncachedInputTokens: uncached);
            }

            PricedSelection? selection = Select(card, span.StartTime, span.InputTokens);
            if (selection is null)
            {
                return Result(
                    span,
                    BillingChannel.GitHubAiCredits,
                    PricingStatus.NoRateForDate,
                    card.ModelId,
                    uncachedInputTokens: uncached);
            }

            TokenCostBreakdown breakdown = Price(span, selection.Tier, uncached);
            return Result(
                span,
                BillingChannel.GitHubAiCredits,
                PricingStatus.Priced,
                card.ModelId,
                card.ModelId,
                selection.Tier.Name,
                uncached,
                breakdown,
                breakdown.TotalUsd,
                AiCreditMath.UsdToAiCredits(breakdown.TotalUsd));
        }

        return Result(
            span,
            BillingChannel.Unpriced,
            PricingStatus.UnrecognizedEndpoint,
            model,
            uncachedInputTokens: uncached);
    }

    // Prices one request at this card's API list prices (apiEquivalent), whatever its billing channel priced: a local
    // request as the local retail baseline, and any other request as its ModelId, or as its RequestModel when no section
    // of this card has ModelId. So a response model that the card doesn't know, such as a dated id that a Foundry BYOK
    // endpoint reports, never hides a request model with a list price, and a model that the card knows without a list
    // price, such as mai-code-2, is never priced as another. Pure and thread-safe. Throws only ArgumentNullException; an
    // unknown model or date is Unpriced, and a non-inference operation or inconsistent token counts are NotApplicable.
    public ApiEquivalentCost CalculateApiEquivalent(ApiEquivalentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!GenAiOperationNames.IsInference(request.OperationName) ||
            request.InputTokens < 0 ||
            request.OutputTokens < 0 ||
            request.CacheReadInputTokens < 0 ||
            request.CacheWriteInputTokens < 0 ||
            request.CacheReadInputTokens > request.InputTokens ||
            request.CacheWriteInputTokens > request.InputTokens - request.CacheReadInputTokens)
        {
            return Retail(ApiEquivalentStatus.NotApplicable);
        }

        string? model = NormalizeModel(request.ModelId);
        string? requestModel = NormalizeModel(request.RequestModel);
        if (requestModel is not null && (model is null || !IsKnown(model)))
        {
            model = requestModel;
        }

        ModelRateCard? card = request.Channel == BillingChannel.Local
            ? _retailLocalBaseline
            : model is null ? null : FindModel(_retailModels, model);
        PricedSelection? selection = card is null
            ? null
            : Select(card, request.StartTime, request.InputTokens);
        if (selection is null)
        {
            return Retail(ApiEquivalentStatus.Unpriced);
        }

        long uncached = request.InputTokens - request.CacheReadInputTokens - request.CacheWriteInputTokens;
        return Retail(
            ApiEquivalentStatus.Priced,
            card!.ModelId,
            selection.Tier.Name,
            Price(request, selection.Tier, uncached));
    }

    // IAuditPricing: localOffload.baselineModel, a githubAiCredits model.
    public string BaselineModel => _baselineCard.ModelId;

    // IAuditPricing: the canonical id of the githubAiCredits model that model names (an id or an alias), or null.
    public string? FindGitHubModel(string model)
    {
        ArgumentNullException.ThrowIfNull(model);
        string? normalized = NormalizeModel(model);
        return normalized is null ? null : FindModel(_githubModels, normalized)?.ModelId;
    }

    // IAuditPricing: the tier that bills a request of channel as Calculate selects it, or null.
    public RateTier? FindBilledTier(
        BillingChannel channel,
        string? model,
        string? requestModel,
        DateTimeOffset instant,
        long inputTokens)
    {
        IReadOnlyList<ModelRateCard>? models = channel switch
        {
            BillingChannel.GitHubAiCredits => _githubModels,
            BillingChannel.FoundryByok => _foundryModels,
            _ => null,
        };
        if (models is null)
        {
            return null;
        }

        string? normalized = NormalizeModel(model);
        string? normalizedRequest = NormalizeModel(requestModel);
        ModelRateCard? card = normalized is null
            ? normalizedRequest is null ? null : FindModel(models, normalizedRequest)
            : FindModel(models, normalized, normalizedRequest);
        return card is null ? null : Select(card, instant, inputTokens)?.Tier;
    }

    // IAuditPricing: the first tier's outputPer1M of each githubAiCredits model with a window covering instant.
    public IReadOnlyList<decimal> GitHubOutputRates(DateTimeOffset instant) =>
    [
        .. _githubModels
            .Select(card => Select(card, instant, 0))
            .OfType<PricedSelection>()
            .Select(selection => selection.Tier.OutputPer1M),
    ];

    // Whether any section of this card has model as an id or alias.
    private bool IsKnown(string model) =>
        FindModel(_githubModels, model) is not null ||
        FindModel(_foundryModels, model) is not null ||
        FindModel(_retailModels, model) is not null ||
        _localModels.Any(item => item.Matches(model));

    private static JsonRateCardCalculator Create(string baseJson, string? overlayJson, string? overlayName)
    {
        RateCardDocument baseDocument = RateCardReader.Read(baseJson, isBase: true);
        JsonRateCardCalculator builtIn = Validate(baseDocument);
        if (overlayJson is null)
        {
            return builtIn;
        }

        try
        {
            RateCardDocument overlay = RateCardReader.Read(overlayJson, isBase: false);
            ValidateOverlay(overlay);
            RateCardDocument merged = Merge(baseDocument, overlay);
            ValidateMergedRates(baseDocument, overlay, merged);
            return Validate(merged);
        }
        catch (InvalidDataException ex)
        {
            throw new InvalidDataException($"{overlayName}: {ex.Message}", ex);
        }
    }

    private static void ValidateOverlay(RateCardDocument overlay)
    {
        if (overlay.GithubAiCredits?.Models is not null)
        {
            _ = ValidateModels(
                overlay.GithubAiCredits.Models,
                BillingChannel.GitHubAiCredits,
                "githubAiCredits.models");
        }

        if (overlay.FoundryByok?.Models is not null)
        {
            _ = ValidateModels(
                overlay.FoundryByok.Models,
                BillingChannel.FoundryByok,
                "foundryByok.models");
        }

        if (overlay.LocalOffload?.Models is not null)
        {
            _ = ValidateLocalModels(overlay.LocalOffload.Models, "localOffload.models");
        }

        if (overlay.LocalOffload?.BaselineModel is not null &&
            NormalizeModel(overlay.LocalOffload.BaselineModel) is null)
        {
            throw Invalid(null, "localOffload.baselineModel", "must not be blank");
        }

        if (overlay.GithubAiCredits?.HostSuffixes is not null)
        {
            _ = ValidateHosts(overlay.GithubAiCredits.HostSuffixes, "githubAiCredits.hostSuffixes");
        }

        if (overlay.FoundryByok?.HostSuffixes is not null)
        {
            _ = ValidateHosts(overlay.FoundryByok.HostSuffixes, "foundryByok.hostSuffixes");
        }

        if (overlay.FoundryByok?.ProviderNames is not null)
        {
            _ = ValidateNames(overlay.FoundryByok.ProviderNames, "foundryByok.providerNames");
        }

        if (overlay.LocalOffload?.AdditionalHosts is not null)
        {
            _ = ValidateNames(overlay.LocalOffload.AdditionalHosts, "localOffload.additionalHosts");
        }

        if (overlay.LocalOffload?.ProviderNames is not null)
        {
            _ = ValidateNames(overlay.LocalOffload.ProviderNames, "localOffload.providerNames");
        }

        if (overlay.ApiEquivalent?.Models is not null)
        {
            _ = ValidateModels(
                overlay.ApiEquivalent.Models,
                BillingChannel.Unpriced,
                "apiEquivalent.models");
        }

        if (overlay.ApiEquivalent?.LocalBaselineModel is not null &&
            NormalizeModel(overlay.ApiEquivalent.LocalBaselineModel) is null)
        {
            throw Invalid(null, "apiEquivalent.localBaselineModel", "must not be blank");
        }
    }

    private static RateCardDocument Merge(RateCardDocument baseDocument, RateCardDocument overlay)
    {
        string ratesVersion = $"{baseDocument.RatesVersion}+{(string.IsNullOrWhiteSpace(overlay.RatesVersion) ? "user" : overlay.RatesVersion)}";

        return new RateCardDocument
        {
            SchemaVersion = Math.Max(baseDocument.SchemaVersion ?? 1, overlay.SchemaVersion ?? 1),
            RatesVersion = ratesVersion,
            Source = baseDocument.Source,
            Notes = baseDocument.Notes,
            GithubAiCredits = new PricedRateSection
            {
                HostSuffixes = MergeStrings(baseDocument.GithubAiCredits!.HostSuffixes!, overlay.GithubAiCredits?.HostSuffixes),
                Models = MergeModels(baseDocument.GithubAiCredits.Models!, overlay.GithubAiCredits?.Models),
            },
            FoundryByok = new FoundryRateSection
            {
                HostSuffixes = MergeStrings(baseDocument.FoundryByok!.HostSuffixes!, overlay.FoundryByok?.HostSuffixes),
                ProviderNames = MergeStrings(baseDocument.FoundryByok.ProviderNames!, overlay.FoundryByok?.ProviderNames),
                Models = MergeModels(baseDocument.FoundryByok.Models!, overlay.FoundryByok?.Models),
            },
            LocalOffload = new LocalRateSection
            {
                BaselineModel = overlay.LocalOffload?.BaselineModel ?? baseDocument.LocalOffload!.BaselineModel,
                Models = MergeModels(baseDocument.LocalOffload!.Models!, overlay.LocalOffload?.Models),
                AdditionalHosts = MergeStrings(baseDocument.LocalOffload!.AdditionalHosts!, overlay.LocalOffload?.AdditionalHosts),
                ProviderNames = MergeStrings(baseDocument.LocalOffload!.ProviderNames!, overlay.LocalOffload?.ProviderNames),
            },
            ApiEquivalent = baseDocument.ApiEquivalent is null && overlay.ApiEquivalent is null
                ? null
                : new ApiEquivalentSection
                {
                    Description = baseDocument.ApiEquivalent?.Description,
                    LocalBaselineModel = overlay.ApiEquivalent?.LocalBaselineModel
                        ?? baseDocument.ApiEquivalent?.LocalBaselineModel,
                    Models = MergeModels(baseDocument.ApiEquivalent?.Models ?? [], overlay.ApiEquivalent?.Models),
                },
        };
    }

    private static void ValidateMergedRates(
        RateCardDocument baseDocument,
        RateCardDocument overlay,
        RateCardDocument merged)
    {
        if (overlay.GithubAiCredits?.Models is not null)
        {
            ValidateMergedModelIdentities(
                baseDocument.GithubAiCredits!.Models!,
                overlay.GithubAiCredits.Models,
                "githubAiCredits.models");
        }

        if (overlay.FoundryByok?.Models is not null)
        {
            ValidateMergedModelIdentities(
                baseDocument.FoundryByok!.Models!,
                overlay.FoundryByok.Models,
                "foundryByok.models");
        }

        if (overlay.LocalOffload?.Models is not null)
        {
            ValidateMergedModelIdentities(
                baseDocument.LocalOffload!.Models!,
                overlay.LocalOffload.Models,
                "localOffload.models");
        }

        if (overlay.ApiEquivalent?.Models is not null)
        {
            ValidateMergedModelIdentities(
                baseDocument.ApiEquivalent?.Models ?? [],
                overlay.ApiEquivalent.Models,
                "apiEquivalent.models");
        }

        string baseline = NormalizeModel(merged.LocalOffload!.BaselineModel)!;
        bool resolves = merged.GithubAiCredits!.Models!.Any(model =>
            NormalizeModel(model.Id) == baseline ||
            model.Aliases.Any(alias => NormalizeModel(alias) == baseline));
        if (!resolves)
        {
            throw Invalid(
                null,
                "localOffload.baselineModel",
                $"does not resolve to a githubAiCredits model in the merged rates: {baseline}");
        }

        string? retailBaseline = NormalizeModel(merged.ApiEquivalent?.LocalBaselineModel);
        if (retailBaseline is not null &&
            !merged.ApiEquivalent!.Models!.Any(model =>
                NormalizeModel(model.Id) == retailBaseline ||
                model.Aliases.Any(alias => NormalizeModel(alias) == retailBaseline)))
        {
            throw Invalid(
                null,
                "apiEquivalent.localBaselineModel",
                $"does not resolve to an apiEquivalent model in the merged rates: {retailBaseline}");
        }
    }

    private static void ValidateMergedModelIdentities(
        IReadOnlyList<PricedModelDocument> baseModels,
        IReadOnlyList<PricedModelDocument> overlayModels,
        string path)
    {
        HashSet<string> replaced = overlayModels
            .Select(model => NormalizeModel(model.Id)!)
            .ToHashSet(StringComparer.Ordinal);
        Dictionary<string, string> owners = new(StringComparer.Ordinal);
        foreach (PricedModelDocument model in baseModels)
        {
            string id = NormalizeModel(model.Id)!;
            if (replaced.Contains(id))
            {
                continue;
            }

            owners[id] = id;
            foreach (string alias in model.Aliases)
            {
                owners[NormalizeModel(alias)!] = id;
            }
        }

        for (int index = 0; index < overlayModels.Count; index++)
        {
            PricedModelDocument model = overlayModels[index];
            RequireNoMergedCollision(owners, NormalizeModel(model.Id)!, $"{path}[{index}].id");
            for (int aliasIndex = 0; aliasIndex < model.Aliases.Count; aliasIndex++)
            {
                RequireNoMergedCollision(
                    owners,
                    NormalizeModel(model.Aliases[aliasIndex])!,
                    $"{path}[{index}].aliases[{aliasIndex}]");
            }
        }
    }

    private static void ValidateMergedModelIdentities(
        IReadOnlyList<LocalModelDocument> baseModels,
        IReadOnlyList<LocalModelDocument> overlayModels,
        string path)
    {
        HashSet<string> replaced = overlayModels
            .Select(model => NormalizeModel(model.Id)!)
            .ToHashSet(StringComparer.Ordinal);
        Dictionary<string, string> owners = new(StringComparer.Ordinal);
        foreach (LocalModelDocument model in baseModels)
        {
            string id = NormalizeModel(model.Id)!;
            if (replaced.Contains(id))
            {
                continue;
            }

            owners[id] = id;
            foreach (string alias in model.Aliases)
            {
                owners[NormalizeModel(alias)!] = id;
            }
        }

        for (int index = 0; index < overlayModels.Count; index++)
        {
            LocalModelDocument model = overlayModels[index];
            RequireNoMergedCollision(owners, NormalizeModel(model.Id)!, $"{path}[{index}].id");
            for (int aliasIndex = 0; aliasIndex < model.Aliases.Count; aliasIndex++)
            {
                RequireNoMergedCollision(
                    owners,
                    NormalizeModel(model.Aliases[aliasIndex])!,
                    $"{path}[{index}].aliases[{aliasIndex}]");
            }
        }
    }

    private static void RequireNoMergedCollision(
        IReadOnlyDictionary<string, string> owners,
        string identity,
        string path)
    {
        if (owners.TryGetValue(identity, out string? owner))
        {
            throw Invalid(
                null,
                path,
                $"duplicate model identity in the merged rates: {identity} (built-in model {owner})");
        }
    }

    private static JsonRateCardCalculator Validate(RateCardDocument document)
    {
        if (string.IsNullOrWhiteSpace(document.RatesVersion))
        {
            throw Invalid(null, "ratesVersion", "must not be blank");
        }

        IReadOnlyList<ModelRateCard> githubModels = ValidateModels(
            document.GithubAiCredits!.Models!,
            BillingChannel.GitHubAiCredits,
            "githubAiCredits.models");
        IReadOnlyList<ModelRateCard> foundryModels = ValidateModels(
            document.FoundryByok!.Models!,
            BillingChannel.FoundryByok,
            "foundryByok.models");
        IReadOnlyList<LocalModelRate> localModels = ValidateLocalModels(
            document.LocalOffload!.Models!,
            "localOffload.models");

        string normalizedBaseline = NormalizeModel(document.LocalOffload.BaselineModel)
            ?? throw Invalid(null, "localOffload.baselineModel", "must not be blank");
        ModelRateCard? baseline = FindModel(githubModels, normalizedBaseline);
        if (baseline is null)
        {
            throw Invalid(null, "localOffload.baselineModel", $"does not resolve to a githubAiCredits model: {normalizedBaseline}");
        }

        IReadOnlyList<ModelRateCard> retailModels = document.ApiEquivalent?.Models is null
            ? []
            : ValidateModels(document.ApiEquivalent.Models, BillingChannel.Unpriced, "apiEquivalent.models");

        // Local requests are priced at retail as apiEquivalent.localBaselineModel when it's set, or else as the
        // localOffload baseline, which may have no apiEquivalent price.
        ModelRateCard? retailLocalBaseline = FindModel(retailModels, normalizedBaseline);
        if (document.ApiEquivalent?.LocalBaselineModel is not null)
        {
            string retailBaseline = NormalizeModel(document.ApiEquivalent.LocalBaselineModel)
                ?? throw Invalid(null, "apiEquivalent.localBaselineModel", "must not be blank");
            retailLocalBaseline = FindModel(retailModels, retailBaseline)
                ?? throw Invalid(
                    null,
                    "apiEquivalent.localBaselineModel",
                    $"does not resolve to an apiEquivalent model: {retailBaseline}");
        }

        return new JsonRateCardCalculator(
            document.RatesVersion,
            githubModels,
            foundryModels,
            localModels,
            ValidateHosts(document.GithubAiCredits.HostSuffixes!, "githubAiCredits.hostSuffixes"),
            ValidateHosts(document.FoundryByok.HostSuffixes!, "foundryByok.hostSuffixes"),
            ValidateNames(document.FoundryByok.ProviderNames!, "foundryByok.providerNames"),
            ValidateNames(document.LocalOffload.AdditionalHosts!, "localOffload.additionalHosts"),
            ValidateNames(document.LocalOffload.ProviderNames!, "localOffload.providerNames"),
            baseline,
            document.SchemaVersion ?? 1,
            retailModels,
            retailLocalBaseline);
    }

    private static IReadOnlyList<ModelRateCard> ValidateModels(
        IReadOnlyList<PricedModelDocument> models,
        BillingChannel channel,
        string path)
    {
        HashSet<string> identities = new(StringComparer.Ordinal);
        List<ModelRateCard> result = [];
        for (int modelIndex = 0; modelIndex < models.Count; modelIndex++)
        {
            string modelPath = $"{path}[{modelIndex}]";
            PricedModelDocument model = models[modelIndex]
                ?? throw Invalid(null, modelPath, "must not be null");
            string id = NormalizeModel(model.Id)
                ?? throw Invalid(null, $"{modelPath}.id", "must not be blank");
            AddIdentity(identities, id, $"{modelPath}.id");

            List<string> aliases = [];
            IReadOnlyList<string> modelAliases = model.Aliases
                ?? throw Invalid(null, $"{modelPath}.aliases", "must not be null");
            for (int aliasIndex = 0; aliasIndex < modelAliases.Count; aliasIndex++)
            {
                string aliasPath = $"{modelPath}.aliases[{aliasIndex}]";
                string? rawAlias = modelAliases[aliasIndex];
                Require(rawAlias is not null, null, aliasPath, "must not be null");
                string alias = NormalizeModel(rawAlias)
                    ?? throw Invalid(null, aliasPath, "must not be blank");
                AddIdentity(identities, alias, aliasPath);
                aliases.Add(alias);
            }

            if (model.Windows.Count == 0)
            {
                throw Invalid(null, $"{modelPath}.windows", "must contain at least one window");
            }

            List<RateWindow> windows = [];
            for (int windowIndex = 0; windowIndex < model.Windows.Count; windowIndex++)
            {
                string windowPath = $"{modelPath}.windows[{windowIndex}]";
                RateWindowDocument window = model.Windows[windowIndex]
                    ?? throw Invalid(null, windowPath, "must not be null");
                if (window.EffectiveFrom is not null &&
                    window.EffectiveUntil is not null &&
                    window.EffectiveFrom >= window.EffectiveUntil)
                {
                    throw Invalid(null, $"{windowPath}.effectiveUntil", "must be later than effectiveFrom");
                }

                if (windowIndex > 0)
                {
                    RateWindowDocument previous = model.Windows[windowIndex - 1]
                        ?? throw Invalid(null, $"{modelPath}.windows[{windowIndex - 1}]", "must not be null");
                    if (window.EffectiveFrom is null ||
                        previous.EffectiveUntil is null ||
                        previous.EffectiveUntil > window.EffectiveFrom)
                    {
                        throw Invalid(null, $"{windowPath}.effectiveFrom", "windows must be sorted and must not overlap");
                    }
                }

                if (window.Tiers.Count == 0)
                {
                    throw Invalid(null, $"{windowPath}.tiers", "must contain at least one tier");
                }

                HashSet<string> tierNames = new(StringComparer.Ordinal);
                List<RateTier> tiers = [];
                long? previousMax = null;
                for (int tierIndex = 0; tierIndex < window.Tiers.Count; tierIndex++)
                {
                    string tierPath = $"{windowPath}.tiers[{tierIndex}]";
                    RateTierDocument tier = window.Tiers[tierIndex]
                        ?? throw Invalid(null, tierPath, "must not be null");
                    if (string.IsNullOrWhiteSpace(tier.Name))
                    {
                        throw Invalid(null, $"{tierPath}.name", "must not be blank");
                    }

                    if (!tierNames.Add(tier.Name))
                    {
                        throw Invalid(null, $"{tierPath}.name", "must be unique within the window");
                    }

                    bool last = tierIndex == window.Tiers.Count - 1;
                    if (last && tier.MaxInputTokens is not null)
                    {
                        throw Invalid(null, $"{tierPath}.maxInputTokens", "the last tier must be unbounded");
                    }

                    if (!last)
                    {
                        if (tier.MaxInputTokens is null or <= 0)
                        {
                            throw Invalid(null, $"{tierPath}.maxInputTokens", "must be greater than 0 for every tier except the last");
                        }

                        if (previousMax is not null && tier.MaxInputTokens <= previousMax)
                        {
                            throw Invalid(null, $"{tierPath}.maxInputTokens", "must be strictly ascending");
                        }

                        previousMax = tier.MaxInputTokens;
                    }

                    ValidateRate(tier.InputPer1M, $"{tierPath}.inputPer1M");
                    ValidateOptionalRate(tier.CachedInputPer1M, $"{tierPath}.cachedInputPer1M");
                    ValidateOptionalRate(tier.CacheWritePer1M, $"{tierPath}.cacheWritePer1M");
                    ValidateRate(tier.OutputPer1M, $"{tierPath}.outputPer1M");
                    tiers.Add(new RateTier(
                        tier.Name,
                        tier.MaxInputTokens,
                        tier.InputPer1M,
                        tier.CachedInputPer1M,
                        tier.CacheWritePer1M,
                        tier.OutputPer1M));
                }

                windows.Add(new RateWindow(window.EffectiveFrom, window.EffectiveUntil, tiers));
            }

            result.Add(new ModelRateCard(id, channel, aliases, windows));
        }

        return result;
    }

    private static IReadOnlyList<LocalModelRate> ValidateLocalModels(
        IReadOnlyList<LocalModelDocument> models,
        string path)
    {
        HashSet<string> identities = new(StringComparer.Ordinal);
        List<LocalModelRate> result = [];
        for (int modelIndex = 0; modelIndex < models.Count; modelIndex++)
        {
            string modelPath = $"{path}[{modelIndex}]";
            LocalModelDocument model = models[modelIndex]
                ?? throw Invalid(null, modelPath, "must not be null");
            string id = NormalizeModel(model.Id)
                ?? throw Invalid(null, $"{modelPath}.id", "must not be blank");
            AddIdentity(identities, id, $"{modelPath}.id");

            List<string> aliases = [];
            IReadOnlyList<string> modelAliases = model.Aliases
                ?? throw Invalid(null, $"{modelPath}.aliases", "must not be null");
            for (int aliasIndex = 0; aliasIndex < modelAliases.Count; aliasIndex++)
            {
                string aliasPath = $"{modelPath}.aliases[{aliasIndex}]";
                string? rawAlias = modelAliases[aliasIndex];
                Require(rawAlias is not null, null, aliasPath, "must not be null");
                string alias = NormalizeModel(rawAlias)
                    ?? throw Invalid(null, aliasPath, "must not be blank");
                AddIdentity(identities, alias, aliasPath);
                aliases.Add(alias);
            }

            result.Add(new LocalModelRate(id, aliases));
        }

        return result;
    }

    private static IReadOnlyList<string> ValidateHosts(IReadOnlyList<string> values, string path)
    {
        List<string> result = [];
        for (int index = 0; index < values.Count; index++)
        {
            Require(values[index] is not null, null, $"{path}[{index}]", "must not be null");
            string value = values[index].Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(value) ||
                value.StartsWith('.') ||
                value.Contains('/') ||
                value.Contains(':'))
            {
                throw Invalid(null, $"{path}[{index}]", "must be a host suffix without a leading '.', '/', or ':'");
            }

            result.Add(value);
        }

        return result;
    }

    private static IReadOnlyList<string> ValidateNames(IReadOnlyList<string> values, string path)
    {
        List<string> result = [];
        for (int index = 0; index < values.Count; index++)
        {
            Require(values[index] is not null, null, $"{path}[{index}]", "must not be null");
            string value = values[index].Trim();
            if (value.Length == 0)
            {
                throw Invalid(null, $"{path}[{index}]", "must not be blank");
            }

            result.Add(value);
        }

        return result;
    }

    private static void AddIdentity(HashSet<string> identities, string value, string path)
    {
        if (!identities.Add(value))
        {
            throw Invalid(null, path, $"duplicate model identity: {value}");
        }
    }

    private static void ValidateOptionalRate(decimal? rate, string path)
    {
        if (rate is not null)
        {
            ValidateRate(rate.Value, path);
        }
    }

    private static void ValidateRate(decimal rate, string path)
    {
        if (rate < 0)
        {
            throw Invalid(null, path, "must be greater than or equal to 0");
        }

        // The bound keeps tokens * rate inside decimal for any long token count, so Calculate never throws.
        if (rate > MaxRatePer1M)
        {
            throw Invalid(null, path, "must be less than or equal to 1000000");
        }

        if (rate * 1_000_000m % 1m != 0m)
        {
            throw Invalid(null, path, "must have no more than 6 decimal places");
        }
    }

    private static IReadOnlyList<string> MergeStrings(
        IReadOnlyList<string> baseValues,
        IReadOnlyList<string>? overlayValues)
    {
        List<string> result = [.. baseValues];
        if (overlayValues is not null)
        {
            foreach (string value in overlayValues)
            {
                if (!result.Contains(value, StringComparer.OrdinalIgnoreCase))
                {
                    result.Add(value);
                }
            }
        }

        return result;
    }

    private static IReadOnlyList<PricedModelDocument> MergeModels(
        IReadOnlyList<PricedModelDocument> baseModels,
        IReadOnlyList<PricedModelDocument>? overlayModels)
    {
        List<PricedModelDocument> result = [.. baseModels];
        if (overlayModels is not null)
        {
            foreach (PricedModelDocument model in overlayModels)
            {
                string? normalized = NormalizeModel(model.Id);
                int index = normalized is null
                    ? -1
                    : result.FindIndex(existing =>
                        string.Equals(NormalizeModel(existing.Id), normalized, StringComparison.Ordinal));
                if (index >= 0)
                {
                    result[index] = model;
                }
                else
                {
                    result.Add(model);
                }
            }
        }

        return result;
    }

    private static IReadOnlyList<LocalModelDocument> MergeModels(
        IReadOnlyList<LocalModelDocument> baseModels,
        IReadOnlyList<LocalModelDocument>? overlayModels)
    {
        List<LocalModelDocument> result = [.. baseModels];
        if (overlayModels is not null)
        {
            foreach (LocalModelDocument model in overlayModels)
            {
                string? normalized = NormalizeModel(model.Id);
                int index = normalized is null
                    ? -1
                    : result.FindIndex(existing =>
                        string.Equals(NormalizeModel(existing.Id), normalized, StringComparison.Ordinal));
                if (index >= 0)
                {
                    result[index] = model;
                }
                else
                {
                    result.Add(model);
                }
            }
        }

        return result;
    }

    private static string? NormalizeModel(string? model)
    {
        if (string.IsNullOrWhiteSpace(model))
        {
            return null;
        }

        StringBuilder result = new();
        bool separator = false;
        foreach (char character in model.Trim().ToLowerInvariant())
        {
            if (char.IsWhiteSpace(character))
            {
                if (result.Length > 0)
                {
                    separator = true;
                }

                continue;
            }

            if (separator)
            {
                result.Append('-');
                separator = false;
            }

            result.Append(character);
        }

        return result.Length == 0 ? null : result.ToString();
    }

    private static string NormalizeHost(string? host)
    {
        string normalized = host?.Trim() ?? string.Empty;
        if (normalized.Length >= 2 && normalized[0] == '[' && normalized[^1] == ']')
        {
            normalized = normalized[1..^1];
        }

        return normalized;
    }

    private static bool IsLoopback(string host)
    {
        return host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            IPAddress.TryParse(host, out IPAddress? address) && IPAddress.IsLoopback(address);
    }

    private static bool MatchesHost(string host, IReadOnlyList<string> suffixes)
    {
        return !string.IsNullOrWhiteSpace(host) &&
            suffixes.Any(suffix =>
                host.Equals(suffix, StringComparison.OrdinalIgnoreCase) ||
                host.EndsWith("." + suffix, StringComparison.OrdinalIgnoreCase));
    }

    private static bool MatchesValue(string? value, IReadOnlyList<string> values) =>
        value is not null && values.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase);

    private static ModelRateCard? FindModel(IEnumerable<ModelRateCard> models, string model) =>
        models.FirstOrDefault(card =>
            card.ModelId.Equals(model, StringComparison.Ordinal) ||
            card.Aliases.Contains(model, StringComparer.Ordinal));

    // The model in models for a request: the one its reported model names, or else the one its request model names.
    // So a response model that models doesn't have, such as a vendor's dated id, never hides a request model that it
    // has, and one that it has is never replaced. Null when models has neither.
    private static ModelRateCard? FindModel(IEnumerable<ModelRateCard> models, string model, string? requestModel) =>
        FindModel(models, model) ?? (requestModel is null ? null : FindModel(models, requestModel));

    private static PricedSelection? Select(ModelRateCard card, DateTimeOffset instant, long inputTokens)
    {
        RateWindow? window = card.Windows.FirstOrDefault(item =>
            (item.EffectiveFrom is null || item.EffectiveFrom <= instant) &&
            (item.EffectiveUntil is null || instant < item.EffectiveUntil));
        if (window is null)
        {
            return null;
        }

        RateTier? tier = window.Tiers.FirstOrDefault(item =>
            item.MaxInputTokens is null || inputTokens <= item.MaxInputTokens);
        return tier is null ? null : new PricedSelection(tier);
    }

    private static TokenCostBreakdown Price(NormalizedGenAiSpan span, RateTier tier, long uncached) =>
        new(
            AiCreditMath.CostUsd(uncached, tier.InputPer1M),
            AiCreditMath.CostUsd(span.CacheReadInputTokens, tier.CachedInputPer1M ?? tier.InputPer1M),
            AiCreditMath.CostUsd(span.CacheWriteInputTokens, tier.CacheWritePer1M ?? tier.InputPer1M),
            AiCreditMath.CostUsd(span.OutputTokens, tier.OutputPer1M));

    private static TokenCostBreakdown Price(ApiEquivalentRequest request, RateTier tier, long uncached) =>
        new(
            AiCreditMath.CostUsd(uncached, tier.InputPer1M),
            AiCreditMath.CostUsd(request.CacheReadInputTokens, tier.CachedInputPer1M ?? tier.InputPer1M),
            AiCreditMath.CostUsd(request.CacheWriteInputTokens, tier.CacheWritePer1M ?? tier.InputPer1M),
            AiCreditMath.CostUsd(request.OutputTokens, tier.OutputPer1M));

    // The credit lens's result for span, with the same request priced in the API-equivalent lens.
    private CreditCalculationResult Result(
        NormalizedGenAiSpan span,
        BillingChannel channel,
        PricingStatus status,
        string? modelId,
        string? rateCardModelId = null,
        string? tierName = null,
        long uncachedInputTokens = 0,
        TokenCostBreakdown? breakdown = null,
        decimal usdCost = 0m,
        decimal aiCredits = 0m,
        decimal estimatedCloudSavedUsd = 0m) =>
        new()
        {
            Channel = channel,
            Status = status,
            RatesVersion = RatesVersion,
            ModelId = modelId,
            RateCardModelId = rateCardModelId,
            TierName = tierName,
            UncachedInputTokens = uncachedInputTokens,
            Breakdown = breakdown,
            UsdCost = usdCost,
            AiCredits = aiCredits,
            EstimatedCloudSavedUsd = estimatedCloudSavedUsd,
            ApiEquivalent = CalculateApiEquivalent(new ApiEquivalentRequest
            {
                OperationName = span.OperationName,
                Channel = channel,
                ModelId = modelId,
                RequestModel = span.RequestModel,
                StartTime = span.StartTime,
                InputTokens = span.InputTokens,
                OutputTokens = span.OutputTokens,
                CacheReadInputTokens = span.CacheReadInputTokens,
                CacheWriteInputTokens = span.CacheWriteInputTokens,
            }),
        };

    private ApiEquivalentCost Retail(
        ApiEquivalentStatus status,
        string? rateCardModelId = null,
        string? tierName = null,
        TokenCostBreakdown? breakdown = null) =>
        new()
        {
            Status = status,
            RatesVersion = RatesVersion,
            RateCardSha256 = ApiRateCardSha256,
            RateCardModelId = rateCardModelId,
            TierName = tierName,
            Breakdown = breakdown,
        };

    // SHA-256 of the canonical API-equivalent card: compact UTF-8 JSON {"localBaselineModel":…,"models":[…]}. The local
    // baseline is the ID of the apiEquivalent model that prices local requests, or null. Models are in ordinal ID order,
    // each with its normalized ID, its normalized aliases in ordinal order, and its windows and tiers in card order. Rates
    // are JSON numbers without trailing zeros, window bounds are UTC "O" timestamps or null, and maxInputTokens is a
    // number or null. vendor, sourceUrl, description, and notes don't change any price, so they aren't hashed.
    private static string ApiRateCardHash(IReadOnlyList<ModelRateCard> models, ModelRateCard? localBaseline)
    {
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(
            stream,
            new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            writer.WriteStartObject();
            if (localBaseline is null)
            {
                writer.WriteNull("localBaselineModel");
            }
            else
            {
                writer.WriteString("localBaselineModel", localBaseline.ModelId);
            }

            writer.WriteStartArray("models");
            foreach (ModelRateCard model in models.OrderBy(item => item.ModelId, StringComparer.Ordinal))
            {
                writer.WriteStartObject();
                writer.WriteString("id", model.ModelId);
                writer.WriteStartArray("aliases");
                foreach (string alias in model.Aliases.Order(StringComparer.Ordinal))
                {
                    writer.WriteStringValue(alias);
                }

                writer.WriteEndArray();
                writer.WriteStartArray("windows");
                foreach (RateWindow window in model.Windows)
                {
                    writer.WriteStartObject();
                    WriteInstant(writer, "effectiveFrom", window.EffectiveFrom);
                    WriteInstant(writer, "effectiveUntil", window.EffectiveUntil);
                    writer.WriteStartArray("tiers");
                    foreach (RateTier tier in window.Tiers)
                    {
                        writer.WriteStartObject();
                        writer.WriteString("name", tier.Name);
                        if (tier.MaxInputTokens is long maxInputTokens)
                        {
                            writer.WriteNumber("maxInputTokens", maxInputTokens);
                        }
                        else
                        {
                            writer.WriteNull("maxInputTokens");
                        }

                        WriteRate(writer, "inputPer1M", tier.InputPer1M);
                        WriteRate(writer, "cachedInputPer1M", tier.CachedInputPer1M);
                        WriteRate(writer, "cacheWritePer1M", tier.CacheWritePer1M);
                        WriteRate(writer, "outputPer1M", tier.OutputPer1M);
                        writer.WriteEndObject();
                    }

                    writer.WriteEndArray();
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Convert.ToHexStringLower(SHA256.HashData(stream.ToArray()));
    }

    private static void WriteInstant(Utf8JsonWriter writer, string name, DateTimeOffset? value)
    {
        if (value is null)
        {
            writer.WriteNull(name);
            return;
        }

        writer.WriteString(name, value.Value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
    }

    // A validated rate has at most 6 decimal places, so "0.######" writes it exactly.
    private static void WriteRate(Utf8JsonWriter writer, string name, decimal? rate)
    {
        if (rate is null)
        {
            writer.WriteNull(name);
            return;
        }

        writer.WritePropertyName(name);
        writer.WriteRawValue(rate.Value.ToString("0.######", CultureInfo.InvariantCulture));
    }

    private static string ReadEmbedded()
    {
        Assembly assembly = typeof(JsonRateCardCalculator).Assembly;
        using Stream stream = assembly.GetManifestResourceStream(EmbeddedResourceName)
            ?? throw new InvalidDataException($"Embedded rate resource was not found: {EmbeddedResourceName}");
        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }

    private static void Require(bool condition, string? source, string path, string message)
    {
        if (!condition)
        {
            throw Invalid(source, path, message);
        }
    }

    private static InvalidDataException Invalid(
        string? source,
        string path,
        string message,
        Exception? innerException = null)
    {
        string prefix = source is null ? string.Empty : $"{source}: ";
        return new InvalidDataException($"{prefix}{path}: {message}", innerException);
    }

    private sealed record LocalModelRate(string ModelId, IReadOnlyList<string> Aliases)
    {
        public bool Matches(string model) =>
            ModelId.Equals(model, StringComparison.Ordinal) ||
            Aliases.Contains(model, StringComparer.Ordinal);
    }

    private sealed record PricedSelection(RateTier Tier);
}
