namespace StorePulse.Core;

public enum HealthStatus { Healthy, Warning, Critical, NotApplicable, Unknown }
// Append new values only: stored scan history serializes categories as numbers.
public enum HealthCategory { ScheduledTasks, Email, ApplicationErrors, Configuration, Plugins, Environment, Catalog, Database }
public enum HealthScope { System, Store }

// Evidence must be produced from approved numeric/configuration fields by each adapter.
// Raw logs, exception messages, URLs and customer records must never enter this contract.
public sealed record HealthCheckResult(
    string CheckId,
    HealthCategory Category,
    HealthScope Scope,
    HealthStatus Status,
    string TitleResourceKey,
    string SummaryResourceKey,
    string RecommendationResourceKey,
    DateTimeOffset CheckedAtUtc,
    IReadOnlyDictionary<string, decimal> Metrics);

public sealed record HealthCheckContext(int StoreId, DateTimeOffset ObservedAtUtc);

public interface IHealthCheck
{
    string CheckId { get; }
    HealthCategory Category { get; }
    HealthScope Scope => HealthScope.System;
    Task<HealthCheckResult> ExecuteAsync(HealthCheckContext context, CancellationToken cancellationToken);
}

public sealed class HealthCheckRegistry
{
    private readonly IReadOnlyList<IHealthCheck> _checks;

    public HealthCheckRegistry(IEnumerable<IHealthCheck> checks)
    {
        var items = checks.ToArray();
        if (items.Any(check => string.IsNullOrWhiteSpace(check.CheckId)))
            throw new ArgumentException("Diagnostic identifiers cannot be empty.", nameof(checks));
        if (items.Select(check => check.CheckId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != items.Length)
            throw new ArgumentException("Diagnostic identifiers must be unique.", nameof(checks));
        _checks = Array.AsReadOnly(items);
    }

    public IReadOnlyList<IHealthCheck> Select(IReadOnlySet<string> disabledIds, HealthCategory? category = null)
        => Array.AsReadOnly(_checks.Where(check =>
            !disabledIds.Any(id => StringComparer.OrdinalIgnoreCase.Equals(id, check.CheckId)) &&
            (!category.HasValue || check.Category == category)).ToArray());
}
