using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CopilotLedger.Core.Import;
using CopilotLedger.Tests.TestSupport;

namespace CopilotLedger.Tests.Import;

public sealed class CopilotSessionGateTests
{
    private const string SessionId = "0f8fad5b-d9cb-469f-a165-70867728950e";

    private static readonly CopilotSessionLogSource Source = new();

    public static TheoryData<string, int, string, SessionLogStatus, string?> Gates => new()
    {
        { "copilot-agent", 1, "1.0.103", SessionLogStatus.Read, "1.0.103" },
        {
            "copilot-agent", 1, "1.0.106-8.unstable.r43566476370.g869eef3", SessionLogStatus.Read,
            "1.0.106-8.unstable.r43566476370.g869eef3"
        },
        { "copilot-agent", 1, "1.0.114", SessionLogStatus.Read, "1.0.114" },
        { "copilot-agent", 1, "1.0.115", SessionLogStatus.Read, "1.0.115" },
        { "copilot-agent", 1, "1.0.116", SessionLogStatus.UnsupportedProducerVersion, "1.0.116" },
        { "copilot-agent", 1, "1.0.120", SessionLogStatus.UnsupportedProducerVersion, "1.0.120" },
        { "copilot-agent", 1, "1.0.102", SessionLogStatus.AggregateOnly, "1.0.102" },
        { "copilot-agent", 1, "1.0.98", SessionLogStatus.AggregateOnly, "1.0.98" },
        { "other-agent", 1, "1.0.110", SessionLogStatus.UnsupportedProducer, null },
        { "copilot-agent", 2, "1.0.110", SessionLogStatus.UnsupportedProducer, null },
    };

    [Fact]
    public async Task CS1_DiscoveryListsSessionDirectoriesInOrder()
    {
        using TempDirectory temp = new();
        string second = new CopilotSessionLogs("b-session").Write(temp.Path);
        string first = new CopilotSessionLogs("a-session").Write(temp.Path, active: true);
        string root = Path.Combine(temp.Path, "session-state");
        Directory.CreateDirectory(Path.Combine(root, ".hidden"));
        await File.WriteAllTextAsync(Path.Combine(root, ".hidden", "events.jsonl"), "{}\n");
        Directory.CreateDirectory(Path.Combine(root, "c-without-events"));
        await File.WriteAllTextAsync(Path.Combine(root, "stray.jsonl"), "{}\n");

        SessionLogDiscovery discovery = Source.Discover(temp.Path);
        IReadOnlyList<SessionLogLocation> logs = discovery.Logs;

        Assert.Equal(["a-session", "b-session"], logs.Select(log => log.LogId));
        Assert.Equal([true, false], logs.Select(log => log.IsActive));
        Assert.Equal([Path.GetFullPath(first), Path.GetFullPath(second)], logs.Select(log => log.Path));
        Assert.Equal(new FileInfo(first).Length, logs[0].SizeBytes);
        Assert.Equal(new DateTimeOffset(File.GetLastWriteTimeUtc(first), TimeSpan.Zero), logs[0].LastWriteTime);
        Assert.All(logs, log => Assert.Equal(ImportSourceKind.Copilot, log.Source));
        Assert.Equal(0, discovery.UnreadableEntries);
        SessionLogDiscovery missing = Source.Discover(Path.Combine(temp.Path, "missing"));
        Assert.Empty(missing.Logs);
        Assert.Equal(0, missing.UnreadableEntries);
    }

    [Theory]
    [MemberData(nameof(Gates))]
    public async Task CS2_TheGateReadsOnlyCopilot1_0_103To1_0_115(
        string producer,
        int schemaVersion,
        string version,
        SessionLogStatus status,
        string? reported)
    {
        using TempDirectory temp = new();
        new CopilotSessionLogs(SessionId, version, producer: producer, schemaVersion: schemaVersion)
            .Raw("{not json")
            .UserMessage()
            .Write(temp.Path);

        SessionLogReadResult result = await ReadAsync(temp.Path);

        Assert.Equal(status, result.Status);
        Assert.Equal(reported, result.ProducerVersion);
        if (status == SessionLogStatus.Read)
        {
            Assert.Equal(1, result.MalformedLines);
            Assert.Equal(1, result.SkippedEvents);
        }
        else
        {
            ImportDiagnostic diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal(
                status switch
                {
                    SessionLogStatus.AggregateOnly => ImportDiagnosticCode.AggregateOnlySession,
                    SessionLogStatus.UnsupportedProducer => ImportDiagnosticCode.UnsupportedProducer,
                    _ => ImportDiagnosticCode.UnsupportedProducerVersion,
                },
                diagnostic.Code);
            Assert.Equal(reported, diagnostic.Version);
            Assert.Equal(ImportEventKind.SessionStart, diagnostic.Event);
            Assert.Equal(1, diagnostic.FirstLine);
            Assert.Equal(0, result.MalformedLines);
            Assert.Equal(0, result.SkippedEvents);
        }
    }

