namespace CopilotLedger.Core;

public static class LedgerPaths
{
    public static string DataDirectory { get; } =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".copilot-ledger");

}
