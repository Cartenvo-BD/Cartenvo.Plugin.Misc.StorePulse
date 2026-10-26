using NUnit.Framework;
using StorePulse.Core;

namespace StorePulse.Core.Tests;

public class RunnerTests
{
    private static readonly HealthCheckContext Context = new(1, DateTimeOffset.UtcNow);

    [Test]
    public async Task PreservesOrderAndIsolatesFailuresWithRegisteredScope()
    {
        var visited = new List<string>();
        var checks = new[] {
            new Check("first", () => { visited.Add("first"); throw new Exception("secret"); }, HealthScope.Store),
            new Check("second", () => { visited.Add("second"); return Task.CompletedTask; }) };
        var results = await new HealthCheckRunner().RunAsync(new(checks), Context, TimeSpan.FromSeconds(1), default);
        Assert.That(visited, Is.EqualTo(new[] { "first", "second" }));
        Assert.That(results[0].Status, Is.EqualTo(HealthStatus.Unknown));
        Assert.That(results[0].Scope, Is.EqualTo(HealthScope.Store));
        Assert.That(results[0].SummaryResourceKey, Does.Not.Contain("secret"));
        Assert.That(results[1].Status, Is.EqualTo(HealthStatus.Healthy));
    }

    [Test]
    public async Task BudgetWaitsForActiveWorkThenSkipsRemainingChecks()
    {
        var time = new Clock();
        var finished = false;
        var skipped = false;
        var checks = new[] {
            new Check("first", () => { time.Elapsed = 20; finished = true; return Task.CompletedTask; }),
            new Check("second", () => { skipped = true; return Task.CompletedTask; }, HealthScope.Store) };
        var results = await new HealthCheckRunner(time).RunAsync(new(checks), Context, TimeSpan.FromSeconds(15), default);
        Assert.That(finished, Is.True);
        Assert.That(skipped, Is.False);
        Assert.That(results[0].Status, Is.EqualTo(HealthStatus.Healthy));
        Assert.That(results[1].Status, Is.EqualTo(HealthStatus.Unknown));
        Assert.That(results[1].Scope, Is.EqualTo(HealthScope.Store));
    }

    [Test]
    public async Task DoesNotReturnWhileActiveCheckStillUsesItsServices()
    {
        var time = new Clock();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var check = new Check("first", async () => { started.SetResult(); await release.Task; });
        var running = new HealthCheckRunner(time).RunAsync(new(new[] { check }), Context,
            TimeSpan.FromSeconds(15), default);
        await started.Task;
        time.Elapsed = 20;
        Assert.That(running.IsCompleted, Is.False);
        release.SetResult();
        Assert.That((await running)[0].Status, Is.EqualTo(HealthStatus.Healthy));
    }

    [Test]
    public void CancellationDuringWorkStopsFollowingChecks()
    {
        using var cancellation = new CancellationTokenSource();
        var visited = false;
        var checks = new[] {
            new Check("first", () => { cancellation.Cancel(); return Task.CompletedTask; }),
            new Check("second", () => { visited = true; return Task.CompletedTask; }) };
        Assert.CatchAsync<OperationCanceledException>(async () => await new HealthCheckRunner().RunAsync(
            new(checks), Context, TimeSpan.FromSeconds(1), cancellation.Token));
        Assert.That(visited, Is.False);
    }

    [Test]
    public async Task DisabledChecksDoNotRun()
    {
        var visited = false;
        var check = new Check("first", () => { visited = true; return Task.CompletedTask; });
        var results = await new HealthCheckRunner().RunAsync(new(new[] { check }), Context,
            TimeSpan.FromSeconds(1), default, new HashSet<string> { "FIRST" });
        Assert.That(results, Is.Empty);
        Assert.That(visited, Is.False);
    }

    [Test]
    public async Task SlowCheckIsCancelledAndTheNextOneStillRuns()
    {
        var visited = false;
        var checks = new IHealthCheck[] {
            new WaitingCheck("slow"),
            new Check("second", () => { visited = true; return Task.CompletedTask; }) };
        var results = await new HealthCheckRunner().RunAsync(new(checks), Context, TimeSpan.FromSeconds(30), default,
            checkTimeout: TimeSpan.FromMilliseconds(50));
        Assert.That(results[0].Status, Is.EqualTo(HealthStatus.Unknown));
        Assert.That(results[1].Status, Is.EqualTo(HealthStatus.Healthy));
        Assert.That(visited, Is.True);
    }

    [Test]
    public async Task CheckTimeoutNeverExceedsTheRemainingBudget()
    {
        var results = await new HealthCheckRunner().RunAsync(new(new IHealthCheck[] { new WaitingCheck("slow") }),
            Context, TimeSpan.FromMilliseconds(50), default, checkTimeout: TimeSpan.FromMinutes(5));
        Assert.That(results[0].Status, Is.EqualTo(HealthStatus.Unknown));
    }

    [Test]
    public void CallerCancellationStillStopsTheScan()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        Assert.CatchAsync<OperationCanceledException>(async () => await new HealthCheckRunner().RunAsync(
            new(new IHealthCheck[] { new WaitingCheck("slow") }), Context, TimeSpan.FromSeconds(30), cancellation.Token,
            checkTimeout: TimeSpan.FromSeconds(10)));
    }

    [Test]
    public void CapturedCountsShowAsMinimums()
    {
        var capped = new Dictionary<string, decimal> { ["LogRecords"] = 100000, [MetricFlags.CountIsMinimum] = 1 };
        Assert.That(MetricFlags.Format(capped, 100000, "0", System.Globalization.CultureInfo.InvariantCulture),
            Is.EqualTo("100000+"));
        Assert.That(MetricFlags.Format(new Dictionary<string, decimal>(), 5, "0"), Is.EqualTo("5"));
    }

    private sealed class WaitingCheck(string id) : IHealthCheck
    {
        public string CheckId => id;
        public HealthCategory Category => HealthCategory.Database;
        public HealthScope Scope => HealthScope.System;
        public async Task<HealthCheckResult> ExecuteAsync(HealthCheckContext context, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken); // stands in for a slow database query
            throw new InvalidOperationException("unreachable");
        }
    }

    private sealed class Clock : TimeProvider
    {
        public long Elapsed { get; set; }
        public override long TimestampFrequency => 1;
        public override long GetTimestamp() => Elapsed;
    }

    private sealed class Check(string id, Func<Task> execute, HealthScope scope = HealthScope.System) : IHealthCheck
    {
        public string CheckId => id;
        public HealthCategory Category => HealthCategory.Configuration;
        public HealthScope Scope => scope;
        public async Task<HealthCheckResult> ExecuteAsync(HealthCheckContext context, CancellationToken cancellationToken)
        {
            await execute();
            return new(id, Category, Scope, HealthStatus.Healthy, "title", "summary", "action",
                context.ObservedAtUtc, new Dictionary<string, decimal>());
        }
    }
}
