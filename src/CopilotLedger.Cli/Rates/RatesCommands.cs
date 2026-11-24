using System.CommandLine;
using System.CommandLine.Parsing;
using System.Text;
using CopilotLedger.Cli.Reporting;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Storage;

namespace CopilotLedger.Cli.Rates;

internal static class RatesCommands
{
    private const string Scaffold = """
        {
          "schemaVersion": 1,
          "notes": [
            "gh-ledger rates overlay. See docs/configuration.md in the gh-ledger repository.",
            "Prices are USD per 1,000,000 tokens, from 0 to 1,000,000, with at most 6 decimal places."
          ],
          "foundryByok": {
            "hostSuffixes": [],
            "models": []
          }
        }
        """;

    public static Command Create(CliContext context)
    {
        Command rates = new("rates", "Create, validate, and update rates.");
        Command init = new("init", "Create a starter rates overlay.");
        Option<bool> force = new("--force")
        {
            Description = "Replace an existing rates overlay.",
        };
        init.Add(force);
        init.SetAction(
            (ParseResult result, CancellationToken cancellationToken) =>
                InitializeAsync(context, result.GetValue(force), cancellationToken));
        rates.Add(init);

        Command validate = new("validate", "Validate a rates overlay, or a --pricing file.");
        Option<string?> file = new("--file")
        {
            Description = "Path to the rates overlay.",
        };
        Option<string?> pricing = new("--pricing")
        {
            Description = "Path to a complete rate card to check as the reports' --pricing checks it.",
        };
        validate.Add(file);
        validate.Add(pricing);
        validate.Validators.Add(result =>
        {
            if (result.GetValue(file) is not null && result.GetValue(pricing) is not null)
            {
                result.AddError("Use either --file or --pricing, not both.");
            }
        });
        validate.SetAction(
            (ParseResult result, CancellationToken _) =>
                Task.FromResult(
                    result.GetValue(pricing) is string pricingFile
                        ? ValidatePricing(context, pricingFile)
                        : Validate(context, result.GetValue(file))));
        rates.Add(validate);

        Command update = new("update", "Download the latest published rates.");
        update.SetAction(
            (ParseResult _, CancellationToken cancellationToken) =>
                RatesUpdate.RunAsync(context, cancellationToken));
        rates.Add(update);

        Command backfill = new(
            "backfill-api-equivalent",
            "Price the spans recorded without API-equivalent prices, at the rates in use.");
        Option<bool> dryRun = new("--dry-run")
        {
            Description = "Show what would be recorded without writing it.",
        };
        backfill.Add(dryRun);
        backfill.SetAction(
            (ParseResult result, CancellationToken cancellationToken) =>
                BackfillAsync(context, result.GetValue(dryRun), cancellationToken));
        rates.Add(backfill);
        return rates;
    }

    private static async Task<int> InitializeAsync(
        CliContext context,
        bool force,
        CancellationToken cancellationToken)
    {
        string path = context.RatesOverlayPath;
        if (!force && File.Exists(path))
        {
            context.Error.WriteLine($"{path} already exists. Use --force to replace it.");
            return 1;
        }

        Directory.CreateDirectory(context.DataDirectory);
        await File.WriteAllTextAsync(
            path,
            Scaffold.ReplaceLineEndings("\n") + "\n",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            cancellationToken);
        context.Out.WriteLine(
            $"Created {path}. Add your prices, then run: gh-ledger rates validate");
        return 0;
    }

    private static int Validate(CliContext context, string? file)
    {
        bool explicitFile = file is not null;
        string path = explicitFile
            ? Path.GetFullPath(file!, context.WorkingDirectory)
            : context.RatesOverlayPath;
        RatesBase rates = RateCardFiles.LoadBase(context);
        if (rates.Warning is not null)
        {
            context.Out.WriteLine($"Warning: {rates.Warning}");
        }

        if (!File.Exists(path))
        {
            if (explicitFile)
            {
                context.Error.WriteLine($"{path} does not exist.");
                return 1;
            }

            context.Out.WriteLine(
                $"No rates overlay at {path}; using the {(rates.DownloadedJson is null ? "built-in" : "downloaded")} rates ({rates.Version}).");
            return 0;
        }

        try
        {
            JsonRateCardCalculator calculator = RateCardFiles.Load(rates, path);
            context.Out.WriteLine(
                $"{path} is valid. Rates version: {calculator.RatesVersion}.");
            return 0;
        }
        catch (InvalidDataException ex)
        {
            context.Error.WriteLine($"Invalid rates overlay: {ex.Message}");
            return 1;
        }
    }

    // Checks a --pricing file with the rules and the messages of the reports' --pricing.
    private static int ValidatePricing(CliContext context, string file)
    {
        WhatIfPricing? pricing = WhatIfPricing.Load(context, file);
        if (pricing is null)
        {
            return 1;
        }

        context.Out.WriteLine(
            $"{pricing.Path} is a valid pricing file. Rates version: {pricing.Calculator.RatesVersion}.");
        return 0;
    }

    // Prices the legacy spans with the rates the daemon would load: the base rates and the overlay.
    private static async Task<int> BackfillAsync(
        CliContext context,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        RatesBase rates = RateCardFiles.LoadBase(context);
        if (rates.Warning is not null)
        {
            context.Out.WriteLine($"Warning: {rates.Warning}");
        }

        JsonRateCardCalculator calculator;
        try
        {
            calculator = RateCardFiles.Load(rates, RateCardFiles.OverlayPath(context));
        }
        catch (InvalidDataException ex)
        {
            context.Error.WriteLine($"Invalid rates overlay: {ex.Message}");
            return 1;
        }

        SqliteLedgerStore store = new(context.DatabasePath, context.TimeProvider);
        await store.InitializeAsync(cancellationToken);
        ApiEquivalentBackfill backfill =
            await store.BackfillApiEquivalentAsync(calculator, dryRun, cancellationToken);
        if (backfill.SpanCount == 0)
        {
            context.Out.WriteLine("Every span has an API-equivalent result; nothing to backfill.");
            return 0;
        }

        context.Out.WriteLine(
            $"{LedgerFormat.Count(backfill.SpanCount)} spans have no API-equivalent result. With rates {calculator.RatesVersion}:");
        context.Out.WriteLine(
            $"  Priced:          {LedgerFormat.Count(backfill.PricedSpanCount)} ({LedgerFormat.Usd(backfill.PricedUsd)})");
        context.Out.WriteLine($"  Unpriced:        {LedgerFormat.Count(backfill.UnpricedSpanCount)}");
        context.Out.WriteLine($"  Not applicable:  {LedgerFormat.Count(backfill.NotApplicableSpanCount)}");
        if (backfill.OutOfRangeSpanCount > 0)
        {
            context.Out.WriteLine(
                $"  Out of range:    {LedgerFormat.Count(backfill.OutOfRangeSpanCount)}, left without a result");
        }

        context.Out.WriteLine(
            dryRun
                ? "Dry run: nothing was written."
                : $"Recorded {LedgerFormat.Count(backfill.UpdatedSpanCount)} results. Billed AI credits, BYOK USD, and local savings didn't change.");
        return 0;
    }
}
