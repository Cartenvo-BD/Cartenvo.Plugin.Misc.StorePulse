using NUnit.Framework;
using StorePulse.Core;

namespace StorePulse.Core.Tests;

public class CommunityRulesTests
{
    private static HealthCheckResult Result(string id, HealthStatus status, HealthCategory category = HealthCategory.Email)
        => new(id, category, HealthScope.System, status, "t", "s", "a", DateTimeOffset.UtcNow, new Dictionary<string, decimal>());

    [TestCase(99, 100, HealthStatus.Healthy)]
    [TestCase(100, 100, HealthStatus.Warning)]
    [TestCase(5_000_000, 100_000, HealthStatus.Warning)]
    public void ThresholdWarnsAtOrAbove(long observed, long threshold, HealthStatus expected)
        => Assert.That(CommunityRules.AtLeast(observed, threshold), Is.EqualTo(expected));

    [Test]
    public void ThresholdMustBePositive()
        => Assert.Throws<ArgumentOutOfRangeException>(() => CommunityRules.AtLeast(1, 0));

    [TestCase(500, 0, HealthStatus.Healthy)]       // too few guests to judge
    [TestCase(5000, 0, HealthStatus.Warning)]      // many guests, no orders
    [TestCase(5000, 100, HealthStatus.Healthy)]    // 50 guests per order is normal
    [TestCase(50000, 100, HealthStatus.Warning)]   // 500 guests per order
    public void BotTrafficCompareGuestsWithOrders(long guests, long orders, HealthStatus expected)
        => Assert.That(CommunityRules.BotTraffic(guests, orders), Is.EqualTo(expected));

    [TestCase(null, HealthStatus.Unknown)]
    [TestCase(90.0, HealthStatus.Healthy)]
    [TestCase(30.0, HealthStatus.Warning)]
    [TestCase(7.0, HealthStatus.Critical)]
    [TestCase(-2.0, HealthStatus.Critical)]
    public void CertificateExpiryThresholds(double? days, HealthStatus expected)
        => Assert.That(CommunityRules.CertificateExpiry(days), Is.EqualTo(expected));

    [Test]
    public void EmailAccountsFlagInvalidAndMissingDefault()
    {
        var good = new CommunityRules.EmailAccountObservation("shop@example.com", "smtp.example.com", 587);
        var badAddress = good with { Email = "not-an-email" };
        var noHost = good with { Host = " " };
        var badPort = good with { Port = 0 };
        Assert.That(CommunityRules.EmailAccounts([good], true), Is.EqualTo((HealthStatus.Healthy, 0)));
        Assert.That(CommunityRules.EmailAccounts([good, badAddress, noHost, badPort], true), Is.EqualTo((HealthStatus.Warning, 3)));
        Assert.That(CommunityRules.EmailAccounts([good], false).Status, Is.EqualTo(HealthStatus.Critical));
    }

    [TestCase("4.90.3", 2)]
    [TestCase("4.90.4", 1)]
    [TestCase("4.90.6", 1)]
    [TestCase("4.90.7", 0)]
    [TestCase("4.90.8", 0)]
    [TestCase("4.80.9", 0)]
    [TestCase("garbage", 0)]
    public void KnownIssuesMatchInstalledVersion(string version, int expected)
        => Assert.That(KnownIssues.For(version).Count, Is.EqualTo(expected));

    [Test]
    public void TwoPartVersionsParse()
    {
        Assert.That(KnownIssues.TryParse("5.00", out var parsed), Is.True);
        Assert.That(parsed, Is.EqualTo(new Version(5, 0, 0)));
    }

    [Test]
    public void ComparisonFindsNewResolvedAndWorse()
    {
        var before = new[] { Result("A", HealthStatus.Warning), Result("B", HealthStatus.Healthy), Result("C", HealthStatus.Warning) };
        var after = new[] { Result("A", HealthStatus.Healthy), Result("B", HealthStatus.Critical), Result("C", HealthStatus.Critical),
            Result("D", HealthStatus.Warning) };
        var change = ScanComparison.Compare(before, after);
        Assert.That(change.Resolved.Select(r => r.CheckId), Is.EquivalentTo(new[] { "A" }));
        Assert.That(change.NewProblems.Select(r => r.CheckId), Is.EquivalentTo(new[] { "B", "D" }));
        Assert.That(change.Worse.Select(r => r.CheckId), Is.EquivalentTo(new[] { "C" }));
    }

    [Test]
    public void UnknownAfterProblemIsNotReportedAsFixed()
    {
        var change = ScanComparison.Compare([Result("A", HealthStatus.Warning)], [Result("A", HealthStatus.Unknown)]);
        Assert.That(change.Resolved, Is.Empty);
    }

    [Test]
    public void CatalogScoreIsSeparateFromHealthScore()
    {
        var results = new[]
        {
            Result("EMAIL-001", HealthStatus.Healthy),
            Result("CAT-001", HealthStatus.Warning, HealthCategory.Catalog),
            Result("CAT-003", HealthStatus.Healthy, HealthCategory.Catalog)
        };
        Assert.That(HealthScoring.Calculate(results).Score, Is.EqualTo(100));
        Assert.That(HealthScoring.CalculateCatalog(results), Is.EqualTo(80));
        Assert.That(HealthScoring.CalculateCatalog([Result("EMAIL-001", HealthStatus.Healthy)]), Is.Null);
    }

    [Test]
    public void DatabaseCategoryCountsTowardScore()
    {
        var score = HealthScoring.Calculate([Result("DB-001", HealthStatus.Warning, HealthCategory.Database)]);
        Assert.That(score.Score, Is.EqualTo(60));
    }
}
