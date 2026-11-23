namespace CopilotLedger.Core.Import;

// A local session store that gh ledger import knows. Claude Code's format is internal and undocumented, so it's never read.
public enum ImportSourceKind
{
    Copilot,
    Claude,
    Codex,
}
