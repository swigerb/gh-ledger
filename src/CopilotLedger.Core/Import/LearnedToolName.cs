namespace CopilotLedger.Core.Import;

// An MCP tool's hashed name, as Copilot CLI's live spans give it (McpToolNames.Hash of a tool.execution_start's
// data.mcpServerName and data.mcpToolName), and that event's data.toolName.
public sealed record LearnedToolName(string HashedName, string ToolName);
