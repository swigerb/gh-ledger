using CopilotLedger.Core.Breaker;

namespace CopilotLedger.Core.Audit;

public enum AuditToolOutcome
{
    Success,
    Failure,
    Unknown,
}

public static class AuditToolOutcomes
{
    // success, failure, or unknown. Throws ArgumentOutOfRangeException for an undefined value.
    public static string ToName(AuditToolOutcome outcome) => outcome switch
    {
        AuditToolOutcome.Success => "success",
        AuditToolOutcome.Failure => "failure",
        AuditToolOutcome.Unknown => "unknown",
        _ => throw new ArgumentOutOfRangeException(nameof(outcome)),
    };

    // The breaker's classification, refined. A failure is ToolCallEvent.IsFailure: an exit code other than 0, or status 2.
    // A success is ToolCallEvent.IsSuccessfulCommand (exit code 0, status not 2), or no exit code with status 1 (OK).
    // Anything else is unknown. An imported call was stored only when its log recorded that it completed, as status 2 for
    // a failure and 0 otherwise, so it's a success unless it's a failure.
    public static AuditToolOutcome Classify(long? exitCode, int statusCode, bool imported)
    {
        ToolCallEvent call = new(string.Empty, default, null, exitCode, statusCode);
        if (call.IsFailure)
        {
            return AuditToolOutcome.Failure;
        }

        return call.IsSuccessfulCommand || (exitCode is null && statusCode == 1) || imported
            ? AuditToolOutcome.Success
            : AuditToolOutcome.Unknown;
    }
}
