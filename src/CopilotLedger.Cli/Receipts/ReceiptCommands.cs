using System.CommandLine;
using System.CommandLine.Parsing;
using System.Text;
using CopilotLedger.Core.Git;
using CopilotLedger.Core.Pricing;
using CopilotLedger.Storage;

namespace CopilotLedger.Cli.Receipts;

internal static class ReceiptCommands
{
    private static readonly LedgerRollupRow NoSpend = new()
    {
        SpanCount = 0,
        UnpricedSpanCount = 0,
        InputTokens = 0,
        OutputTokens = 0,
        CacheReadInputTokens = 0,
        CacheWriteInputTokens = 0,
        CacheReportedSpanCount = 0,
        ReportedInputTokens = 0,
        ReportedCacheReadInputTokens = 0,
        ReportedUncachedInputTokens = 0,
        AiCredits = 0m,
        AiCreditsUsd = 0m,
        FoundryByokUsd = 0m,
        LocalSavedUsd = 0m,
        ApiEquivalentUsd = 0m,
        ApiEquivalentUnpricedSpanCount = 0,
        ApiEquivalentEstimatedSpanCount = 0,
    };

    public static Command Create(CliContext context)
    {
        Command command = new("export-pr", "Write the current branch's AI spend receipt for its pull request.");
        Option<bool> stdout = new("--stdout")
        {
            Description = "Write the Markdown receipt to standard output instead of the receipt file.",
        };
        Option<bool> json = new("--json")
        {
            Description = "Write the receipt's JSON data to standard output instead of the receipt file.",
        };
        command.Add(stdout);
        command.Add(json);
        command.Validators.Add(result =>
        {
            if (result.GetValue(stdout) && result.GetValue(json))
            {
                result.AddError("Use either --stdout or --json, not both.");
            }
        });
        command.SetAction(
            (ParseResult result, CancellationToken cancellationToken) =>
                ExportAsync(
                    context,
                    result.GetValue(stdout),
                    result.GetValue(json),
                    cancellationToken));
        return command;
    }

    private static async Task<int> ExportAsync(
        CliContext context,
        bool stdout,
        bool json,
        CancellationToken cancellationToken)
    {
        GitWorkspaceContext? git =
            new FileSystemGitContextResolver().Resolve(context.WorkingDirectory);
        if (git is null)
        {
            context.Error.WriteLine($"{context.WorkingDirectory} isn't in a Git repository.");
            return 1;
        }

        if (git.BranchName is null)
        {
            context.Error.WriteLine(
                "HEAD is detached. Check out the pull request's branch, then run export-pr again.");
            return 1;
        }

        if (git.HeadCommitSha is null)
        {
            context.Error.WriteLine($"{git.BranchName} has no commits yet.");
            return 1;
        }

        if (git.RepositorySlug is null)
        {
            context.Error.WriteLine(
                $"export-pr needs a GitHub remote named origin, and {git.RepositoryRoot} has none.");
            return 1;
        }

        SqliteLedgerStore store = new(context.DatabasePath, context.TimeProvider);
        await store.InitializeAsync(cancellationToken);
        LedgerFilter filter = new()
        {
            Repository = git.RepositorySlug,
            Branch = git.BranchName,
        };
        IReadOnlyList<LedgerRollupRow> branchRows = await store.GetRollupAsync(
            LedgerGrouping.Branch,
            filter,
            cancellationToken);
        IReadOnlyList<PromptChainRow> costliest = await store.GetTopPromptChainsAsync(
            filter,
            1,
            cancellationToken);
        PullRequestReceipt receipt = new()
        {
            Repository = git.RepositorySlug,
            Branch = git.BranchName,
            Commit = git.HeadCommitSha,
            GeneratedAt = context.TimeProvider.GetUtcNow(),
            Totals = branchRows.Count == 0 ? NoSpend : branchRows[0],
            Models = await store.GetRollupAsync(
                LedgerGrouping.ChannelModel,
                filter,
                cancellationToken),
            Prompts = await store.GetPromptCountAsync(filter, cancellationToken),
            MaxPromptCredits = costliest.Count == 0
                ? 0m
                : (costliest[0].AiCreditsUsd + costliest[0].FoundryByokUsd) / AiCreditMath.UsdPerAiCredit,
            BreakerTrips = await store.CountBreakerTripsAsync(
                git.RepositorySlug,
                git.BranchName,
                cancellationToken),
        };

        if (json)
        {
            context.Out.Write(ReceiptMarkdown.ToJson(receipt) + "\n");
            return 0;
        }

        string markdown = ReceiptMarkdown.ToMarkdown(receipt);
        int size = Encoding.UTF8.GetByteCount(markdown);
        if (size > ReceiptMarkdown.MaximumBytes)
        {
            context.Error.WriteLine(
                $"The receipt for {git.BranchName} is {size} bytes; a receipt can be at most {ReceiptMarkdown.MaximumBytes} bytes.");
            return 1;
        }

        if (stdout)
        {
            context.Out.Write(markdown);
            return 0;
        }

        string fileName = ReceiptMarkdown.FileName(git.BranchName);
        string path = Path.Combine(
            git.RepositoryRoot,
            ".github",
            "copilot-ledger",
            "receipts",
            fileName);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(
                path,
                markdown,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            context.Error.WriteLine($"Could not write {path}: {ex.Message}");
            return 1;
        }

        context.Out.WriteLine($"Wrote {ReceiptMarkdown.ReceiptsDirectory}/{fileName} for {git.BranchName}.");
        context.Out.WriteLine(
            "Commit it on the branch and push; the CopilotLedger action posts it on the pull request.");
        return 0;
    }
}
