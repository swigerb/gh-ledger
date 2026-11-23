namespace CopilotLedger.Otlp.Normalization;

public static class GenAiAttributeNames
{
    public const string OperationName = "gen_ai.operation.name";
    public const string ToolName = "gen_ai.tool.name";
    public const string ProcessExitCode = "process.exit.code";
    public const string ProviderName = "gen_ai.provider.name";
    public const string RequestModel = "gen_ai.request.model";
    public const string ResponseModel = "gen_ai.response.model";
    public const string AgentName = "gen_ai.agent.name";
    public const string ConversationId = "gen_ai.conversation.id";
    public const string InputTokens = "gen_ai.usage.input_tokens";
    public const string OutputTokens = "gen_ai.usage.output_tokens";
    public const string CacheReadInputTokens = "gen_ai.usage.cache_read.input_tokens";
    public const string CacheWriteInputTokens = "gen_ai.usage.cache_write.input_tokens";
    public const string LegacyCacheCreationInputTokens = "gen_ai.usage.cache_creation.input_tokens";
    public const string ServerAddress = "server.address";
    public const string ServerPort = "server.port";
    public const string ServiceRequestId = "github.copilot.service_request_id";
    public const string GitRepository = "github.copilot.git.repository";
    public const string GitBranch = "github.copilot.git.branch";
    public const string GitCommitSha = "github.copilot.git.commit_sha";
    public const string LegacyRepoRemoteUrl = "copilot_chat.repo.remote_url";
    public const string LegacyRepoHeadBranchName = "copilot_chat.repo.head_branch_name";
    public const string LegacyRepoHeadCommitHash = "copilot_chat.repo.head_commit_hash";
    public const string ServiceName = "service.name";                          // resource attribute
    public const string ProcessWorkingDirectory = "process.working_directory"; // resource attribute
}
