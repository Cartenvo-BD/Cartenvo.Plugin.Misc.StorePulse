using NUnit.Framework;
using StorePulse.Core;

namespace StorePulse.Core.Tests;

public class TaskHealthRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Grace = TimeSpan.FromMinutes(10);

    [Test]
    public void DisabledTaskIsNotAnOutage()
    {
        var task = new TaskObservation(false, 60, null, null);
        Assert.That(TaskHealthRules.Overdue(task, Now, Grace, 3), Is.EqualTo(HealthStatus.NotApplicable));
        Assert.That(TaskHealthRules.NeverExecuted(task, Now, Grace), Is.EqualTo(HealthStatus.NotApplicable));
    }

    [Test]
    public void RecentlyReenabledTaskGetsGraceDespiteOldExecution()
    {
        var task = new TaskObservation(true, 60, Now.AddMinutes(-1), Now.AddDays(-10));
        Assert.That(TaskHealthRules.Overdue(task, Now, Grace, 3), Is.EqualTo(HealthStatus.NotApplicable));
    }

    [TestCase(-601, HealthStatus.Warning)]
    [TestCase(-600, HealthStatus.Healthy)]
    [TestCase(1, HealthStatus.Unknown)]
    public void OverdueBoundaryAndClockSkew(int seconds, HealthStatus expected)
    {
        var task = new TaskObservation(true, 60, Now.AddDays(-1), Now.AddSeconds(seconds));
        Assert.That(TaskHealthRules.Overdue(task, Now, Grace, 3), Is.EqualTo(expected));
    }

    [Test]
    public void LongIntervalDoesNotWarnBeforeFirstExpectedRun()
    {
        var task = new TaskObservation(true, 86400, Now.AddHours(-1), null);
        Assert.That(TaskHealthRules.NeverExecuted(task, Now, Grace), Is.EqualTo(HealthStatus.NotApplicable));
    }

    [Test]
    public void MissingEnablementEvidenceCannotProveNeverExecutedFailure()
    {
        var task = new TaskObservation(true, 60, null, null);
        Assert.That(TaskHealthRules.NeverExecuted(task, Now, Grace), Is.EqualTo(HealthStatus.Unknown));
    }

    [Test]
    public void NeverExecutedWarnsAfterGrace()
    {
        var task = new TaskObservation(true, 60, Now.AddHours(-1), null);
        Assert.That(TaskHealthRules.NeverExecuted(task, Now, Grace), Is.EqualTo(HealthStatus.Warning));
    }
}
