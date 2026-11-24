namespace CopilotLedger.Core.Pricing;

public enum ReportedChargeState
{
    Absent = 0,
    Valid,
    Malformed,
}

public readonly record struct ReportedCharge(ReportedChargeState State, long NanoAiu)
{
    public static ReportedCharge Absent => default;

    public static ReportedCharge Malformed => new(ReportedChargeState.Malformed, 0);

    public static ReportedCharge Valid(long nanoAiu)
    {
        NanoAiuContracts.Validate(nanoAiu);
        return new(ReportedChargeState.Valid, nanoAiu);
    }
}
