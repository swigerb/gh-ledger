using System.Diagnostics;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using CopilotLedger.Core.Breaker;
using CopilotLedger.Core.Git;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Storage;
using Microsoft.Data.Sqlite;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace CopilotLedger.Mcp;

internal sealed class LedgerMcpTools(LedgerMcpContext context)
{
    private const string BranchSchemaJson =
        """
        {
          "type": "object",
          "properties": {
            "repository": { "type": "string", "minLength": 1, "description": "Repository key: owner/repo, a repository root, or (none). Pass with branch." },
            "branch": { "type": "string", "minLength": 1, "description": "Branch name, or (none). Pass with repository." }
          },
          "additionalProperties": false
        }
        """;
    private const string PromptSchemaJson =
        """
        {
          "type": "object",
          "properties": {
            "repository": { "type": "string", "minLength": 1, "description": "Repository key: owner/repo, a repository root, or (none). Pass with branch." },
            "branch": { "type": "string", "minLength": 1, "description": "Branch name, or (none). Pass with repository." },
            "limit": { "type": "integer", "minimum": 1, "maximum": 20, "default": 5, "description": "Number of prompt chains." },
            "orderBy": { "type": "string", "enum": ["cost", "outputTokens", "failedToolCalls"], "default": "cost", "description": "Sort key, highest first." }
          },
          "additionalProperties": false
        }
        """;
    private const string BudgetSchemaJson =
        """
        {
          "type": "object",
          "properties": {
            "repository": { "type": "string", "minLength": 1, "description": "Repository key: owner/repo, a repository root, or (none). Pass with branch." },
            "branch": { "type": "string", "minLength": 1, "description": "Branch name, or (none). Pass with repository." },
            "aiCredits": { "type": "number", "exclusiveMinimum": 0, "maximum": 1000000, "description": "Budget in AI credits (1 AI credit = $0.01), with at most 2 decimal places." }
          },
          "required": ["aiCredits"],
          "additionalProperties": false
        }
        """;
    private static readonly JsonElement BranchSchema = ParseSchema(BranchSchemaJson);
    private static readonly JsonElement PromptSchema = ParseSchema(PromptSchemaJson);
    private static readonly JsonElement BudgetSchema = ParseSchema(BudgetSchemaJson);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
    private static readonly Tool[] Tools =
    [
        new()
        {
            Name = "get_branch_ledger",
            Title = "Branch ledger",
            Description = "Spend on one Git branch: AI credits, USD, BYOK USD, local savings, top model, budget, and circuit-breaker status. Uses the current repository and branch unless you pass both.",
            InputSchema = BranchSchema.Clone(),
            Annotations = new ToolAnnotations
            {
                ReadOnlyHint = true,
                OpenWorldHint = false,
            },
        },
        new()
        {
            Name = "get_top_expensive_prompts",
            Title = "Top expensive prompts",
            Description = "The most expensive prompt chains on one Git branch, with their output tokens and failed tool calls (retry loops). Uses the current repository and branch unless you pass both. Returns 5 by default, ordered by cost.",
            InputSchema = PromptSchema.Clone(),
            Annotations = new ToolAnnotations
            {
                ReadOnlyHint = true,
                OpenWorldHint = false,
            },
        },
        new()
        {
            Name = "get_agent_breakdown",
            Title = "Agent breakdown",
            Description = "Spend on one Git branch split by agent (gen_ai.agent.name). Uses the current repository and branch unless you pass both.",
            InputSchema = BranchSchema.Clone(),
            Annotations = new ToolAnnotations
            {
                ReadOnlyHint = true,
                OpenWorldHint = false,
            },
        },
        new()
        {
            Name = "set_branch_budget",
            Title = "Set branch budget",
            Description = "Sets a soft AI-credit budget for one Git branch. CopilotLedger alerts once when the branch's billed spend exceeds it; nothing is blocked. Uses the current repository and branch unless you pass both.",
            InputSchema = BudgetSchema.Clone(),
            Annotations = new ToolAnnotations
            {
                ReadOnlyHint = false,
                DestructiveHint = false,
                IdempotentHint = true,
                OpenWorldHint = false,
            },
        },
    ];
    private readonly LedgerMcpContext _context = context;
    private readonly SemaphoreSlim _storeGate = new(1, 1);
    private SqliteLedgerStore? _store;

