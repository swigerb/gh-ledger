using System.Globalization;
using CopilotLedger.Cli.Reporting;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Storage;

namespace CopilotLedger.Cli.Export;

// How the export names repositories, scopes, and counts. Local paths are shown as their folder names unless the export
// includes them.
internal static class ExportRecords
{
    // A shown repository key: a local root (one of roots, or one that looks like a path) becomes its folder name unless
    // includeLocalPaths; a slug or (none) stays as it is.
    public static string Repository(string key, IReadOnlySet<string> roots, bool includeLocalPaths) =>
        !includeLocalPaths && (roots.Contains(key) || LooksLikeLocalPath(key)) ? FolderName(key) : key;

    // A local path's last segment, once its percent-encoded bytes are decoded and trailing / and \ removed, so it's never a
    // path; (root folder) for a root, such as C:\, /, or \\server\share.
    public static string FolderName(string path)
    {
        string trimmed = Unescape(path).TrimEnd('/', '\\');
        string name = trimmed[(trimmed.LastIndexOfAny(['/', '\\']) + 1)..];
        return name.Length == 0 || name.EndsWith(':') || IsShareRoot(trimmed) ? "(root folder)" : name;
    }

    // Starts with / or \, with a drive letter and a colon, or with file:, once its percent-encoded bytes are decoded.
    public static bool LooksLikeLocalPath(string key)
    {
        string path = Unescape(key);
        return path.StartsWith('/') || path.StartsWith('\\') || (path.Length >= 2 && char.IsAsciiLetter(path[0]) && path[1] == ':')
            || path.StartsWith("file:", StringComparison.OrdinalIgnoreCase);
    }

    // text with its percent-encoded bytes decoded until none is left, however deep the encoding, as a file: URI can hold
    // separators: file:///C:%5CUsers%5C…
    private static string Unescape(string text)
    {
        string decoded = Uri.UnescapeDataString(text);
        while (decoded != text)
        {
            text = decoded;
            decoded = Uri.UnescapeDataString(text);
        }

        return text;
    }

    // \\server\share or //server/share, without trailing separators.
    private static bool IsShareRoot(string trimmed) =>
        trimmed.Length > 2 && trimmed[0] is '/' or '\\' && trimmed[1] is '/' or '\\' && trimmed[2..].Split(['/', '\\']).Length == 2;

    // "<repository, or all repositories>[, branch <b>][, pull request #<n>][, conversation <id>]". A local root shows as
    // its folder name even when the export includes local paths, so standard output never shows a path.
    public static string Scope(ExportFilter filter) =>
        (filter.Repository is null
            ? "all repositories"
            : LooksLikeLocalPath(filter.Repository) ? FolderName(filter.Repository) : filter.Repository)
        + (filter.Branch is null ? string.Empty : $", branch {filter.Branch}")
        + (filter.PullRequest is int number
            ? ", pull request #" + number.ToString(CultureInfo.InvariantCulture)
            : string.Empty)
        + (filter.Conversation is null ? string.Empty : $", conversation {filter.Conversation}");

    // count with its noun: "1 request", "8 requests".
    public static string Plural(long count, string one, string many) =>
        $"{LedgerFormat.Count(count)} {(count == 1 ? one : many)}";

    // The line that says the timeline doesn't draw every request or tool call.
    public static string TimelineTruncation(ExportCoverage coverage) =>
        $"The timeline draws the latest {LedgerFormat.Count(coverage.TimelineSpans)} of {Plural(coverage.Spans, "request", "requests")} and {LedgerFormat.Count(coverage.TimelineToolCalls)} of {Plural(coverage.ToolCalls, "tool call", "tool calls")}; the totals and tables include them all.";

    // value when it has at most length UTF-16 code units; else its first length - 1 units and "…", one fewer when the cut
    // would split a surrogate pair (HtmlEncoder would write a lone surrogate as U+FFFD).
    public static string Shorten(string value, int length)
    {
        if (value.Length <= length)
        {
            return value;
        }

        int keep = length - 1;
        if (keep > 0 && char.IsHighSurrogate(value[keep - 1]))
        {
            keep--;
        }

        return value[..keep] + "…";
    }

    // A request's shown repository: its slug, else its root's folder name (the root with includeLocalPaths), else (none).
    public static string SpanRepository(string? slug, string? root, bool includeLocalPaths) =>
        slug ?? (root is null ? "(none)" : includeLocalPaths ? root : FolderName(root));

    public static ExportSpanRecord Span(ExportSpanRow row, bool includeLocalPaths) => new()
    {
        TraceId = row.TraceId,
        SpanId = row.SpanId,
        ParentSpanId = row.ParentSpanId,
        OperationName = row.OperationName,
        ConversationId = row.ConversationId,
        Agent = row.AgentName,
        Provider = row.ProviderName,
        RequestModel = row.RequestModel,
        ResponseModel = row.ResponseModel,
        Model = row.ModelId,
        StartTime = row.StartTime,
        EndTime = row.EndTime,
        InputTokens = row.InputTokens,
        CacheReadInputTokens = row.CacheReadInputTokens,
        CacheWriteInputTokens = row.CacheWriteInputTokens,
        UncachedInputTokens = Math.Max(row.InputTokens - row.CacheReadInputTokens - row.CacheWriteInputTokens, 0),
        OutputTokens = row.OutputTokens,
        CacheReadState = ReportCommands.StateName(row.CacheReadState),
        CacheWriteState = ReportCommands.StateName(row.CacheWriteState),
        BillingChannel = row.BillingChannel,
        PricingStatus = row.PricingStatus,
        RatesVersion = row.RatesVersion,
        CostUsd = row.CostUsd,
        AiCredits = row.BillingChannel == BillingChannel.GitHubAiCredits ? AiCreditMath.UsdToAiCredits(row.CostUsd) : 0m,
        SavedUsd = row.SavedUsd,
        ApiEquivalentUsd = row.ApiEquivalentUsd,
        ApiEquivalentStatus = row.ApiEquivalentStatus,
        ApiRatesVersion = row.ApiRatesVersion,
        ApiRateCardSha256 = row.ApiRateCardSha256,
        Repository = SpanRepository(row.RepositorySlug, row.RepositoryRoot, includeLocalPaths),
        RepositoryRoot = includeLocalPaths ? row.RepositoryRoot : null,
        Branch = row.BranchName,
        HeadCommit = row.HeadCommitSha,
        PullRequest = row.PullRequestNumber,
        IngestSource = row.IngestSource,
    };

    public static ExportToolCallRecord ToolCall(ExportToolCallRow row)
    {
        string status = row.StatusCode switch
        {
            1 => "ok",
            2 => "error",
            _ => "unset",
        };
        return new ExportToolCallRecord
        {
            TraceId = row.TraceId,
            SpanId = row.SpanId,
            ConversationId = row.ConversationId,
            Agent = row.AgentName,
            ToolName = row.ToolName,
            StartTime = row.StartTime,
            ExitCode = row.ToolExitCode,
            Status = status,
            Failed = row.ToolExitCode is not (null or 0) || status == "error",
            IngestSource = row.IngestSource,
        };
    }
}
