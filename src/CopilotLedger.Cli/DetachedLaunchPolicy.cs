namespace CopilotLedger.Cli;

public enum DetachedLaunch
{
    Independent,
    BreakawayFromJob,
    Blocked,
}

public static class DetachedLaunchPolicy
{
    public const uint BreakawayOk = 0x00000800;
    public const uint SilentBreakawayOk = 0x00001000;
    public const uint KillOnJobClose = 0x00002000;

    public static DetachedLaunch Decide(JobState state)
    {
        if (!state.InJob)
        {
            return DetachedLaunch.Independent;
        }

        if ((state.LimitFlags & SilentBreakawayOk) != 0)
        {
            return DetachedLaunch.Independent;
        }

        if ((state.LimitFlags & BreakawayOk) != 0)
        {
            return DetachedLaunch.BreakawayFromJob;
        }

        return (state.LimitFlags & KillOnJobClose) != 0
            ? DetachedLaunch.Blocked
            : DetachedLaunch.Independent;
    }

    public static bool DiesWithJob(JobState state) =>
        state.InJob && (state.LimitFlags & KillOnJobClose) != 0;
}
