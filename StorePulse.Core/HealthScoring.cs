namespace StorePulse.Core;

public sealed record HealthScore(decimal? Score, decimal CoveragePercent, int CriticalCount);

public static class HealthScoring
{
    private static readonly IReadOnlyDictionary<HealthCategory, decimal> Weights =
        new Dictionary<HealthCategory, decimal>
        {
            [HealthCategory.ScheduledTasks] = 20,
            [HealthCategory.Email] = 20,
            [HealthCategory.ApplicationErrors] = 20,
            [HealthCategory.Configuration] = 15,
            [HealthCategory.Plugins] = 15,
            [HealthCategory.Environment] = 10,
            [HealthCategory.Database] = 10
        };

    // Coverage is weighted by category, excludes NotApplicable, and includes Unknown.
    // Categories without applicable registered checks do not contribute to either denominator.
    public static HealthScore Calculate(IEnumerable<HealthCheckResult> results)
    {
        var items = results.ToArray();
        if (items.Select(result => result.CheckId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != items.Length)
            throw new ArgumentException("Score inputs must contain unique check identifiers.", nameof(results));
        if (items.Any(result => !Enum.IsDefined(result.Status) || !Enum.IsDefined(result.Category)))
            throw new ArgumentException("Score inputs contain an invalid status or category.", nameof(results));

        decimal scoreSum = 0, evaluatedWeight = 0, coverageSum = 0, applicableWeight = 0;
        foreach (var (category, weight) in Weights)
        {
            var applicable = items.Where(result => result.Category == category &&
                result.Status != HealthStatus.NotApplicable).ToArray();
            if (applicable.Length == 0)
                continue;
            var evaluated = applicable.Where(result => result.Status != HealthStatus.Unknown).ToArray();
            applicableWeight += weight;
            coverageSum += weight * evaluated.Length / applicable.Length;
            if (evaluated.Length == 0)
                continue;
            scoreSum += weight * evaluated.Average(result => result.Status switch
            {
                HealthStatus.Healthy => 100m,
                HealthStatus.Warning => 60m,
                _ => 0m
            });
            evaluatedWeight += weight;
        }

        return Build(scoreSum, evaluatedWeight, coverageSum, applicableWeight, items);
    }

    /// <summary>Catalog quality is reported separately so it can never hide an operational problem.</summary>
    public static decimal? CalculateCatalog(IEnumerable<HealthCheckResult> results)
    {
        var evaluated = results.Where(result => result.Category == HealthCategory.Catalog &&
            result.Status is HealthStatus.Healthy or HealthStatus.Warning or HealthStatus.Critical).ToArray();
        return evaluated.Length == 0 ? null : Math.Round(evaluated.Average(result => result.Status switch
        {
            HealthStatus.Healthy => 100m,
            HealthStatus.Warning => 60m,
            _ => 0m
        }), 0);
    }

    private static HealthScore Build(decimal scoreSum, decimal evaluatedWeight, decimal coverageSum,
        decimal applicableWeight, HealthCheckResult[] items)
    {
        return new HealthScore(evaluatedWeight == 0 ? null : Math.Round(scoreSum / evaluatedWeight, 2),
            applicableWeight == 0 ? 0 : Math.Round(coverageSum / applicableWeight * 100, 2),
            items.Count(result => result.Category != HealthCategory.Catalog && result.Status == HealthStatus.Critical));
    }
}