    public ListToolsResult List() => new() { Tools = Tools };

    public async ValueTask<CallToolResult> CallAsync(
        CallToolRequestParams request,
        CancellationToken cancellationToken)
    {
        string name = request.Name;
        string[] allowed = name switch
        {
            "get_branch_ledger" or "get_agent_breakdown" => ["repository", "branch"],
            "get_top_expensive_prompts" =>
                ["repository", "branch", "limit", "orderBy"],
            "set_branch_budget" => ["repository", "branch", "aiCredits"],
            _ => throw new McpProtocolException(
                $"Unknown tool: {name}",
                McpErrorCode.InvalidParams),
        };
        IDictionary<string, JsonElement> arguments =
            request.Arguments ?? new Dictionary<string, JsonElement>();
        CallToolResult? invalid = Validate(name, allowed, arguments, out ToolArguments values);
        if (invalid is not null)
        {
            return invalid;
        }

        ScopeResult scope = ResolveScope(values.Repository, values.Branch);
        if (scope.Error is not null)
        {
            return Error(scope.Error);
        }

        try
        {
            SqliteLedgerStore store = await OpenStoreAsync(cancellationToken);
            JsonObject result = name switch
            {
                "get_branch_ledger" => await BranchLedgerAsync(
                    store,
                    scope.Repository!,
                    scope.Branch!,
                    cancellationToken),
                "get_top_expensive_prompts" => await TopPromptsAsync(
                    store,
                    scope.Repository!,
                    scope.Branch!,
                    values.OrderBy,
                    values.Limit,
                    cancellationToken),
                "get_agent_breakdown" => await AgentBreakdownAsync(
                    store,
                    scope.Repository!,
                    scope.Branch!,
                    cancellationToken),
                "set_branch_budget" => await SetBudgetAsync(
                    store,
                    scope.Repository!,
                    scope.Branch!,
                    values.AiCredits,
                    cancellationToken),
                _ => throw new UnreachableException(),
            };
            return Success(result);
        }
        catch (Exception exception) when (exception is SqliteException
            or IOException
            or UnauthorizedAccessException
            or InvalidOperationException)
        {
            return Error(
                $"The ledger at {_context.DatabasePath} is unavailable: {exception.Message}");
        }
    }

    private static CallToolResult? Validate(
        string name,
        IReadOnlyCollection<string> allowed,
        IDictionary<string, JsonElement> arguments,
        out ToolArguments values)
    {
        values = ToolArguments.Empty;
        string? unknown = arguments.Keys
            .Where(key => !allowed.Contains(key))
            .Order(StringComparer.Ordinal)
            .FirstOrDefault();
        if (unknown is not null)
        {
            return Error($"Unknown argument: {unknown}.");
        }

        string? repository = null;
        if (arguments.TryGetValue("repository", out JsonElement repositoryElement))
        {
            if (repositoryElement.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(repositoryElement.GetString()))
            {
                return Error("repository must be a non-empty string.");
            }

            repository = repositoryElement.GetString();
        }

        string? branch = null;
        if (arguments.TryGetValue("branch", out JsonElement branchElement))
        {
            if (branchElement.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(branchElement.GetString()))
            {
                return Error("branch must be a non-empty string.");
            }

            branch = branchElement.GetString();
        }

        if ((repository is null) != (branch is null))
        {
            return Error("Pass repository and branch together, or neither.");
        }

        int limit = 5;
        if (arguments.TryGetValue("limit", out JsonElement limitElement)
            && (limitElement.ValueKind != JsonValueKind.Number
                || !limitElement.TryGetInt32(out limit)
                || limit is < 1 or > 20))
        {
            return Error("limit must be an integer from 1 to 20.");
        }

        string orderBy = "cost";
        if (arguments.TryGetValue("orderBy", out JsonElement orderByElement))
        {
            if (orderByElement.ValueKind != JsonValueKind.String)
            {
                return Error("orderBy must be cost, outputTokens, or failedToolCalls.");
            }

            string? requested = orderByElement.GetString();
            if (requested is not ("cost" or "outputTokens" or "failedToolCalls"))
            {
                return Error("orderBy must be cost, outputTokens, or failedToolCalls.");
            }

            orderBy = requested;
        }

        decimal aiCredits = decimal.Zero;
        if (name == "set_branch_budget"
            && !arguments.TryGetValue("aiCredits", out _))
        {
            return Error("aiCredits is required.");
        }

        if (arguments.TryGetValue("aiCredits", out JsonElement creditElement)
            && (creditElement.ValueKind != JsonValueKind.Number
                || !creditElement.TryGetDecimal(out aiCredits)
                || aiCredits <= 0m
                || aiCredits > 1_000_000m
                || aiCredits % 0.01m != 0m))
        {
            return Error(
                "aiCredits must be a number greater than 0 and at most 1000000, with at most 2 decimal places.");
        }

        values = new ToolArguments(repository, branch, limit, orderBy, aiCredits);
        return null;
    }

