namespace CopilotLedger.Core.Telemetry;

// Where JsonRateCardCalculator finds a request's billing channel. Live spans keep Endpoint; only an import sets Provider.
public enum BillingSource
{
    // server.address, then gen_ai.provider.name, then a known local model. A request without an endpoint is GitHub's.
    Endpoint,

    // The provider that billed the request, ProviderName: local or Foundry BYOK when the card lists it under
    // localOffload.providerNames or foundryByok.providerNames, and otherwise another provider (OtherProvider), never GitHub.
    Provider,
}
