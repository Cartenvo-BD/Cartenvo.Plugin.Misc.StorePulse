using NUnit.Framework;
using StorePulse.Core;

namespace StorePulse.Core.Tests;

public class HealthScoringTests
{
    private static HealthCheckResult Result(string id, HealthCategory category, HealthStatus status)
        => new(id, category, HealthScope.System, status, "title", "summary", "action",
            DateTimeOffset.UtcNow, new Dictionary<string, decimal>());

    [TestCase(HealthStatus.Unknown)]
    [TestCase(HealthStatus.NotApplicable)]
    public void NoEvaluatedChecksHasNoScore(HealthStatus status)
    {
        var score = HealthScoring.Calculate([Result("A", HealthCategory.Email, status)]);
        Assert.That(score.Score, Is.Null);
        Assert.That(score.CoveragePercent, Is.Zero);
    }

    [Test]
    public void UnknownReducesCoverageWithoutCountingAsPass()
    {
        var score = HealthScoring.Calculate([
            Result("A", HealthCategory.Email, HealthStatus.Healthy),
            Result("B", HealthCategory.Email, HealthStatus.Unknown)]);
        Assert.That(score.Score, Is.EqualTo(100));
        Assert.That(score.CoveragePercent, Is.EqualTo(50));
    }

    [Test]
    public void CatalogCannotHideCriticalEmailIssue()
    {
        var results = Enumerable.Range(0, 100).Select(i => Result($"CAT-{i}", HealthCategory.Catalog, HealthStatus.Healthy))
            .Append(Result("EMAIL-001", HealthCategory.Email, HealthStatus.Critical));
        var score = HealthScoring.Calculate(results);
        Assert.That(score.Score, Is.Zero);
        Assert.That(score.CriticalCount, Is.EqualTo(1));
    }

    [Test]
    public void CategoryWeightsApplyAfterAveragingChecks()
    {
        var score = HealthScoring.Calculate([
            Result("A", HealthCategory.Email, HealthStatus.Critical),
            Result("B", HealthCategory.Configuration, HealthStatus.Healthy)]);
        Assert.That(score.Score, Is.EqualTo(42.86m));
    }

    [Test]
    public void DuplicateResultsAreRejected()
        => Assert.Throws<ArgumentException>(() => HealthScoring.Calculate([
            Result("A", HealthCategory.Email, HealthStatus.Healthy),
            Result("a", HealthCategory.Email, HealthStatus.Critical)]));
}
