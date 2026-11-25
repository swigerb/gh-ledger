using CopilotLedger.Core.Telemetry;

namespace CopilotLedger.Tests.Telemetry;

public sealed class McpToolNamesTests
{
    private const string LedgerHash =
        "fe14010b4fe83303852f0467c919ef9a7ca089b91e96e3aad7d426dd87079297/eb20c46435d60871da0eb97ce23417c04835e272725c59a3a649c7ce8e521d97";

    private const string ExecJs =
        "10be6beef496b89dae799c37bd04e5eba2a6264a0a4d605f5661b50d7dc4d1ea/8453dc19fda259c463d7aaffb07becf26b7ce2cfc690a2b0a957e201653dc85a";

    [Theory]
    [InlineData(
        "ledger",
        "get_branch_ledger",
        LedgerHash)]
    [InlineData(
        "computer-use",
        "exec_js",
        ExecJs)]
    [InlineData(
        "caf\u00e9",
        "exec_js",
        "850f7dc43910ff890f8879c0ed26fe697c93a067ad93a7d50f466a7028a9bf4e/8453dc19fda259c463d7aaffb07becf26b7ce2cfc690a2b0a957e201653dc85a")]
    [InlineData(
        "",
        "",
        "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855/e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855")]
    public void MN1_Hash_IsTheSha256OfEachNameInUtf8(string serverName, string toolName, string expected)
    {
        Assert.Equal(expected, McpToolNames.Hash(serverName, toolName));
    }

    public static TheoryData<string?, bool> IsHashedCases => new()
    {
        { LedgerHash, true },
        { ExecJs, true },
        { null, false },
        { "", false },
        { "powershell", false },
        { ExecJs.ToUpperInvariant(), false },
        { ExecJs[..128], false },
        { ExecJs + "0", false },
        { ExecJs.Replace('/', '-'), false },
        { "g" + ExecJs[1..], false },
        { ExecJs[..63] + "/0" + ExecJs[65..], false },
    };

    [Theory]
    [MemberData(nameof(IsHashedCases))]
    public void MN2_IsHashed_AcceptsOnlyTheHashedShape(string? name, bool expected)
    {
        Assert.Equal(expected, McpToolNames.IsHashed(name));
    }

    [Theory]
    [InlineData(LedgerHash, "MCP tool fe14010b/eb20c464")]
    [InlineData(ExecJs, "MCP tool 10be6bee/8453dc19")]
    public void MN3_ShortForm_IsTheFirst8DigitsOfEachHash(string hashedName, string expected)
    {
        Assert.Equal(expected, McpToolNames.ShortForm(hashedName));
    }

    [Fact]
    public void MN4_ShortForm_RefusesAnotherName()
    {
        Assert.Throws<ArgumentException>(() => McpToolNames.ShortForm("powershell"));
    }

    public static TheoryData<string?, string?> DisplayCases => new()
    {
        { null, null },
        { "powershell", "powershell" },
        { ExecJs, "computer-use-exec_js" },
        { LedgerHash, "MCP tool fe14010b/eb20c464" },
        { ExecJs.ToUpperInvariant(), ExecJs.ToUpperInvariant() },
    };

    [Theory]
    [MemberData(nameof(DisplayCases))]
    public void MN5_Display_ResolvesOnlyHashedNames(string? name, string? expected)
    {
        Dictionary<string, string> learned = new(StringComparer.Ordinal)
        {
            [ExecJs] = "computer-use-exec_js",
            ["powershell"] = "never shown",
        };

        Assert.Equal(expected, McpToolNames.Display(name, learned));
    }

    [Fact]
    public void MN6_Display_RequiresTheLearnedNames()
    {
        Assert.Throws<ArgumentNullException>(() => McpToolNames.Display("powershell", null!));
    }
}
