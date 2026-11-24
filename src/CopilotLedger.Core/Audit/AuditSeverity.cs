namespace CopilotLedger.Core.Audit;

// How much a finding matters, least first.
public enum AuditSeverity
{
    Info,
    Warning,
    High,
}

public static class AuditSeverities
{
    // info, warning, or high. Throws ArgumentOutOfRangeException for an undefined value.
    public static string ToName(AuditSeverity severity) => severity switch
    {
        AuditSeverity.Info => "info",
        AuditSeverity.Warning => "warning",
        AuditSeverity.High => "high",
        _ => throw new ArgumentOutOfRangeException(nameof(severity)),
    };

    // Ordinal: exactly info, warning, or high. False for anything else, including null.
    public static bool TryParse(string? name, out AuditSeverity severity)
    {
        switch (name)
        {
            case "info":
                severity = AuditSeverity.Info;
                return true;
            case "warning":
                severity = AuditSeverity.Warning;
                return true;
            case "high":
                severity = AuditSeverity.High;
                return true;
            default:
                severity = AuditSeverity.Info;
                return false;
        }
    }
}