    private ScopeResult ResolveScope(string? repository, string? branch)
    {
        if (repository is not null && branch is not null)
        {
            return new ScopeResult(repository, branch, null);
        }

        string sessionDirectory =
            _context.GetEnvironmentVariable("COPILOT_SESSION_CWD") ?? string.Empty;
        string directory =
            !string.IsNullOrWhiteSpace(sessionDirectory)
            && Path.IsPathFullyQualified(sessionDirectory)
            && Directory.Exists(sessionDirectory)
                ? sessionDirectory
                : _context.WorkingDirectory;
        GitWorkspaceContext? git = null;
        try
        {
            git = _context.GitResolver.Resolve(directory);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or ArgumentException)
        {
        }

        return git is null
            ? new ScopeResult(
                null,
                null,
                $"No Git repository at {directory}. Pass repository and branch.")
            : new ScopeResult(
                git.RepositorySlug ?? git.RepositoryRoot,
                git.BranchName ?? "(none)",
                null);
    }

    private async Task<SqliteLedgerStore> OpenStoreAsync(
        CancellationToken cancellationToken)
    {
        await _storeGate.WaitAsync(cancellationToken);
        try
        {
            if (_store is null)
            {
                SqliteLedgerStore store = new(
                    _context.DatabasePath,
                    _context.TimeProvider);
                await store.InitializeAsync(cancellationToken);
                _store = store;
            }

            return _store;
        }
        finally
        {
            _storeGate.Release();
        }
    }

    private async Task<JsonObject> BranchLedgerAsync(
        SqliteLedgerStore store,
        string repository,
        string branch,
        CancellationToken cancellationToken)
    {
        LedgerFilter filter = new() { Repository = repository, Branch = branch };
        IReadOnlyList<LedgerRollupRow> totals =
            await store.GetRollupAsync(LedgerGrouping.Branch, filter, cancellationToken);
        LedgerRollupRow total = totals.SingleOrDefault() ?? EmptyRollup();
        IReadOnlyList<LedgerRollupRow> models =
            await store.GetRollupAsync(LedgerGrouping.Model, filter, cancellationToken);
        WarningResult warnings = await WarningsAsync(
            store,
            repository,
            branch,
            cancellationToken);

        return new JsonObject
        {
            ["repository"] = repository,
            ["branch"] = branch,
            ["spans"] = total.SpanCount,
            ["unpricedSpans"] = total.UnpricedSpanCount,
            ["inputTokens"] = total.InputTokens,
            ["outputTokens"] = total.OutputTokens,
            ["cacheReadInputTokens"] = total.CacheReadInputTokens,
            ["cacheWriteInputTokens"] = total.CacheWriteInputTokens,
            ["aiCredits"] = Money(total.AiCredits),
            ["aiCreditsUsd"] = Money(total.AiCreditsUsd),
            ["foundryByokUsd"] = Money(total.FoundryByokUsd),
            ["localSavedUsd"] = Money(total.LocalSavedUsd),
            ["billedAiCredits"] = Money(
                BilledAiCredits(total.AiCredits, total.FoundryByokUsd)),
            ["topModel"] = models.FirstOrDefault()?.Model,
            ["budget"] = warnings.Budget,
            ["circuitBreaker"] = warnings.CircuitBreaker,
            ["warnings"] = new JsonArray(
                warnings.Warnings.Select(message => JsonValue.Create(message)).ToArray()),
        };
    }

