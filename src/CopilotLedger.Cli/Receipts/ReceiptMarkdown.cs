using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using CopilotLedger.Cli.Reporting;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Storage;

namespace CopilotLedger.Cli.Receipts;

// Renders a receipt as the Markdown file that export-pr writes and the action posts. Every line ends with "\n" on every OS.
public static class ReceiptMarkdown
{
    public const string Marker = "<!-- copilot-ledger-receipt -->";
    public const string ReceiptsDirectory = ".github/copilot-ledger/receipts";
    public const int MaximumBytes = 65536;   // as in receipt.ps1 in swigerb/copilot-ledger-action; keeps a receipt within GitHub's 65,536-character comment limit

    internal static string ToJson(PullRequestReceipt receipt)
    {
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(
            stream,
            new JsonWriterOptions
            {
                Indented = true,
                NewLine = "\n",
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", 1);
            writer.WriteString("repository", receipt.Repository);
            writer.WriteString("branch", receipt.Branch);
            writer.WriteString("commit", receipt.Commit);
            LedgerFormat.WriteTime(writer, "generatedAt", receipt.GeneratedAt);
            writer.WriteString("generator", $"gh-ledger {GhLedgerCli.Version}");
            writer.WritePropertyName("totals");
            writer.WriteStartObject();
            LedgerFormat.WriteTotals(writer, receipt.Totals);
            writer.WriteEndObject();
            writer.WriteNumber("prompts", receipt.Prompts);
            LedgerFormat.WriteMoney(writer, "maxPromptCredits", receipt.MaxPromptCredits);
            writer.WriteNumber("breakerTrips", receipt.BreakerTrips);
            writer.WriteStartArray("models");
            foreach (LedgerRollupRow row in receipt.Models)
            {
                writer.WriteStartObject();
                writer.WriteString("channel", ChannelId(row.Channel!.Value));
                writer.WriteString("model", row.Model);
                LedgerFormat.WriteTotals(writer, row);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    internal static string ToMarkdown(PullRequestReceipt receipt)
    {
        LedgerRollupRow totals = receipt.Totals;
        LedgerRollupRow[] github = Rows(receipt, BillingChannel.GitHubAiCredits);
        LedgerRollupRow[] byok = Rows(receipt, BillingChannel.FoundryByok);
        LedgerRollupRow[] local = Rows(receipt, BillingChannel.Local);
        LedgerRollupRow[] unpriced = Rows(receipt, BillingChannel.Unpriced);
        StringBuilder text = new();
        text.Append(Marker).Append('\n');
        text.Append("### CopilotLedger receipt for ").Append(Code(receipt.Branch)).Append("\n\n");

        text.Append("- **GitHub AI Credits:** ");
        if (github.Length == 0)
        {
            text.Append("none\n");
        }
        else
        {
            text.Append(LedgerFormat.Credits(totals.AiCredits)).Append(" credits (")
                .Append(LedgerFormat.Usd(totals.AiCreditsUsd)).Append(") across ")
                .Append(Turns(github)).Append(": ")
                .Append(string.Join(", ", github.Select(row => $"{Code(row.Model!)} {LedgerFormat.Credits(row.AiCredits)}")))
                .Append('\n');
        }

        text.Append("- **Foundry BYOK:** ");
        if (byok.Length == 0)
        {
            text.Append("none\n");
        }
        else
        {
            text.Append(LedgerFormat.Usd(totals.FoundryByokUsd)).Append(" across ")
                .Append(Turns(byok)).Append(": ")
                .Append(string.Join(", ", byok.Select(row => $"{Code(row.Model!)} {LedgerFormat.Usd(row.FoundryByokUsd)}")))
                .Append('\n');
        }

        text.Append("- **Local offload:** ");
        if (local.Length == 0)
        {
            text.Append("none\n");
        }
        else
        {
            text.Append(LedgerFormat.Count(local.Sum(row => row.InputTokens + row.OutputTokens))).Append(" tokens across ")
                .Append(Turns(local)).Append(" ($0.00 billed • ")
                .Append(LedgerFormat.Usd(totals.LocalSavedUsd)).Append(" cloud spend avoided): ")
                .Append(string.Join(", ", local.Select(row => $"{Code(row.Model!)} {LedgerFormat.Count(row.InputTokens + row.OutputTokens)}")))
                .Append('\n');
        }

        if (unpriced.Length > 0)
        {
            text.Append("- **Not priced:** ").Append(Plural(unpriced.Sum(row => row.SpanCount), "turn", "turns")).Append(": ")
                .Append(string.Join(", ", unpriced.Select(row => Code(row.Model!))))
                .Append('\n');
        }

        text.Append("- **Circuit breaker:** ").Append(Plural(receipt.BreakerTrips, "trip", "trips"))
            .Append(" • max single-prompt burn: ").Append(LedgerFormat.Credits(receipt.MaxPromptCredits))
            .Append(" credits\n\n");

        text.Append("<details>\n<summary>Details and data</summary>\n\n");
        if (receipt.Models.Count == 0)
        {
            text.Append("No spans are recorded for this branch.\n\n");
        }
        else
        {
            text.Append("| Billing | Model | Turns | Unpriced | Input tokens | Output tokens | Cost | Cloud spend avoided |\n");
            text.Append("| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |\n");
            foreach (LedgerRollupRow row in receipt.Models)
            {
                text.Append("| ").Append(ChannelLabel(row.Channel!.Value))
                    .Append(" | ").Append(Code(row.Model!).Replace("|", "\\|", StringComparison.Ordinal))
                    .Append(" | ").Append(LedgerFormat.Count(row.SpanCount))
                    .Append(" | ").Append(LedgerFormat.Count(row.UnpricedSpanCount))
                    .Append(" | ").Append(LedgerFormat.Count(row.InputTokens))
                    .Append(" | ").Append(LedgerFormat.Count(row.OutputTokens))
                    .Append(" | ").Append(Cost(row))
                    .Append(" | ").Append(LedgerFormat.Usd(row.LocalSavedUsd))
                    .Append(" |\n");
            }

            text.Append('\n');
        }

        text.Append("Repository ").Append(Code(receipt.Repository))
            .Append(" • ").Append(Plural(receipt.Prompts, "prompt", "prompts"))
            .Append(" • commit ").Append(Code(receipt.Commit[..Math.Min(12, receipt.Commit.Length)]))
            .Append(" • generated ")
            .Append(receipt.GeneratedAt.ToUniversalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture))
            .Append(" UTC by gh-ledger ").Append(GhLedgerCli.Version).Append("\n\n");
        text.Append("To check these totals, run ").Append(Code("gh ledger branch --since all --json"))
            .Append(" in the repository and compare the ").Append(Code(receipt.Branch)).Append(" row.\n\n");
        text.Append("```json\n").Append(ToJson(receipt)).Append("\n```\n\n</details>\n");
        return text.ToString();
    }

    // <name>-<hash>.md. name is branch with every character other than A-Z, a-z, 0-9, '.', '_', and '-' replaced by '-',
    // cut to its first 64 characters. hash is the first 8 lowercase hex digits of the SHA-256 of branch's UTF-8 bytes,
    // so branches that differ only in replaced characters or letter case still get different files.
    public static string FileName(string branch)
    {
        StringBuilder name = new(branch.Length);
        foreach (char character in branch)
        {
            name.Append(char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-'
                ? character
                : '-');
        }

        if (name.Length > 64)
        {
            name.Length = 64;
        }

        string hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(branch)))[..8];
        return $"{name}-{hash}.md";
    }

    // A CommonMark code span that shows value literally. Control characters, U+2028, and U+2029 become U+FFFD; the fence is
    // one backtick longer than the longest run of backticks in value; and a space pads both ends when value starts or ends
    // with a backtick or a space. GitHub doesn't turn @mentions, #references, or URLs inside a code span into links.
    // value is never empty.
    public static string Code(string value)
    {
        StringBuilder content = new(value.Length);
        int run = 0;
        int longestRun = 0;
        foreach (char character in value)
        {
            char shown = char.IsControl(character) || character is '\u2028' or '\u2029'
                ? '\uFFFD'
                : character;
            run = shown == '`' ? run + 1 : 0;
            longestRun = Math.Max(longestRun, run);
            content.Append(shown);
        }

        string fence = new('`', longestRun + 1);
        bool pad = content[0] is '`' or ' ' || content[content.Length - 1] is '`' or ' ';
        return pad ? $"{fence} {content} {fence}" : $"{fence}{content}{fence}";
    }

    private static string ChannelId(BillingChannel channel) => channel switch
    {
        BillingChannel.GitHubAiCredits => "github_ai_credits",
        BillingChannel.FoundryByok => "foundry_byok",
        BillingChannel.Local => "local",
        BillingChannel.Unpriced => "unpriced",
        _ => throw new UnreachableException(),
    };

    private static string ChannelLabel(BillingChannel channel) => channel switch
    {
        BillingChannel.GitHubAiCredits => "GitHub AI Credits",
        BillingChannel.FoundryByok => "Foundry BYOK",
        BillingChannel.Local => "Local",
        BillingChannel.Unpriced => "Not priced",
        _ => throw new UnreachableException(),
    };

    private static string Cost(LedgerRollupRow row) => row.Channel switch
    {
        BillingChannel.GitHubAiCredits =>
            $"{LedgerFormat.Credits(row.AiCredits)} credits ({LedgerFormat.Usd(row.AiCreditsUsd)})",
        BillingChannel.FoundryByok => LedgerFormat.Usd(row.FoundryByokUsd),
        BillingChannel.Local or BillingChannel.Unpriced => "$0.00",
        _ => throw new UnreachableException(),
    };

    private static LedgerRollupRow[] Rows(PullRequestReceipt receipt, BillingChannel channel) =>
        receipt.Models.Where(row => row.Channel == channel).ToArray();

    private static string Turns(IReadOnlyList<LedgerRollupRow> rows)
    {
        long unpriced = rows.Sum(row => row.UnpricedSpanCount);
        string turns = Plural(rows.Sum(row => row.SpanCount), "turn", "turns");
        return unpriced == 0 ? turns : $"{turns}, {LedgerFormat.Count(unpriced)} unpriced";
    }

    private static string Plural(long count, string singular, string plural) =>
        $"{LedgerFormat.Count(count)} {(count == 1 ? singular : plural)}";
}
