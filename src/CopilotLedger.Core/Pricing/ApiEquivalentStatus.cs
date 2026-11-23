namespace CopilotLedger.Core.Pricing;

// How the API-equivalent (retail) lens judged a request.
public enum ApiEquivalentStatus
{
    Priced,          // an apiEquivalent model and window priced it
    Unpriced,        // no apiEquivalent model, or no window for the request's start time
    NotApplicable,   // not an inference operation, or inconsistent token counts (InvalidUsage)
}
