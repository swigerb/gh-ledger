namespace CopilotLedger.Core.Audit;

// The tool-call sequences tool_loop and failed_tool_retry read.
internal static class ToolSequences
{
    // Calls with an agent identity, partitioned by their prompt chain's repository, branch, and pull request, their trace,
    // and their agent invocation, each in (start, span ID) order. Another invocation's calls are never in a sequence, but a
    // call of the trace whose invocation is unknown and whose agent name is the sequence's (ordinal; null matches null) may
    // belong to it, so it's in the sequence too, where Runs puts it in no run and lets it end one. Partitions in ordinal key
    // order.
    public static IEnumerable<List<AuditToolCall>> Of(IEnumerable<AuditToolCall> calls)
    {
        List<AuditToolCall> all = [.. calls];
        ILookup<string, AuditToolCall> unknown = all
            .Where(call => call.AgentInvocationId is null)
            .ToLookup(call => call.TraceId, StringComparer.Ordinal);
        return all
            .Where(call => call.AgentInvocationId is not null)
            .GroupBy(call => (call.Repository, call.Branch, call.PullRequestNumber, call.TraceId, call.AgentInvocationId))
            .OrderBy(group => group.Key.Repository, StringComparer.Ordinal)
            .ThenBy(group => group.Key.Branch, StringComparer.Ordinal)
            .ThenBy(group => group.Key.PullRequestNumber ?? 0)
            .ThenBy(group => group.Key.TraceId, StringComparer.Ordinal)
            .ThenBy(group => group.Key.AgentInvocationId, StringComparer.Ordinal)
            .Select(group => AuditMath.ByStart(group.Concat(unknown[group.Key.TraceId].Where(call =>
                string.Equals(call.AgentName, group.First().AgentName, StringComparison.Ordinal)))));
    }

    // The maximal stretches of consecutive calls of sequence with the same tool name that belong(first call, call) accepts;
    // a call without a name or an invocation, or that belong doesn't accept as a first call, is in none, and a call of
    // unknown invocation ends a stretch.
    public static IEnumerable<List<AuditToolCall>> Runs(
        List<AuditToolCall> sequence,
        Func<AuditToolCall, AuditToolCall, bool> belong)
    {
        int index = 0;
        while (index < sequence.Count)
        {
            AuditToolCall first = sequence[index];
            if (first.ToolName is null || first.AgentInvocationId is null || !belong(first, first))
            {
                index++;
                continue;
            }

            int end = index + 1;
            while (end < sequence.Count &&
                   sequence[end].AgentInvocationId is not null &&
                   string.Equals(sequence[end].ToolName, first.ToolName, StringComparison.Ordinal) &&
                   belong(first, sequence[end]))
            {
                end++;
            }

            yield return sequence.GetRange(index, end - index);
            index = end;
        }
    }

    // The requests of the run's agent invocation that started strictly after its first call and before its last: the
    // requests that asked for the repeated calls.
    public static List<AuditSpan> Between(IEnumerable<AuditSpan> spans, List<AuditToolCall> run)
    {
        AuditToolCall first = run[0];
        DateTimeOffset last = run[^1].StartTime;
        return AuditMath.ByStart(spans.Where(span =>
            string.Equals(span.TraceId, first.TraceId, StringComparison.Ordinal) &&
            string.Equals(span.AgentInvocationId, first.AgentInvocationId, StringComparison.Ordinal) &&
            span.StartTime > first.StartTime &&
            span.StartTime < last));
    }

    // A run's scope: its prompt chain's repository, branch, and pull request, its first call's agent, trace, conversation,
    // and tool, and the run's outcome.
    public static AuditScope Scope(List<AuditToolCall> run) => new()
    {
        Repository = run[0].Repository,
        Branch = run[0].Branch,
        PullRequestNumber = run[0].PullRequestNumber,
        Agent = AuditMath.Agent(run[0].AgentName),
        TraceId = run[0].TraceId,
        ConversationId = run[0].ConversationId,
        Tool = run[0].ToolName,
        ToolOutcome = run[0].Outcome,
    };
}
