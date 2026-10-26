using NUnit.Framework;
using StorePulse.Core;

namespace StorePulse.Core.Tests;

public class HealthCheckRegistryTests
{
    private sealed class Check(string id, HealthCategory category) : IHealthCheck
    {
        public string CheckId => id;
        public HealthCategory Category => category;
        public Task<HealthCheckResult> ExecuteAsync(HealthCheckContext context, CancellationToken cancellationToken)
            => throw new NotImplementedException();
    }

    [Test]
    public void DuplicateIdentifiersAreRejectedIgnoringCase()
        => Assert.Throws<ArgumentException>(() => new HealthCheckRegistry([
            new Check("TASK-001", HealthCategory.ScheduledTasks),
            new Check("task-001", HealthCategory.ScheduledTasks)]));

    [Test]
    public void EmptyIdentifiersAreRejected()
        => Assert.Throws<ArgumentException>(() => new HealthCheckRegistry([new Check(" ", HealthCategory.Email)]));

    [Test]
    public void SelectionRespectsDisabledIdentifiersAndCategory()
    {
        var registry = new HealthCheckRegistry([
            new Check("EMAIL-001", HealthCategory.Email),
            new Check("EMAIL-002", HealthCategory.Email),
            new Check("TASK-001", HealthCategory.ScheduledTasks)]);
        var selected = registry.Select(new HashSet<string> { "email-001" }, HealthCategory.Email);
        Assert.That(selected.Select(check => check.CheckId), Is.EqualTo(new[] { "EMAIL-002" }));
    }
}
