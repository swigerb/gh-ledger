namespace CopilotLedger.Storage;

public enum LedgerGrouping
{
    Repository,
    Branch,
    Model,
    Agent,
    ChannelModel,
    PullRequest,     // GetCacheEfficiencyAsync only: repository and pull request, or repository and branch without one
}
