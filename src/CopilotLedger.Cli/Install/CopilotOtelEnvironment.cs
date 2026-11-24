namespace CopilotLedger.Cli.Install;

internal static class CopilotOtelEnvironment
{
    private static readonly string[] CertificateVariables =
    [
        "OTEL_EXPORTER_OTLP_CERTIFICATE",
        "OTEL_EXPORTER_OTLP_CLIENT_CERTIFICATE",
        "OTEL_EXPORTER_OTLP_CLIENT_KEY",
        "OTEL_EXPORTER_OTLP_TRACES_CERTIFICATE",
        "OTEL_EXPORTER_OTLP_TRACES_CLIENT_CERTIFICATE",
        "OTEL_EXPORTER_OTLP_TRACES_CLIENT_KEY",
    ];

    public static IReadOnlyList<string> GetWarnings(Func<string, string?> lookup)
    {
        List<string> warnings = [];
        string? endpoint = Value(lookup, "OTEL_EXPORTER_OTLP_ENDPOINT");
        if (endpoint is not null && !IsLocalEndpoint(endpoint))
        {
            warnings.Add(
                $"OTEL_EXPORTER_OTLP_ENDPOINT is {endpoint}; Copilot exports there instead of to gh-ledger at http://localhost:4318.");
        }

        if (Value(lookup, "COPILOT_OTEL_FILE_EXPORTER_PATH") is not null)
        {
            warnings.Add(
                "COPILOT_OTEL_FILE_EXPORTER_PATH is set; Copilot writes telemetry to that file instead of exporting it to gh-ledger.");
        }

        string? exporter = Value(lookup, "COPILOT_OTEL_EXPORTER_TYPE");
        if (exporter is not null &&
            !exporter.Equals("otlp-http", StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add(
                $"COPILOT_OTEL_EXPORTER_TYPE is {exporter}; remove it or set it to otlp-http.");
        }

        foreach (string name in CertificateVariables)
        {
            if (Value(lookup, name) is not null)
            {
                warnings.Add(
                    $"{name} is set; Copilot turns off export to http:// endpoints while TLS settings are set.");
            }
        }

        AddProtocolWarning(warnings, lookup, "OTEL_EXPORTER_OTLP_PROTOCOL");
        AddProtocolWarning(warnings, lookup, "OTEL_EXPORTER_OTLP_TRACES_PROTOCOL");
        return warnings;
    }

    public static bool IsLocalEndpoint(string value)
    {
        string endpoint = value.Trim().TrimEnd('/');
        return endpoint.Equals(
                "http://localhost:4318",
                StringComparison.OrdinalIgnoreCase) ||
            endpoint.Equals(
                "http://127.0.0.1:4318",
                StringComparison.OrdinalIgnoreCase);
    }

    private static void AddProtocolWarning(
        List<string> warnings,
        Func<string, string?> lookup,
        string name)
    {
        string? value = Value(lookup, name);
        if (value?.Equals("grpc", StringComparison.OrdinalIgnoreCase) == true)
        {
            warnings.Add(
                $"{name} is grpc; gh-ledger accepts only http/protobuf and http/json.");
        }
    }

    private static string? Value(Func<string, string?> lookup, string name)
    {
        string? value = lookup(name);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
