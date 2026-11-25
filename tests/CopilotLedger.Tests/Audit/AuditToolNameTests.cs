using CopilotLedger.Core.Audit;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Audit;

public sealed class AuditToolNameTests
{
    private const string LedgerHash =
        "fe14010b4fe83303852f0467c919ef9a7ca089b91e96e3aad7d426dd87079297/eb20c46435d60871da0eb97ce23417c04835e272725c59a3a649c7ce8e521d97";

    private const string ExecJs =
        "10be6beef496b89dae799c37bd04e5eba2a6264a0a4d605f5661b50d7dc4d1ea/8453dc19fda259c463d7aaffb07becf26b7ce2cfc690a2b0a957e201653dc85a";

    [Fact]
    public void MD6_WithToolNames_ChangesOnlyTheToolOfEachFinding()
    {
        AuditToolCall[] calls =
        [
            .. Enumerable.Range(1, 9).Select(number =>
            {
                string name = number <= 3 ? ExecJs : number <= 6 ? LedgerHash : "view";
                return AuditFixtures.Tool(
                    number,
                    TimeSpan.FromSeconds(10 * number),
                    name,
                    exitCode: 1,
                    statusCode: 2);
            }),
        ];
        AuditInput input = AuditFixtures.Input([AuditFixtures.Span(1)], calls) with
        {
            Rules = [AuditRuleIds.FailedToolRetry],
        };
        AuditReport report = AuditAnalyzer.Report(
            input,
            AuditFixtures.Start,
            new AuditReportFilter
            {
                Since = "7d",
                Period = "last 7 days",
                ToUtc = AuditFixtures.Start,
            },
            AuditSeverity.Info,
            null);
        AuditReport resolved = report.WithToolNames(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [ExecJs] = "computer-use-exec_js",
            });

        Assert.Equal([ExecJs, LedgerHash, "view"], report.Findings.Select(finding => finding.Scope.Tool));
        Assert.Equal(
            ["computer-use-exec_js", "MCP tool fe14010b/eb20c464", "view"],
            resolved.Findings.Select(finding => finding.Scope.Tool));
        Assert.Equal(
            report.Findings.Select(finding =>
                finding with { Scope = finding.Scope with { Tool = null } }),
            resolved.Findings.Select(finding =>
                finding with { Scope = finding.Scope with { Tool = null } }));
        Assert.Equal(report.Shown.Count, resolved.Shown.Count);
        Assert.Throws<ArgumentNullException>(() => report.WithToolNames(null!));
    }
}