    private async Task<JsonObject> TopPromptsAsync(
        SqliteLedgerStore store,
        string repository,
        string branch,
        string orderBy,
        int limit,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<PromptChainRow> candidates =
            await store.GetTopPromptChainsAsync(
                new LedgerFilter { Repository = repository, Branch = branch },
                1000,
                cancellationToken);
        IReadOnlyDictionary<string, long> failures =
            await store.GetFailedToolCallCountsAsync(
                candidates.Select(candidate => candidate.TraceId),
                cancellationToken);
        IOrderedEnumerable<PromptChainRow> ordered = orderBy switch
        {
            "outputTokens" => candidates.OrderByDescending(row => row.OutputTokens),
            "failedToolCalls" => candidates.OrderByDescending(
                row => failures.GetValueOrDefault(row.TraceId)),
            _ => candidates.OrderByDescending(
                row => row.AiCreditsUsd + row.FoundryByokUsd),
        };
        PromptChainRow[] prompts = ordered
            .ThenByDescending(row => row.AiCreditsUsd + row.FoundryByokUsd)
            .ThenByDescending(row => row.StartTime)
            .ThenBy(row => row.TraceId, StringComparer.Ordinal)
            .Take(limit)
            .ToArray();
        WarningResult warnings = await WarningsAsync(
            store,
            repository,
            branch,
            cancellationToken);
        JsonArray resultPrompts = [];
        for (int index = 0; index < prompts.Length; index++)
        {
            PromptChainRow row = prompts[index];
            resultPrompts.Add(new JsonObject
            {
                ["rank"] = index + 1,
                ["traceId"] = row.TraceId,
                ["conversationId"] = row.ConversationId,
                ["startTime"] = FormatTime(row.StartTime),
                ["endTime"] = FormatTime(row.EndTime),
                ["agents"] = new JsonArray(
                    row.Agents.Select(agent => JsonValue.Create(agent)).ToArray()),
                ["models"] = new JsonArray(
                    row.Models.Select(model => JsonValue.Create(model)).ToArray()),
                ["spans"] = row.SpanCount,
                ["unpricedSpans"] = row.UnpricedSpanCount,
                ["inputTokens"] = row.InputTokens,
                ["outputTokens"] = row.OutputTokens,
                ["cacheReadInputTokens"] = row.CacheReadInputTokens,
                ["cacheWriteInputTokens"] = row.CacheWriteInputTokens,
                ["aiCredits"] = Money(row.AiCredits),
                ["aiCreditsUsd"] = Money(row.AiCreditsUsd),
                ["foundryByokUsd"] = Money(row.FoundryByokUsd),
                ["localSavedUsd"] = Money(row.LocalSavedUsd),
                ["billedAiCredits"] = Money(
                    BilledAiCredits(row.AiCredits, row.FoundryByokUsd)),
                ["failedToolCalls"] = failures.GetValueOrDefault(row.TraceId),
            });
        }

        return new JsonObject
        {
            ["repository"] = repository,
            ["branch"] = branch,
            ["orderBy"] = orderBy,
            ["limit"] = limit,
            ["prompts"] = resultPrompts,
            ["circuitBreaker"] = warnings.CircuitBreaker,
            ["warnings"] = new JsonArray(
                warnings.Warnings.Select(message => JsonValue.Create(message)).ToArray()),
        };
    }

