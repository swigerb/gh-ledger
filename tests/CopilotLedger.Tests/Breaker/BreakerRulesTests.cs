using CopilotLedger.Core.Breaker;
using CopilotLedger.Otlp.Breaker;
using CopilotLedger.Storage;

namespace CopilotLedger.Tests.Breaker;

public sealed class BreakerRulesTests
{
    private static readonly DateTimeOffset T0 =
        new(2026, 11, 21, 10, 0, 0, TimeSpan.Zero);
    private const string Hash =
        "c3d1fe831856c77818bb8c1242d32d6efa6c37366eb902e56c5fd0054c1dcbc3/918a44e755cc5eee09b08d214831e8339a645158ef1f831a0fa02bd43999700f";

    public static TheoryData<string> VelocityCases =>
    [
        "V1", "V2", "V3", "V4", "V5", "V6", "V7", "V8", "V9", "V10",
    ];

    [Theory]
    [MemberData(nameof(VelocityCases))]
    public void BR1_Rule1(string name)
    {
        (CostEvent[] costs, decimal threshold, TimeSpan window, VelocityTrip? expected) =
            GetVelocityCase(name);

        Assert.Equal(
            expected,
            BreakerRules.FindVelocityTrip(costs, threshold, window));
    }

    public static TheoryData<string> ChurnCases =>
    [
        "C1", "C2", "C3", "C4", "C5", "C6", "C7", "C8", "C9", "C10", "C11",
    ];

    [Theory]
    [MemberData(nameof(ChurnCases))]
    public void BR2_Rule2(string name)
    {
        (ToolCallEvent[] calls, CommitObservation[] commits, int failures, ChurnTrip? expected) =
            GetChurnCase(name);

        Assert.Equal(
            expected,
            BreakerRules.FindChurnTrip(calls, commits, failures));
    }

