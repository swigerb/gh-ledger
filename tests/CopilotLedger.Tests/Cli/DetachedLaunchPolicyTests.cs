using CopilotLedger.Cli;

namespace CopilotLedger.Tests.Cli;

public sealed class DetachedLaunchPolicyTests
{
    [Theory]
    [InlineData(false, 0x0u, DetachedLaunch.Independent)]
    [InlineData(false, 0x2000u, DetachedLaunch.Independent)]
    [InlineData(true, 0x0u, DetachedLaunch.Independent)]
    [InlineData(true, 0x2000u, DetachedLaunch.Blocked)]
    [InlineData(true, 0x2800u, DetachedLaunch.BreakawayFromJob)]
    [InlineData(true, 0x3000u, DetachedLaunch.Independent)]
    [InlineData(true, 0x3800u, DetachedLaunch.Independent)]
    [InlineData(true, 0x0800u, DetachedLaunch.BreakawayFromJob)]
    public void DL1_Decide(bool inJob, uint flags, DetachedLaunch expected)
    {
        Assert.Equal(expected, DetachedLaunchPolicy.Decide(new JobState(inJob, flags)));
    }

    [Theory]
    [InlineData(false, 0x2000u, false)]
    [InlineData(true, 0x2000u, true)]
    [InlineData(true, 0x2800u, true)]
    [InlineData(true, 0x0u, false)]
    public void DL2_DiesWithJob(bool inJob, uint flags, bool expected)
    {
        Assert.Equal(expected, DetachedLaunchPolicy.DiesWithJob(new JobState(inJob, flags)));
    }

    [Fact]
    public void DL3_SystemPlatform_ReadsThisProcessesJob()
    {
        JobState state = new SystemCliPlatform().GetJobState();

        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(default, state);
        }
        else if (!state.InJob)
        {
            Assert.Equal(0u, state.LimitFlags);
        }
    }
}
