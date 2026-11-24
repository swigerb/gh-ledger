using CopilotLedger.Core.Audit;
using CopilotLedger.Storage;

namespace CopilotLedger.Tests.TestSupport;

// What gh ledger audit writes for AuditSeeds with its defaults (every repository, --since 7d, the embedded rates, at
// AuditSeeds.GeneratedAt in UTC-5), and the report it writes. Tool history starts at the earliest span link the ledger keeps,
// I1's tool call at 11-20 12:00, which is later than the retention's start. No request in the window started before it, but
// Codex's X1 has no tool-call count, so tool history is insufficient.
public static class AuditOutputs
{
    // --since 7d at AuditSeeds.GeneratedAt in UTC-5.
    public static readonly LedgerFilter Window = new()
    {
        From = new DateTimeOffset(2026, 11, 15, 5, 0, 0, TimeSpan.Zero),
        To = AuditSeeds.GeneratedAt,
    };

    // The output of gh ledger audit --json, without its final line break. A checkout with CRLF line endings writes this
    // file's raw strings with CRLF, so the expected values use ReplaceLineEndings.
    public static readonly string Json = """
        {
          "schemaVersion": 1,
          "generatedAtUtc": "2026-11-21T21:30:00.0000000+00:00",
          "filter": {
            "repository": null,
            "branch": null,
            "pullRequest": null,
            "conversation": null,
            "since": "7d",
            "period": "last 7 days",
            "fromUtc": "2026-11-15T05:00:00.0000000+00:00",
            "toUtc": "2026-11-21T21:30:00.0000000+00:00"
          },
          "impactLens": "billed",
          "ratesVersion": "2026-11-23",
          "economyModel": "mai-code-2",
          "thresholds": {
            "turnVelocityRequests": 30,
            "turnVelocityWindowMinutes": 5,
            "toolLoopCalls": 8,
            "failedToolRetryCalls": 3,
            "contextBloatInputTokens": 50000,
            "contextBloatRatio": 200,
            "contextGrowthRequests": 10,
            "contextGrowthRatio": 4,
            "cacheMissStreakRequests": 5,
            "cacheMissInputTokens": 20000,
            "cacheMissHitRatio": 0.1,
            "trivialOutputTokens": 200,
            "trivialRequestsPerDay": 10,
            "nearBreakerFraction": 0.8,
            "breakerVelocityAiCredits": 150,
            "breakerVelocityWindowMinutes": 5,
            "breakerChurnFailures": 5
          },
          "rules": [
            "turn_velocity",
            "tool_loop",
            "failed_tool_retry",
            "context_bloat",
            "context_growth",
            "cache_miss_streak",
            "premium_model_trivial_turn",
            "velocity_near_breaker"
          ],
          "coverage": {
            "spans": 29,
            "spansBySource": [
              {
                "source": "import:codex",
                "spans": 1
              },
              {
                "source": "import:copilot",
                "spans": 2
              },
              {
                "source": "otlp",
                "spans": 26
              }
            ],
            "toolCalls": 7,
            "unpricedSpans": 1,
            "cacheReportedSpans": 26,
            "cacheAbsentSpans": 3,
            "cacheLegacyUnknownSpans": 0,
            "spansWithoutAgentIdentity": 3,
            "toolCallsWithoutAgentIdentity": 0,
            "spansWithUnknownToolCount": 2,
            "insufficientToolHistory": true,
            "toolHistoryRetentionDays": 7,
            "toolHistoryFromUtc": "2026-11-20T12:00:00.0000000+00:00",
            "toolHistoryStart": "earliest_link",
            "spansBeforeToolHistory": 0,
            "importedToolCallsWithoutDetail": 0,
            "importedRequestsWithoutToolData": 1,
            "supersededMatchedImportSpans": 0,
            "supersededUnmatchedImportSpans": 0,
            "ledgerAmbiguousImportRecords": 0,
            "ledgerIncompleteImportLogs": 0
          },
          "minSeverity": "info",
          "limit": 20,
          "findingsTotal": 4,
          "findings": [
            {
              "ruleId": "failed_tool_retry",
              "severity": "high",
              "scope": {
                "repository": "swigerb/copilot-ledger",
                "branch": "feature/ledger",
                "pullRequest": 42,
                "agent": "explore",
                "model": null,
                "traceId": "52525252525252525252525252525252",
                "conversationId": "conv-r",
                "tool": "bash",
                "toolOutcome": "failure"
              },
              "windowStart": "2026-11-21T15:00:20.0000000+00:00",
              "windowEnd": "2026-11-21T15:01:20.0000000+00:00",
              "metrics": [
                {
                  "name": "failures",
                  "value": 4,
                  "unit": "calls"
                },
                {
                  "name": "requests",
                  "value": 3,
                  "unit": "requests"
                },
                {
                  "name": "breakerChurnFailures",
                  "value": 5,
                  "unit": "calls"
                }
              ],
              "estimatedImpactUsd": 0.0129,
              "recommendationKey": "failed_tool_retry"
            },
            {
              "ruleId": "premium_model_trivial_turn",
              "severity": "warning",
              "scope": {
                "repository": "swigerb/copilot-ledger",
                "branch": "main",
                "pullRequest": null,
                "agent": "(none)",
                "model": "claude-opus-5.5",
                "traceId": null,
                "conversationId": null,
                "tool": null,
                "toolOutcome": null
              },
              "windowStart": "2026-11-21T14:00:00.0000000+00:00",
              "windowEnd": "2026-11-21T14:22:02.0000000+00:00",
              "metrics": [
                {
                  "name": "requests",
                  "value": 12,
                  "unit": "requests"
                },
                {
                  "name": "outputTokens",
                  "value": 1440,
                  "unit": "tokens"
                },
                {
                  "name": "modelOutputRatePer1M",
                  "value": 20,
                  "unit": "usd_per_1m_tokens"
                },
                {
                  "name": "topQuartileOutputRatePer1M",
                  "value": 15,
                  "unit": "usd_per_1m_tokens"
                }
              ],
              "estimatedImpactUsd": 0.3276,
              "recommendationKey": "premium_model_trivial_turn"
            },
            {
              "ruleId": "cache_miss_streak",
              "severity": "warning",
              "scope": {
                "repository": "swigerb/copilot-ledger",
                "branch": "feature/ledger",
                "pullRequest": 42,
                "agent": "(none)",
                "model": "mai-code-2",
                "traceId": "4d4d4d4d4d4d4d4d4d4d4d4d4d4d4d4d",
                "conversationId": "conv-m",
                "tool": null,
                "toolOutcome": null
              },
              "windowStart": "2026-11-21T18:00:00.0000000+00:00",
              "windowEnd": "2026-11-21T18:05:02.0000000+00:00",
              "metrics": [
                {
                  "name": "requests",
                  "value": 6,
                  "unit": "requests"
                },
                {
                  "name": "inputTokens",
                  "value": 240000,
                  "unit": "tokens"
                },
                {
                  "name": "cacheReadInputTokens",
                  "value": 0,
                  "unit": "tokens"
                },
                {
                  "name": "uncachedInputTokens",
                  "value": 240000,
                  "unit": "tokens"
                },
                {
                  "name": "cacheHitRatio",
                  "value": 0,
                  "unit": "ratio"
                }
              ],
              "estimatedImpactUsd": 0.114,
              "recommendationKey": "cache_miss_streak"
            },
            {
              "ruleId": "context_bloat",
              "severity": "info",
              "scope": {
                "repository": "swigerb/copilot-ledger",
                "branch": "feature/ledger",
                "pullRequest": 42,
                "agent": "(none)",
                "model": "gpt-6.2-sol",
                "traceId": "42424242424242424242424242424242",
                "conversationId": "conv-b",
                "tool": null,
                "toolOutcome": null
              },
              "windowStart": "2026-11-21T17:00:00.0000000+00:00",
              "windowEnd": "2026-11-21T17:00:02.0000000+00:00",
              "metrics": [
                {
                  "name": "requests",
                  "value": 1,
                  "unit": "requests"
                },
                {
                  "name": "inputTokens",
                  "value": 260000,
                  "unit": "tokens"
                },
                {
                  "name": "outputTokens",
                  "value": 1000,
                  "unit": "tokens"
                },
                {
                  "name": "inputOutputRatio",
                  "value": 260,
                  "unit": "ratio"
                }
              ],
              "estimatedImpactUsd": null,
              "recommendationKey": "context_bloat"
            }
          ]
        }
        """.ReplaceLineEndings("\n");

