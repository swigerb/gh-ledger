using CopilotLedger.Storage;

namespace CopilotLedger.Cli.Reporting;

internal sealed record Period(string Label, DateTimeOffset? From)
{
    // LedgerPeriod.TryParse at the context's clock, which the MCP server's since argument shares.
    public static bool TryParse(
        string? value,
        CliContext context,
        out Period period)
    {
        if (LedgerPeriod.TryParse(value, context.TimeProvider, out LedgerPeriod? parsed))
        {
            period = new Period(parsed.Label, parsed.From);
            return true;
        }

        period = null!;
        return false;
    }
}
