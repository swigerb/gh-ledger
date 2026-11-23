using CopilotLedger.Core.Import;

namespace CopilotLedger.Tests.Import;

public sealed class ImportIdsTests
{
    private const string Session = "0f8fad5b-d9cb-469f-a165-70867728950e";
    private const string UsageId = "msg_01SyntheticResponseA00000";
    private const string ServiceRequestId = "1f0e9c2a-7b3d-4e5f-8a9b-0c1d2e3f4a5b";

    [Fact]
    public void ID1_RecordKeysAreHashedPerSourceAndKind()
    {
        Assert.Equal(
            "d488d860e770efc924c94c61c0278727bd343d09038d57371d19f663b1cef26f",
            ImportIds.RecordKeySha256(ImportSourceKind.Copilot, ImportedRecordKind.Usage, UsageId));
        Assert.Equal(
            "d57ba7025ffeec5f437aabb47708a52fa402e16cf9ce6b9cff6cc54b6389705c",
            ImportIds.RecordKeySha256(ImportSourceKind.Copilot, ImportedRecordKind.Compaction, ServiceRequestId));
        Assert.Equal(
            "9c84b999b8610a1f30a2f8033d194e153b8e2cdb90c5caaab0395a36a4690af0",
            ImportIds.RecordKeySha256(ImportSourceKind.Copilot, ImportedRecordKind.Usage, ServiceRequestId));
        Assert.Equal(
            "c30d411d93c6e502c64ef699d33095f2e41203aceef25dbab04af74d67039387",
            ImportIds.RecordKeySha256(ImportSourceKind.Codex, ImportedRecordKind.Usage, "resp_0123456789abcdef"));
    }

    [Fact]
    public void ID2_TraceSpanAndToolIdsArePinned()
    {
        Assert.Equal(
            "24909750d9f376616a575a6eeaaae842",
            ImportIds.TraceId(ImportSourceKind.Copilot, Session, UsageId, ImportedRecordKind.Usage, "other"));
        Assert.Equal(
            "78e16000fe718aa49a587d3ea4e6406e",
            ImportIds.TraceId(ImportSourceKind.Copilot, Session, null, ImportedRecordKind.Usage, UsageId));
        Assert.Equal("b93d6266bdfc46ab", ImportIds.SpanId(ImportSourceKind.Copilot, Session, ImportedRecordKind.Usage, UsageId));
        Assert.Equal(
            "d91fdbb4e5326b07",
            ImportIds.ToolSpanId(ImportSourceKind.Copilot, Session, "toolu_01SyntheticTool000000000"));
    }

    [Theory]
    [InlineData(ImportSourceKind.Copilot, "copilot")]
    [InlineData(ImportSourceKind.Claude, "claude")]
    [InlineData(ImportSourceKind.Codex, "codex")]
    public void ID3_SourceNames(ImportSourceKind source, string name)
    {
        Assert.Equal(name, ImportIds.SourceName(source));
    }
}