    [Fact]
    public void BR3_ArgumentChecks()
    {
        Assert.Throws<ArgumentNullException>(
            () => BreakerRules.FindVelocityTrip(null!, 1m, TimeSpan.FromMinutes(5)));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => BreakerRules.FindVelocityTrip([], 0m, TimeSpan.FromMinutes(5)));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => BreakerRules.FindVelocityTrip([], 1m, TimeSpan.Zero));
        Assert.Throws<ArgumentNullException>(
            () => BreakerRules.FindChurnTrip(null!, [], 5));
        Assert.Throws<ArgumentNullException>(
            () => BreakerRules.FindChurnTrip([], null!, 5));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => BreakerRules.FindChurnTrip([], [], 1));
    }

    [Fact]
    public void BR4_Messages()
    {
        Assert.Equal("180.00", BreakerMessages.Credits(180m));
        Assert.Equal("1,234.50", BreakerMessages.Credits(1234.5m));
        Assert.Equal("0.01", BreakerMessages.Credits(0.005m));
        Assert.Equal("0.00", BreakerMessages.Credits(0.004999m));
        Assert.Equal("2.68", BreakerMessages.Credits(2.675m));
        Assert.Equal("1 minute", BreakerMessages.Minutes(1));
        Assert.Equal("5 minutes", BreakerMessages.Minutes(5));
        Assert.Equal("180.00 AI credits in 5 minutes", BreakerMessages.VelocityDetail(1.80m, 5));
        Assert.Equal("1.50 AI credits in 1 minute", BreakerMessages.VelocityDetail(0.015m, 1));
        Assert.Equal("5 failed tool calls in a row", BreakerMessages.ChurnDetail(5));
        Assert.Equal(
            "more than 150.00 AI credits in 5 minutes on one prompt; 5 failed tool calls in a row on one prompt",
            BreakerMessages.RulesSummary(BreakerSettings.Default));
        Assert.Equal(
            "more than 12.50 AI credits in 1 minute on one prompt; 2 failed tool calls in a row on one prompt",
            BreakerMessages.RulesSummary(BreakerSettings.Default with
            {
                VelocityCredits = 12.5m,
                VelocityWindowMinutes = 1,
                ChurnFailures = 2,
            }));

        BreakerAlert trip = BreakerMessages.ForTrip(
            7,
            "swigerb/copilot-ledger",
            "feature/ledger",
            "5 failed tool calls in a row",
            false);
        Assert.Equal(BreakerAlertKind.Trip, trip.Kind);
        Assert.Equal(7, trip.TripId);
        Assert.Equal("swigerb/copilot-ledger", trip.Repository);
        Assert.Equal("feature/ledger", trip.Branch);
        Assert.Equal("gh-ledger circuit breaker tripped", trip.Title);
        Assert.Equal(
            "swigerb/copilot-ledger @ feature/ledger: 5 failed tool calls in a row on one prompt. Run: gh-ledger breaker status",
            trip.Body);
        Assert.False(trip.ShowNotification);

        BranchBudgetRow row = new()
        {
            Repository = "contoso/api",
            Branch = "main",
            BudgetAiCredits = 40.5m,
            SpentAiCredits = 31.5m,
            SetAt = T0,
        };
        BreakerAlert budget = BreakerMessages.ForBudget(row, true);
        Assert.Equal(BreakerAlertKind.Budget, budget.Kind);
        Assert.Null(budget.TripId);
        Assert.Equal("contoso/api", budget.Repository);
        Assert.Equal("main", budget.Branch);
        Assert.Equal("gh-ledger branch budget exceeded", budget.Title);
        Assert.Equal(
            "contoso/api @ main: 31.50 of 40.50 AI credits spent. Run: gh-ledger budget list",
            budget.Body);
        Assert.True(budget.ShowNotification);
    }

    [Theory]
    [InlineData("trip")]
    [InlineData("budget")]
    public void BA1_Alerts_NameTheGivenCommand(string kind)
    {
        string expected = kind switch
        {
            "trip" => "swigerb/copilot-ledger @ feature/ledger: 5 failed tool calls in a row on one prompt. Run: gh ledger breaker status",
            "budget" => "contoso/api @ main: 31.50 of 40.50 AI credits spent. Run: gh ledger budget list",
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

        BreakerAlert alert = kind == "trip"
            ? BreakerMessages.ForTrip(
                7,
                "swigerb/copilot-ledger",
                "feature/ledger",
                "5 failed tool calls in a row",
                false,
                command: "gh ledger")
            : BreakerMessages.ForBudget(
                new BranchBudgetRow
                {
                    Repository = "contoso/api",
                    Branch = "main",
                    BudgetAiCredits = 40.5m,
                    SpentAiCredits = 31.5m,
                    SetAt = T0,
                },
                true,
                command: "gh ledger");

        Assert.Equal(expected, alert.Body);
    }

    private static CostEvent Cost(string id, int seconds, decimal usd) =>
        new(id, T0.AddSeconds(seconds), usd);

    private static (CostEvent[], decimal, TimeSpan, VelocityTrip?) GetVelocityCase(
        string name) =>
        name switch
        {
            "V1" => ([Cost("a", 30, 0.50m), Cost("b", 120, 0.60m), Cost("c", 299, 0.40m)], 1.50m, TimeSpan.FromMinutes(5), null),
            "V2" => ([Cost("a", 30, 0.50m), Cost("b", 120, 0.60m), Cost("c", 299, 0.39m)], 1.50m, TimeSpan.FromMinutes(5), null),
            "V3" => ([Cost("a", 0, 1.00m), Cost("b", 300, 0.60m)], 1.50m, TimeSpan.FromMinutes(5), null),
            "V4" => ([Cost("a", 1, 1.00m), Cost("b", 300, 0.60m)], 1.50m, TimeSpan.FromMinutes(5), new(T0.AddSeconds(300), 1.60m)),
            "V5" => ([Cost("b", 200, 0.70m), Cost("z", 150, 0.00m), Cost("a", 100, 0.81m)], 1.50m, TimeSpan.FromMinutes(5), new(T0.AddSeconds(200), 1.51m)),
            "V6" => ([Cost("b", 60, 0.76m), Cost("a", 60, 0.75m)], 1.50m, TimeSpan.FromMinutes(5), new(T0.AddSeconds(60), 1.51m)),
            "V7" => ([Cost("a", 0, 1.00m), Cost("b", 59, 0.51m)], 1.50m, TimeSpan.FromMinutes(1), new(T0.AddSeconds(59), 1.51m)),
            "V8" => ([Cost("a", 0, 1.00m), Cost("b", 60, 0.51m)], 1.50m, TimeSpan.FromMinutes(1), null),
            "V9" => ([], 1.50m, TimeSpan.FromMinutes(5), null),
            "V10" => ([Cost("a", 30, 0.50m), Cost("b", 120, 0.60m), Cost("c", 299, 0.400000000001m)], 1.50m, TimeSpan.FromMinutes(5), new(T0.AddSeconds(299), 1.500000000001m)),
            _ => throw new ArgumentOutOfRangeException(nameof(name)),
        };

    private static (ToolCallEvent[], CommitObservation[], int, ChurnTrip?) GetChurnCase(
        string name) =>
        name switch
        {
            "C1" => (Calls(F, 1, F, 2, F, 3, S, 4, F, 5, F, 6, F, 7, F, 8), [], 5, null),
            "C2" => (Calls(E, 1, E, 2, E, 3, E, 4, E, 5), [], 5, new(T0.AddSeconds(5), 5)),
            "C3" => (Calls(F, 1, F, 2, F, 3, F, 4, F, 5), [Commit("c1", 0.5m, "aaaa"), Commit("c2", 3.5m, "bbbb")], 5, null),
            "C4" => (Calls(F, 1, F, 2, F, 3, F, 4, F, 5), [Commit("c1", 0.5m, "aaaa"), Commit("c2", 3.5m, "aaaa")], 5, new(T0.AddSeconds(5), 5)),
            "C5" => (Calls(F, 1, F, 2, F, 3, F, 4, F, 5), [Commit("c1", 0.5m, "aaaa"), Commit("c2", 5m, "bbbb")], 5, null),
            "C6" => (Calls(F, 1, N, 2, F, 3, N, 4, F, 5, N, 6, F, 7, N, 8, F, 9), [], 5, new(T0.AddSeconds(9), 5)),
            "C7" => (Calls(F, 1, F, 2, F, 3, F, 4, X, 5), [], 5, new(T0.AddSeconds(5), 5)),
            "C8" => (Calls(F, 1, F, 2), [], 2, new(T0.AddSeconds(2), 2)),
            "C9" => (Calls(F, 1, F, 2, F, 3, F, 4, N, 5, F, 6), [], 5, new(T0.AddSeconds(6), 5)),
            "C10" => (Calls(F, 1, F, 2, F, 3, F, 4, S, 5, F, 6), [], 5, null),
            "C11" => ([], [], 5, null),
            _ => throw new ArgumentOutOfRangeException(nameof(name)),
        };

    private static CommitObservation Commit(string id, decimal seconds, string sha) =>
        new(id, T0.AddSeconds((double)seconds), sha);

    private static ToolCallEvent[] Calls(params object[] values)
    {
        List<(ToolKind Kind, int Seconds)> calls = [];
        for (int index = 0; index < values.Length; index += 2)
        {
            calls.Add(((ToolKind)values[index], (int)values[index + 1]));
        }

        return calls
            .Select((call, index) => new ToolCallEvent(
                $"0{index + 1:D1}",
                T0.AddSeconds(call.Seconds),
                call.Kind switch
                {
                    ToolKind.F or ToolKind.S or ToolKind.N or ToolKind.X => call.Kind == ToolKind.N
                        ? "apply_patch"
                        : "powershell",
                    ToolKind.E => Hash,
                    _ => throw new ArgumentOutOfRangeException(nameof(calls)),
                },
                call.Kind switch
                {
                    ToolKind.F => 1,
                    ToolKind.S => 0,
                    ToolKind.X => 0,
                    _ => null,
                },
                call.Kind is ToolKind.E or ToolKind.X ? 2 : 0))
            .ToArray();
    }

    private static ToolKind F => ToolKind.F;
    private static ToolKind S => ToolKind.S;
    private static ToolKind E => ToolKind.E;
    private static ToolKind N => ToolKind.N;
    private static ToolKind X => ToolKind.X;

    private enum ToolKind
    {
        F,
        S,
        E,
        N,
        X,
    }
}
