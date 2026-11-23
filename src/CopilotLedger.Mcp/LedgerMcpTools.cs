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
            "orderBy": { "type": "string", "enum": ["cost", "outputTokens", "failedToolCalls", "apiEquivalentUsd"], "default": "cost", "description": "Sort key, highest first. With apiEquivalentUsd, prompt chains without an API-equivalent price come last." }
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
    private const string CacheSchemaJson =
        """
        {
          "type": "object",
          "properties": {
            "repository": { "type": "string", "minLength": 1, "description": "Repository key: owner/repo, a repository root, or (none). Defaults to the current repository." },
            "branch": { "type": "string", "minLength": 1, "description": "Only this branch, or (none)." },
            "pullRequest": { "type": "integer", "minimum": 1, "description": "Only this pull request number." },
            "since": { "type": "string", "default": "30d", "description": "Period: today, all, Nd (1 to 3650 days), or yyyy-MM-dd." },
            "by": { "type": "string", "enum": ["model", "branch", "pr", "agent"], "default": "model", "description": "Group rows by model, branch, pull request, or agent." },
            "limit": { "type": "integer", "minimum": 1, "maximum": 50, "default": 10, "description": "Number of rows, most input first." }
          },
          "additionalProperties": false
        }
        """;
    private static readonly JsonElement BranchSchema = ParseSchema(BranchSchemaJson);
    private static readonly JsonElement PromptSchema = ParseSchema(PromptSchemaJson);
    private static readonly JsonElement BudgetSchema = ParseSchema(BudgetSchemaJson);
    private static readonly JsonElement CacheSchema = ParseSchema(CacheSchemaJson);
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
            Description = "Spend on one Git branch: AI credits, USD, BYOK USD, local savings, API-equivalent USD (the same requests at API list prices, for comparison only), top model, budget, and circuit-breaker status. Uses the current repository and branch unless you pass both.",
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
            Name = "get_cache_efficiency",
            Title = "Cache efficiency",
            Description = "Prompt-cache efficiency in one repository, grouped by model, branch, pull request, or agent: the cache hit ratio, input read from and written to the cache, uncached input, and the largest prompt. Spans that reported no cache counters count as unknown, never as misses. Uses the current repository unless you pass one.",
            InputSchema = CacheSchema.Clone(),
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
            "get_cache_efficiency" =>
                ["repository", "branch", "pullRequest", "since", "by", "limit"],
            "set_branch_budget" => ["repository", "branch", "aiCredits"],
            _ => throw new McpProtocolException(
                $"Unknown tool: {name}",
                McpErrorCode.InvalidParams),
        };
        IDictionary<string, JsonElement> arguments =
            request.Arguments ?? new Dictionary<string, JsonElement>();
        CallToolResult? invalid = Validate(
            name,
            allowed,
            arguments,
            _context.TimeProvider,
            out ToolArguments values);
        if (invalid is not null)
        {
            return invalid;
        }

        // get_cache_efficiency reports one repository, the current one by default, and warns about the current branch.
        GitWorkspaceContext? current = null;
        ScopeResult scope;
        if (name == "get_cache_efficiency")
        {
            (string directory, current) = ResolveCurrent();
            string? repository = values.Repository ?? current?.RepositorySlug ?? current?.RepositoryRoot;
            if (repository is null)
            {
                return Error($"No Git repository at {directory}. Pass repository.");
            }

            scope = new ScopeResult(repository, values.Branch, null);
        }
        else
        {
            scope = ResolveScope(values.Repository, values.Branch);
            if (scope.Error is not null)
            {
                return Error(scope.Error);
            }
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
                "get_cache_efficiency" => await CacheEfficiencyAsync(
                    store,
                    scope.Repository!,
                    current,
                    values,
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
        TimeProvider timeProvider,
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

        bool cacheTool = name == "get_cache_efficiency";
        if (!cacheTool && (repository is null) != (branch is null))
        {
            return Error("Pass repository and branch together, or neither.");
        }

        int maximum = cacheTool ? 50 : 20;
        int limit = cacheTool ? 10 : 5;
        if (arguments.TryGetValue("limit", out JsonElement limitElement)
            && (limitElement.ValueKind != JsonValueKind.Number
                || !limitElement.TryGetInt32(out limit)
                || limit < 1
                || limit > maximum))
        {
            return Error($"limit must be an integer from 1 to {maximum}.");
        }

        string orderBy = "cost";
        if (arguments.TryGetValue("orderBy", out JsonElement orderByElement))
        {
            if (orderByElement.ValueKind != JsonValueKind.String)
            {
                return Error("orderBy must be cost, outputTokens, failedToolCalls, or apiEquivalentUsd.");
            }

            string? requested = orderByElement.GetString();
            if (requested is not ("cost" or "outputTokens" or "failedToolCalls" or "apiEquivalentUsd"))
            {
                return Error("orderBy must be cost, outputTokens, failedToolCalls, or apiEquivalentUsd.");
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

        int? pullRequest = null;
        if (arguments.TryGetValue("pullRequest", out JsonElement pullRequestElement))
        {
            if (pullRequestElement.ValueKind != JsonValueKind.Number
                || !pullRequestElement.TryGetInt32(out int number)
                || number < 1)
            {
                return Error("pullRequest must be an integer of at least 1.");
            }

            pullRequest = number;
        }

        LedgerPeriod? period = null;
        if (cacheTool)
        {
            string since = "30d";
            if (arguments.TryGetValue("since", out JsonElement sinceElement))
            {
                since = sinceElement.ValueKind == JsonValueKind.String
                    ? sinceElement.GetString()!
                    : string.Empty;
            }

            if (!LedgerPeriod.TryParse(since, timeProvider, out period))
            {
                return Error("since must be today, all, <N>d (1-3650), or yyyy-MM-dd.");
            }
        }

        string by = "model";
        if (arguments.TryGetValue("by", out JsonElement byElement))
        {
            string? requested = byElement.ValueKind == JsonValueKind.String
                ? byElement.GetString()
                : null;
            if (requested is not ("model" or "branch" or "pr" or "agent"))
            {
                return Error("by must be model, branch, pr, or agent.");
            }

            by = requested;
        }

        values = new ToolArguments(repository, branch, limit, orderBy, aiCredits, pullRequest, period, by);
        return null;
    }

    private ScopeResult ResolveScope(string? repository, string? branch)
    {
        if (repository is not null && branch is not null)
        {
            return new ScopeResult(repository, branch, null);
        }

        (string directory, GitWorkspaceContext? git) = ResolveCurrent();
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

    // The session's directory, COPILOT_SESSION_CWD when it's a full path to a directory or else the server's, and its Git
    // context, null outside a repository.
    private (string Directory, GitWorkspaceContext? Git) ResolveCurrent()
    {
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

        return (directory, git);
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
            ["uncachedInputTokens"] = Uncached(total),
            ["cacheReportedSpans"] = total.CacheReportedSpanCount,
            ["cacheHitRatio"] = Ratio(total.CacheHitRatio),
            ["aiCredits"] = Money(total.AiCredits),
            ["aiCreditsUsd"] = Money(total.AiCreditsUsd),
            ["foundryByokUsd"] = Money(total.FoundryByokUsd),
            ["localSavedUsd"] = Money(total.LocalSavedUsd),
            ["apiEquivalentUsd"] = NullableMoney(total.ApiEquivalentUsd),
            ["apiEquivalentUnpricedSpans"] = total.ApiEquivalentUnpricedSpanCount,
            ["apiEquivalentEstimatedSpans"] = total.ApiEquivalentEstimatedSpanCount,
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
            "apiEquivalentUsd" => candidates
                .OrderBy(row => row.ApiEquivalentUsd is null)
                .ThenByDescending(row => row.ApiEquivalentUsd ?? 0m),
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
                ["apiEquivalentUsd"] = NullableMoney(row.ApiEquivalentUsd),
                ["apiEquivalentUnpricedSpans"] = row.ApiEquivalentUnpricedSpanCount,
                ["apiEquivalentEstimatedSpans"] = row.ApiEquivalentEstimatedSpanCount,
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
                ["uncachedInputTokens"] = Uncached(row),
                ["cacheReportedSpans"] = row.CacheReportedSpanCount,
                ["cacheHitRatio"] = Ratio(row.CacheHitRatio),
                ["aiCredits"] = Money(row.AiCredits),
                ["aiCreditsUsd"] = Money(row.AiCreditsUsd),
                ["foundryByokUsd"] = Money(row.FoundryByokUsd),
                ["localSavedUsd"] = Money(row.LocalSavedUsd),
                ["apiEquivalentUsd"] = NullableMoney(row.ApiEquivalentUsd),
                ["apiEquivalentUnpricedSpans"] = row.ApiEquivalentUnpricedSpanCount,
                ["apiEquivalentEstimatedSpans"] = row.ApiEquivalentEstimatedSpanCount,
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

    // current is the session's Git context, whose branch the warnings describe; without one, they come only from trips of
    // the current Copilot session.
    private async Task<JsonObject> CacheEfficiencyAsync(
        SqliteLedgerStore store,
        string repository,
        GitWorkspaceContext? current,
        ToolArguments values,
        CancellationToken cancellationToken)
    {
        LedgerGrouping grouping = values.By switch
        {
            "branch" => LedgerGrouping.Branch,
            "pr" => LedgerGrouping.PullRequest,
            "agent" => LedgerGrouping.Agent,
            _ => LedgerGrouping.Model,
        };
        IReadOnlyList<CacheEfficiencyRow> rows = await store.GetCacheEfficiencyAsync(
            grouping,
            new LedgerFilter
            {
                From = values.Period!.From,
                Repository = repository,
                Branch = values.Branch,
                PullRequestNumber = values.PullRequest,
            },
            cancellationToken);
        WarningResult warnings = await WarningsAsync(
            store,
            current is null ? null : current.RepositorySlug ?? current.RepositoryRoot,
            current is null ? null : current.BranchName ?? "(none)",
            cancellationToken);
        JsonArray resultRows = [];
        foreach (CacheEfficiencyRow row in rows.Take(values.Limit))
        {
            JsonObject item = grouping switch
            {
                LedgerGrouping.Model => new JsonObject { ["model"] = row.Model },
                LedgerGrouping.Agent => new JsonObject { ["agent"] = row.Agent },
                LedgerGrouping.Branch => new JsonObject
                {
                    ["repository"] = row.Repository,
                    ["branch"] = row.Branch,
                },
                _ => new JsonObject
                {
                    ["repository"] = row.Repository,
                    ["pullRequest"] = row.PullRequestNumber,
                    ["branch"] = row.Branch,
                },
            };
            item["spans"] = row.SpanCount;
            item["cacheReportedSpans"] = row.CacheReportedSpanCount;
            item["cacheUnreportedSpans"] = row.CacheUnreportedSpanCount;
            item["inputTokens"] = row.InputTokens;
            item["outputTokens"] = row.OutputTokens;
            item["maxInputTokens"] = row.MaxInputTokens;
            item["averageInputTokensPerTurn"] = Ratio(row.AverageInputTokensPerTurn);
            item["reportedInputTokens"] = row.ReportedInputTokens;
            item["reportedCacheReadInputTokens"] = row.ReportedCacheReadInputTokens;
            item["reportedCacheWriteInputTokens"] = row.ReportedCacheWriteInputTokens;
            item["uncachedInputTokens"] = row.UncachedInputTokens;
            item["estimatedUncachedInputTokens"] = row.EstimatedUncachedInputTokens;
            item["cacheHitRatio"] = Ratio(row.CacheHitRatio);
            resultRows.Add(item);
        }

        return new JsonObject
        {
            ["repository"] = repository,
            ["branch"] = values.Branch,
            ["pullRequest"] = values.PullRequest,
            ["period"] = values.Period.Label,
            ["from"] = values.Period.From is DateTimeOffset from ? FormatTime(from) : null,
            ["by"] = values.By,
            ["limit"] = values.Limit,
            ["rows"] = resultRows,
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

    // repository and branch are the current branch; when they're null, warnings come only from the current session's trips.
    private async Task<WarningResult> WarningsAsync(
        SqliteLedgerStore store,
        string? repository,
        string? branch,
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
                (repository is not null
                    && string.Equals(trip.Repository, repository, StringComparison.Ordinal)
                    && string.Equals(trip.Branch, branch, StringComparison.Ordinal))
                || (sessionId is not null
                    && string.Equals(
                        trip.ConversationId,
                        sessionId,
                        StringComparison.Ordinal)))
            .ToArray();
        IReadOnlyList<BranchBudgetRow> budgets = repository is null || branch is null
            ? []
            : await store.GetBranchBudgetsAsync(
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
        ApiEquivalentUsd = 0m,
        ApiEquivalentUnpricedSpanCount = 0,
        ApiEquivalentEstimatedSpanCount = 0,
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

    // An API-equivalent amount, or null when nothing was priced: never 0 for an unknown price.
    private static JsonNode? NullableMoney(decimal? value) =>
        value is null ? null : Money(value.Value);

    // value with every digit decimal division gave it, or null.
    private static JsonNode? Ratio(decimal? value) =>
        value is null
            ? null
            : JsonNode.Parse(value.Value.ToString(CultureInfo.InvariantCulture));

    // Observed uncached input; null when no span reported cache usage.
    private static long? Uncached(LedgerRollupRow row) =>
        row.CacheReportedSpanCount == 0 ? null : row.ReportedUncachedInputTokens;

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
        decimal AiCredits,
        int? PullRequest,
        LedgerPeriod? Period,
        string By)
    {
        public static ToolArguments Empty { get; } =
            new(null, null, 5, "cost", decimal.Zero, null, null, "model");
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
