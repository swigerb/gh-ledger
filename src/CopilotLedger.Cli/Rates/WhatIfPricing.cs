using System.CommandLine;
using System.Text.Json;
using CopilotLedger.Core.Pricing;

namespace CopilotLedger.Cli.Rates;

// A --pricing file: a complete rate card whose API list prices replace the recorded API-equivalent results in a report. Path
// is the file's full path. Nothing else in the report changes, and nothing is saved.
internal sealed record WhatIfPricing(string Path, JsonRateCardCalculator Calculator)
{
    // "what-if <path> (ratesVersion <version>)"; with fileNameOnly, the path is the file's name, as receipts show it.
    public string Label(bool fileNameOnly = false) =>
        $"what-if {(fileNameOnly ? System.IO.Path.GetFileName(Path) : Path)} (ratesVersion {Calculator.RatesVersion})";

    // Reads file, relative to the working directory, as a complete rate card with API list prices (schemaVersion 2), on its
    // own: neither the overlay nor the downloaded rates apply. Returns null after writing "Invalid pricing file: <problem>" to
    // context.Error when the file can't be used.
    public static WhatIfPricing? Load(CliContext context, string file)
    {
        string path = System.IO.Path.GetFullPath(file, context.WorkingDirectory);
        string? problem = null;
        JsonRateCardCalculator? calculator = null;
        if (!File.Exists(path))
        {
            problem = $"{path} does not exist.";
        }
        else
        {
            try
            {
                calculator = JsonRateCardCalculator.Parse(File.ReadAllText(path));
                if (calculator.SchemaVersion < 2)
                {
                    problem = $"{path}: schemaVersion: must be 2, so that the file has API list prices";
                }
            }
            catch (InvalidDataException ex)
            {
                problem = $"{path}: {ex.Message}";
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                problem = $"{path}: could not read it: {ex.Message}";
            }
        }

        if (problem is not null)
        {
            context.Error.WriteLine($"Invalid pricing file: {problem}");
            return null;
        }

        return new WhatIfPricing(path, calculator!);
    }

    // "pricingSource": kind whatIf, the path (its file name with fileNameOnly), the ratesVersion, and the apiRateCardSha256
    // of the file's API list prices.
    public void Write(Utf8JsonWriter writer, bool fileNameOnly = false)
    {
        writer.WritePropertyName("pricingSource");
        writer.WriteStartObject();
        writer.WriteString("kind", "whatIf");
        writer.WriteString("path", fileNameOnly ? System.IO.Path.GetFileName(Path) : Path);
        writer.WriteString("ratesVersion", Calculator.RatesVersion);
        writer.WriteString("apiRateCardSha256", Calculator.ApiRateCardSha256);
        writer.WriteEndObject();
    }

    // The --pricing option of status, branch, agents, top-prompts, and export-pr.
    public static Option<string?> CreateOption() =>
        new("--pricing")
        {
            Description =
                "Price the API-equivalent amounts with this complete rate card instead. Nothing else changes, and nothing is saved.",
        };
}
