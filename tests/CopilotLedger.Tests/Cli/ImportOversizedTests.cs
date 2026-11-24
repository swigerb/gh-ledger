using System.Text.Json;
using CopilotLedger.Core.Import;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Cli;

public sealed class ImportOversizedTests
{
    private const string R1 = "11111111-1111-4111-8111-111111111111";
    private const string R2 = "22222222-2222-4222-8222-222222222222";

    private static readonly DateTimeOffset Now = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task OV1_ALogWhoseOversizedLinesAreUnread_CompletesAndIsSkippedNextTime()
    {
        using TempDirectory temp = new();
        await using CliHarness harness = new(
            temp.Path,
            new FakeCliPlatform(),
            new ManualTimeProvider(Now));
        new CopilotSessionLogs("s-ov")
            .Usage("u-1", R1)
            .Raw(O("{\"type\":\"model.messages_snapshot\",\"x\":\""))
            .Raw(O("{\"type\":\"model.model_call_success\",\"x\":\""))
            .Usage("u-2", R2)
            .Write(harness.Context.CopilotHome);

        CliRun first = await harness.RunAsync("import", "--all", "--json");
        CliRun second = await harness.RunAsync("import", "--all", "--json");

        Assert.Equal(0, first.ExitCode);
        Assert.Empty(first.Error);
        using JsonDocument firstJson = JsonDocument.Parse(first.Out);
        JsonElement firstSource = firstJson.RootElement.GetProperty("sources")[0];
        Assert.Equal("copilot", firstSource.GetProperty("source").GetString());
        Assert.Equal(2, firstSource.GetProperty("recorded").GetInt64());
        Assert.Equal(0, firstSource.GetProperty("malformedLines").GetInt64());
        Assert.Equal(0, firstSource.GetProperty("unsupportedUsageShapes").GetInt64());
        Assert.Equal(2, firstSource.GetProperty("skippedEvents").GetInt64());
        Assert.Equal("complete", firstSource.GetProperty("coverage").GetString());

        Assert.Equal(0, second.ExitCode);
        Assert.Empty(second.Error);
        using JsonDocument secondJson = JsonDocument.Parse(second.Out);
        JsonElement secondSource = secondJson.RootElement.GetProperty("sources")[0];
        Assert.Equal(1, secondSource.GetProperty("sessionsSkippedUnchanged").GetInt64());
        Assert.Equal(0, secondSource.GetProperty("recorded").GetInt64());
        Assert.Equal(0, secondSource.GetProperty("duplicate").GetInt64());

        Assert.DoesNotContain(CopilotSessionLogs.Canary, first.Out, StringComparison.Ordinal);
        Assert.DoesNotContain("xxxxxxxx", first.Out, StringComparison.Ordinal);
        Assert.DoesNotContain(CopilotSessionLogs.Canary, second.Out, StringComparison.Ordinal);
        Assert.DoesNotContain("xxxxxxxx", second.Out, StringComparison.Ordinal);
    }

    private static string O(string head) =>
        head + new string('x', ImportLimits.MaxLineBytes + 1 - head.Length - 3) + "\"}}";
}