    [Theory]
    [InlineData(false, SessionLogStatus.MissingSessionStart)]
    [InlineData(true, SessionLogStatus.SessionIdMismatch)]
    public async Task CS3_TheFirstEventMustBeThisSessionsStart(bool otherSession, SessionLogStatus status)
    {
        using TempDirectory temp = new();
        string text = otherSession
            ? new CopilotSessionLogs("another-session").Text()
            : "{\"type\":\"user.message\",\"data\":{\"content\":\"" + CopilotSessionLogs.Canary + "\"}}\n"
              + new CopilotSessionLogs(SessionId).Text();
        await WriteAsync(temp.Path, text);

        SessionLogReadResult result = await ReadAsync(temp.Path);

        Assert.Equal(status, result.Status);
        Assert.Equal(1, Assert.Single(result.Diagnostics).FirstLine);
        AssertNoCanary(result);
    }

    [Fact]
    public async Task CS4_MalformedLinesAreCountedWithTheirPlaceOnly()
    {
        using TempDirectory temp = new();
        CopilotSessionLogs log = new CopilotSessionLogs(SessionId)
            .Raw("{\"type\":\"user.message\",\"data\":\"" + CopilotSessionLogs.Canary)
            .Raw("[\"" + CopilotSessionLogs.Canary + "\"]")
            .Raw("{\"data\":\"" + CopilotSessionLogs.Canary + "\"}");
        log.Write(temp.Path);

        SessionLogReadResult result = await ReadAsync(temp.Path);

        Assert.Equal(SessionLogStatus.Read, result.Status);
        Assert.Equal(3, result.MalformedLines);
        ImportDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(ImportDiagnosticCode.MalformedLine, diagnostic.Code);
        Assert.Equal(ImportEventKind.Unknown, diagnostic.Event);
        Assert.Null(diagnostic.Field);
        Assert.Equal(3, diagnostic.Count);
        Assert.Equal(2, diagnostic.FirstLine);
        Assert.Equal(Encoding.UTF8.GetByteCount(log.Lines[0]) + 1, diagnostic.FirstByteOffset);
        AssertNoCanary(result);
    }

    [Fact]
    public async Task CS5_ALineLongerThan8MiBIsDiscardedUnread()
    {
        using TempDirectory temp = new();
        string prefix = "{\"type\":\"user.message\",\"data\":{\"content\":\"" + CopilotSessionLogs.Canary;
        string suffix = "\"}}";
        string oversized = prefix + new string('x', ImportLimits.MaxLineBytes + 1 - prefix.Length - suffix.Length) + suffix;
        new CopilotSessionLogs(SessionId).Raw(oversized).UserMessage().Write(temp.Path);

        SessionLogReadResult result = await ReadAsync(temp.Path);

        Assert.Equal(ImportLimits.MaxLineBytes + 1, Encoding.UTF8.GetByteCount(oversized));
        Assert.Equal(1, result.MalformedLines);
        Assert.Equal(1, result.SkippedEvents);
        ImportDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(ImportDiagnosticCode.OversizedLine, diagnostic.Code);
        Assert.Equal(2, diagnostic.FirstLine);
        AssertNoCanary(result);
    }

    [Fact]
    public async Task CS6_AnUnterminatedLastLineIsNeverParsed()
    {
        using TempDirectory temp = new();
        await WriteAsync(temp.Path, new CopilotSessionLogs(SessionId).Text() + "{\"type\":\"journal.link\"}");

        SessionLogReadResult result = await ReadAsync(temp.Path);

        Assert.Equal(1, result.MalformedLines);
        ImportDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(ImportDiagnosticCode.UnterminatedLine, diagnostic.Code);
        Assert.Equal(2, diagnostic.FirstLine);
    }