    private async Task<JsonObject> AgentBreakdownAsync(
        SqliteLedgerStore store,
        string repository,
        string branch,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<LedgerRollupRow> rows = await store.GetRollupAsync(
            LedgerGrouping.Agent,
            new LedgerFilter { Repository = repository, Branch = branch },
            cancellationToken);
        WarningResult warnings = await WarningsAsync(
            store,
            repository,
            branch,
            cancellationToken);
        JsonArray agents = [];
        foreach (LedgerRollupRow row in rows)
        {
            agents.Add(new JsonObject
            {
                ["agent"] = row.Agent,
                ["spans"] = row.SpanCount,
                ["unpricedSpans"] = row.UnpricedSpanCount,
                ["inputTokens"] = row.InputTokens,
                ["outputTokens"] = row.OutputTokens,
                ["cacheReadInputTokens"] = row.CacheReadInputTokens,
                ["cacheWriteInputTokens"] = row.CacheWriteInputTokens,
                ["aiCredits"] = Money(row.AiCredits),
                ["aiCreditsUsd"] = Money(row.AiCreditsUsd),
                ["foundryByokUsd"] = Money(row.FoundryByokUsd),
                ["localSavedUsd"] = Money(row.LocalSavedUsd),
                ["billedAiCredits"] = Money(
                    BilledAiCredits(row.AiCredits, row.FoundryByokUsd)),
            });
        }

        return new JsonObject
        {
            ["repository"] = repository,
            ["branch"] = branch,
            ["agents"] = agents,
            ["circuitBreaker"] = warnings.CircuitBreaker,
            ["warnings"] = new JsonArray(
                warnings.Warnings.Select(message => JsonValue.Create(message)).ToArray()),
        };
    }

    private async Task<JsonObject> SetBudgetAsync(
        SqliteLedgerStore store,
        string repository,
        string branch,
        decimal aiCredits,
        CancellationToken cancellationToken)
    {
        await store.SetBranchBudgetAsync(
            repository,
            branch,
            aiCredits,
            cancellationToken);
        WarningResult warnings = await WarningsAsync(
            store,
            repository,
            branch,
            cancellationToken);
        return new JsonObject
        {
            ["repository"] = repository,
            ["branch"] = branch,
            ["budget"] = warnings.Budget,
            ["circuitBreaker"] = warnings.CircuitBreaker,
            ["warnings"] = new JsonArray(
                warnings.Warnings.Select(message => JsonValue.Create(message)).ToArray()),
        };
    }

    private async Task<WarningResult> WarningsAsync(
        SqliteLedgerStore store,
        string repository,
        string branch,
        CancellationToken cancellationToken)
    {
        string? sessionId = _context.GetEnvironmentVariable("COPILOT_AGENT_SESSION_ID");
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            sessionId = null;
        }

        IReadOnlyList<BreakerTrip> allTrips =
            await store.GetOpenBreakerTripsAsync(
                BreakerTripFilter.All,
                cancellationToken);
        BreakerTrip[] trips = allTrips
            .Where(trip =>
                (string.Equals(trip.Repository, repository, StringComparison.Ordinal)
                    && string.Equals(trip.Branch, branch, StringComparison.Ordinal))
                || (sessionId is not null
                    && string.Equals(
                        trip.ConversationId,
                        sessionId,
                        StringComparison.Ordinal)))
            .ToArray();
        IReadOnlyList<BranchBudgetRow> budgets =
            await store.GetBranchBudgetsAsync(
                repository,
                branch,
                cancellationToken);
        BranchBudgetRow? budget = budgets.SingleOrDefault();
        List<string> warningText = [];
        JsonArray openTrips = [];
        foreach (BreakerTrip trip in trips)
        {
            bool currentSession =
                sessionId is not null
                && string.Equals(
                    trip.ConversationId,
                    sessionId,
                    StringComparison.Ordinal);
            openTrips.Add(new JsonObject
            {
                ["id"] = trip.Id,
                ["rule"] = BreakerRuleNames.ToName(trip.Rule),
                ["repository"] = trip.Repository,
                ["branch"] = trip.Branch,
                ["traceId"] = trip.TraceId,
                ["conversationId"] = trip.ConversationId,
                ["currentSession"] = currentSession,
                ["trippedAt"] = FormatTime(trip.TrippedAt),
                ["detail"] = trip.Detail,
                ["resetCommand"] = $"gh-ledger breaker reset --id {trip.Id}",
            });
            warningText.Add(
                $"WARNING: CopilotLedger circuit breaker is open for {trip.Repository} @ {trip.Branch}: {trip.Detail} on one prompt (trip {trip.Id}). Stop and ask the user before continuing. The user can reset it with: gh-ledger breaker reset --id {trip.Id}");
        }

