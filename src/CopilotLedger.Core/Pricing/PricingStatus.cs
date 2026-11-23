namespace CopilotLedger.Core.Pricing;

public enum PricingStatus
{
    Priced,                 // GitHubAiCredits or FoundryByok with a matching rate
    LocalOffload,           // Local: billed 0, savings priced at the baseline model
    ByokUnpriced,           // FoundryByok endpoint, but no BYOK rate for the model
    UnknownModel,           // no model on the span, or neither reported model is in the channel's table
    NoRateForDate,          // model known, but no window covers StartTime
    UnrecognizedEndpoint,   // host isn't local, Foundry, or GitHub
    InvalidUsage,           // a negative token count, or cache read + cache write > input
    NotBillableOperation,   // not an inference operation (for example invoke_agent)
}