    [Fact]
    public async Task CS7_TheTypeIsFoundAfterContent()
    {
        using TempDirectory temp = new();
        new CopilotSessionLogs(SessionId)
            .Raw("{\"data\":{\"content\":\"" + CopilotSessionLogs.Canary + "\"},\"type\":\"user.message\"}")
            .Raw("{\"data\":{\"items\":[{\"x\":\"" + CopilotSessionLogs.Canary + "\"}]},\"t\\u0079pe\":\"journal.link\"}")
            .Write(temp.Path);

        SessionLogReadResult result = await ReadAsync(temp.Path);

        Assert.Equal(0, result.MalformedLines);
        Assert.Equal(1, result.SkippedEvents);
        ImportDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(ImportDiagnosticCode.JournalPredecessor, diagnostic.Code);
        Assert.Equal(3, diagnostic.FirstLine);
        AssertNoCanary(result);
    }

    [Fact]
    public async Task CS8_UnknownTypesAreCountedAndNeverEchoed()
    {
        using TempDirectory temp = new();
        new CopilotSessionLogs(SessionId)
            .Event(CopilotSessionLogs.Canary, new JsonObject { ["x"] = 1 })
            .Event("assistant.message", new JsonObject { ["content"] = CopilotSessionLogs.Canary })
            .Event("session.start", new JsonObject { ["sessionId"] = SessionId })
            .Write(temp.Path);

        SessionLogReadResult result = await ReadAsync(temp.Path);

        Assert.Equal(SessionLogStatus.Read, result.Status);
        Assert.Equal(3, result.SkippedEvents);
        Assert.Empty(result.Diagnostics);
        AssertNoCanary(result);
    }