        JsonObject? budgetObject = null;
        if (budget is not null)
        {
            budgetObject = new JsonObject
            {
                ["aiCredits"] = Money(budget.BudgetAiCredits),
                ["spentAiCredits"] = Money(budget.SpentAiCredits),
                ["exceeded"] = budget.Exceeded,
            };
            if (budget.Exceeded)
            {
                warningText.Add(
                    $"WARNING: {repository} @ {branch} has spent {Credits(budget.SpentAiCredits)} of its {Credits(budget.BudgetAiCredits)} AI credit budget.");
            }
        }

        JsonObject circuitBreaker = new()
        {
            ["state"] = trips.Length == 0 ? "closed" : "open",
            ["openTrips"] = openTrips,
        };
        return new WarningResult(budgetObject, circuitBreaker, warningText);
    }

    private static CallToolResult Success(JsonObject result)
    {
        string json = result.ToJsonString(JsonOptions);
        List<string> warnings = result["warnings"]!
            .AsArray()
            .Select(value => value!.GetValue<string>())
            .ToList();
        return new CallToolResult
        {
            StructuredContent = JsonSerializer.SerializeToElement(result, JsonOptions),
            Content =
            [
                new TextContentBlock
                {
                    Text = warnings.Count == 0
                        ? json
                        : string.Join('\n', warnings) + "\n" + json,
                },
            ],
        };
    }

    private static CallToolResult Error(string message) => new()
    {
        IsError = true,
        Content = [new TextContentBlock { Text = message }],
    };

    private static LedgerRollupRow EmptyRollup() => new()
    {
        SpanCount = 0,
        UnpricedSpanCount = 0,
        InputTokens = 0,
        OutputTokens = 0,
        CacheReadInputTokens = 0,
        CacheWriteInputTokens = 0,
        CacheReportedSpanCount = 0,
        ReportedInputTokens = 0,
        ReportedCacheReadInputTokens = 0,
        ReportedUncachedInputTokens = 0,
        AiCredits = 0m,
        AiCreditsUsd = 0m,
        FoundryByokUsd = 0m,
        LocalSavedUsd = 0m,
    };

    private static JsonElement ParseSchema(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static decimal BilledAiCredits(decimal aiCredits, decimal byokUsd) =>
        aiCredits + byokUsd / AiCreditMath.UsdPerAiCredit;

    private static JsonNode Money(decimal value) =>
        JsonNode.Parse(
            value.ToString("0.############", CultureInfo.InvariantCulture))!;

    private static string Credits(decimal credits) =>
        decimal.Round(credits, 2, MidpointRounding.AwayFromZero)
            .ToString("N2", CultureInfo.InvariantCulture);

    private static string FormatTime(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private sealed record ToolArguments(
        string? Repository,
        string? Branch,
        int Limit,
        string OrderBy,
        decimal AiCredits)
    {
        public static ToolArguments Empty { get; } =
            new(null, null, 5, "cost", decimal.Zero);
    }

    private sealed record ScopeResult(
        string? Repository,
        string? Branch,
        string? Error);

    private sealed record WarningResult(
        JsonObject? Budget,
        JsonObject CircuitBreaker,
        IReadOnlyList<string> Warnings);
}
