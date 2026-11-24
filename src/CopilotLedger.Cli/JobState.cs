namespace CopilotLedger.Cli;

// The Windows job object that this process runs in: whether it's in one, and its immediate job's LimitFlags
// (JOBOBJECT_BASIC_LIMIT_INFORMATION). (false, 0) outside a job and on other platforms.
public readonly record struct JobState(bool InJob, uint LimitFlags);