    // The output of gh ledger audit, without its final line break.
    public static readonly string Table = """
        Audit of all repositories (last 7 days)
        4 findings: 1 high, 2 warning, 1 info.
        By rule: failed_tool_retry 1, context_bloat 1, cache_miss_streak 1, premium_model_trivial_turn 1.

        Severity  Rule                        Scope                                                         Window                  Key metrics                                          Est. impact
        High      failed_tool_retry           swigerb/copilot-ledger @ feature/ledger, explore, bash        2026-11-21 10:00-10:01  4 failures in a row                                        $0.01
        Warning   premium_model_trivial_turn  swigerb/copilot-ledger @ main, (none), claude-opus-5.5        2026-11-21 09:00-09:22  12 tool-free requests of 200 or fewer output tokens        $0.33
        Warning   cache_miss_streak           swigerb/copilot-ledger @ feature/ledger, (none), mai-code-2   2026-11-21 13:00-13:05  6 requests, 0.0% cache hits, 240,000 uncached              $0.11
        Info      context_bloat               swigerb/copilot-ledger @ feature/ledger, (none), gpt-6.2-sol  2026-11-21 12:00        260,000 in, 1,000 out (260:1)                                  -

        Requests: 29 analyzed (26 live, 2 from Copilot session logs, 1 from Codex rollouts).
        Tool calls: 7 analyzed.
        Impacts: billed USD (GitHub AI credits and Foundry BYOK) at rates 2026-11-23, economy model mai-code-2. Findings can overlap, so don't add their impacts up.
        Cache counters: unknown for 3 requests (3 absent, 0 recorded before gh-ledger kept counter states); cache_miss_streak skipped them.
        Agent invocations: unknown for 3 requests and 0 tool calls; the sequence rules skipped them.
        Tool counts: unknown for 2 requests; premium_model_trivial_turn left them out.
        Tool history: insufficient: the ledger's tool history starts at 2026-11-20 07:00; 1 imported request has no tool-call count in its session log, so the tool rules can't see those calls.
        Unpriced: 1 request has no billed price, so impacts that include it are unknown.
        Rules: all 8.

        Recommendations:
          failed_tool_retry: Fix the cause of the failing command, such as a missing dependency or a wrong path, before the agent retries it, because every retry costs another model request.
          premium_model_trivial_turn: Consider a cheaper model or a local model for these short requests without tool calls; they're only candidates, because a short answer can still need a strong model.
          cache_miss_streak: Keep the start of each prompt stable, such as instructions, tool definitions, and earlier messages, so the prompt cache can serve it, and avoid switching models in the middle of a task.
          context_bloat: Send less context with each request: attach smaller files and tool results, trim long instructions, or start a new session for a new task.
        """.ReplaceLineEndings("\n");

    // The report gh ledger audit writes for the seeded ledger, with minSeverity and limit.
    public static async Task<AuditReport> ReportAsync(
        SqliteLedgerStore store,
        AuditSeverity minSeverity = AuditSeverity.Info,
        int? limit = 20)
    {
        AuditInput input;
        await using (LedgerExportSnapshot snapshot = await store.OpenExportSnapshotAsync())
        {
            input = await snapshot.GetAuditInputAsync(Window, AuditSeeds.GeneratedAt);
        }

        return AuditAnalyzer.Report(
            input with
            {
                Pricing = AuditFixtures.Pricing,
                EconomyModel = "mai-code-2",
                TimeZone = ManualTimeProvider.UtcMinus5,
            },
            AuditSeeds.GeneratedAt,
            new AuditReportFilter { Since = "7d", Period = "last 7 days", FromUtc = Window.From, ToUtc = AuditSeeds.GeneratedAt },
            minSeverity,
            limit);
    }
}