    [Fact]
    public async Task CS9_ATypeGivenTwiceIsMalformed()
    {
        using TempDirectory temp = new();
        new CopilotSessionLogs(SessionId).Raw("{\"type\":\"user.message\",\"type\":\"journal.link\"}").Write(temp.Path);

        SessionLogReadResult result = await ReadAsync(temp.Path);

        Assert.Equal(1, result.MalformedLines);
        Assert.Equal(ImportDiagnosticCode.MalformedLine, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public async Task CS10_ContentAfterTheObjectIsMalformed()
    {
        using TempDirectory temp = new();
        new CopilotSessionLogs(SessionId).Raw("{\"type\":\"journal.link\"} {\"type\":\"journal.link\"}").Write(temp.Path);

        SessionLogReadResult result = await ReadAsync(temp.Path);

        Assert.Equal(1, result.MalformedLines);
        Assert.Equal(ImportDiagnosticCode.MalformedLine, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public async Task CS11_AJournalLinkIsReportedOnce()
    {
        using TempDirectory temp = new();
        new CopilotSessionLogs(SessionId)
            .Event("journal.link", new JsonObject { ["previous"] = CopilotSessionLogs.Canary })
            .Event("journal.link", new JsonObject())
            .Write(temp.Path);

        SessionLogReadResult result = await ReadAsync(temp.Path);

        ImportDiagnostic diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(ImportDiagnosticCode.JournalPredecessor, diagnostic.Code);
        Assert.Equal(ImportEventKind.JournalLink, diagnostic.Event);
        Assert.Equal(1, diagnostic.Count);
        Assert.Equal(2, diagnostic.FirstLine);
        AssertNoCanary(result);
    }

    [Fact]
    public async Task CS12_BytesAppendedAfterDiscoveryAreLeftForTheNextRun()
    {
        using TempDirectory temp = new();
        string path = new CopilotSessionLogs(SessionId).Write(temp.Path);
        SessionLogLocation location = Assert.Single(Source.Discover(temp.Path).Logs);
        await File.AppendAllTextAsync(path, "{not json\n");

        SessionLogReadResult result = await Source.ReadAsync(location, CancellationToken.None);

        Assert.Equal(SessionLogStatus.Read, result.Status);
        Assert.Equal(0, result.MalformedLines);
        Assert.Empty(result.Diagnostics);
    }

    [Theory]
    [InlineData("1.0.103", 1, 0, 103)]
    [InlineData("1.0.106-8.unstable.r43566476370.g869eef3", 1, 0, 106)]
    [InlineData("0.171.0+build.5", 0, 171, 0)]
    public void CS13_ProducerVersionsParse(string value, int major, int minor, int patch)
    {
        Assert.True(ProducerVersion.TryParse(value, out ProducerVersion? version));
        Assert.Equal(new ProducerVersion(major, minor, patch), version);
    }

    [Theory]
    [InlineData("1.0")]
    [InlineData("01.0.103")]
    [InlineData("1.0.103-")]
    [InlineData("v1.0.103")]
    [InlineData("1.0.1234567890")]
    public void CS14_OtherVersionsDont(string value)
    {
        Assert.False(ProducerVersion.TryParse(value, out ProducerVersion? version));
        Assert.Null(version);
    }

    [Fact]
    public async Task CS15_ASessionStoreThatCantBeListedIsCounted()
    {
        using TempDirectory temp = new();
        await File.WriteAllTextAsync(Path.Combine(temp.Path, "session-state"), "not a folder");

        SessionLogDiscovery discovery = Source.Discover(temp.Path);

        Assert.Empty(discovery.Logs);
        Assert.Equal(1, discovery.UnreadableEntries);
    }

    // ValueTextEquals can't unescape a lone surrogate, so a name or type that holds one is none of the parser's: its line is
    // skipped, or its property ignored, like any other unknown one, and the log is read on.
    [Fact]
    public async Task CS16_AnEscapedLoneSurrogateInANameOrATypeIsNeverOneOfTheParsers()
    {
        using TempDirectory temp = new();
        string usage = new CopilotSessionLogs(SessionId)
            .Usage("u1", "1f0e9c2a-7b3d-4e5f-8a9b-0c1d2e3f4a5b")
            .Lines[1]
            .Replace("\"usage\":{\"model\":", "\"usage\":{\"\\uDC00\\uDC00\":1,\"model\":", StringComparison.Ordinal);
        new CopilotSessionLogs(SessionId)
            .Raw("{\"\\uDC00\\uDC00\":\"" + CopilotSessionLogs.Canary + "\",\"type\":\"user.message\"}")
            .Raw("{\"type\":\"\\uDC00\\uDC00\\uDC00\",\"data\":{\"content\":\"" + CopilotSessionLogs.Canary + "\"}}")
            .Raw(usage)
            .Write(temp.Path);

        SessionLogReadResult result = await ReadAsync(temp.Path);

        Assert.Equal(SessionLogStatus.Read, result.Status);
        Assert.Equal(
            (1L, 0L, 0L, 2L),
            (result.RecordsParsed, result.UnsupportedUsageShapes, result.MalformedLines, result.SkippedEvents));
        Assert.Equal("u1", Assert.Single(result.Records).RecordKey);
        Assert.Empty(result.Diagnostics);
        AssertNoCanary(result);
    }

    // A session folder that this user can't enter is an unreadable entry, never a session without a log, and the others are
    // still found; a folder without events.jsonl still has no log. FileInfo.Exists, which is false for a file it can't
    // examine, hid such a folder before. Root on Unix can enter it, so there it's read.
    [Fact]
    public void CS17_ASessionFolderThatCantBeEnteredIsCounted()
    {
        using TempDirectory temp = new();
        new CopilotSessionLogs("a-session").Write(temp.Path);
        string folder = Path.GetDirectoryName(new CopilotSessionLogs("b-session").Write(temp.Path))!;
        Directory.CreateDirectory(Path.Combine(temp.Path, "session-state", "c-without-events"));

        SessionLogDiscovery discovery;
        bool enforced;
        using (DeniedDirectory denied = new(folder))
        {
            enforced = denied.Enforced;
            discovery = Source.Discover(temp.Path);
        }

        Assert.True(enforced || (!OperatingSystem.IsWindows() && Environment.IsPrivilegedProcess));
        string[] found = enforced ? ["a-session"] : ["a-session", "b-session"];
        Assert.Equal(found, discovery.Logs.Select(log => log.LogId));
        Assert.Equal(enforced ? 1 : 0, discovery.UnreadableEntries);
    }

    private static async Task<SessionLogReadResult> ReadAsync(string home) =>
        await Source.ReadAsync(Assert.Single(Source.Discover(home).Logs), CancellationToken.None);

    private static async Task WriteAsync(string home, string text)
    {
        string directory = Path.Combine(home, "session-state", SessionId);
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "events.jsonl"), text);
    }

    private static void AssertNoCanary(SessionLogReadResult result) =>
        Assert.DoesNotContain(CopilotSessionLogs.Canary, JsonSerializer.Serialize(result), StringComparison.Ordinal);
}
