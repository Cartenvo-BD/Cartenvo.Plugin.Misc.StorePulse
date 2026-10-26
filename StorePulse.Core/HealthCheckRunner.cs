namespace StorePulse.Core;

/// <summary>Runs checks sequentially so dependent observations retain their declared order.</summary>
public sealed class HealthCheckRunner(TimeProvider? timeProvider = null)
{
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;

    public async Task<IReadOnlyList<HealthCheckResult>> RunAsync(HealthCheckRegistry registry,
        HealthCheckContext context, TimeSpan budget, CancellationToken cancellationToken,
        IReadOnlySet<string>? disabledIds = null, HealthCategory? category = null, TimeSpan? checkTimeout = null)
    {
        if (budget <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(budget));
        if (checkTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(checkTimeout));

        var started = _time.GetTimestamp();
        var results = new List<HealthCheckResult>();
        foreach (var check in registry.Select(disabledIds ?? new HashSet<string>(), category))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = Unknown(check, context);
            // Never abandon work still using a request's services or database connection.
            var remaining = budget - _time.GetElapsedTime(started);
            if (remaining > TimeSpan.Zero)
            {
                // A check that runs out of time is cancelled (its query too) and reported as Unknown.
                using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                limit.CancelAfter(checkTimeout is { } perCheck && perCheck < remaining ? perCheck : remaining);
                try
                {
                    var observed = await check.ExecuteAsync(context, limit.Token);
                    if (observed.CheckId != check.CheckId || observed.Category != check.Category ||
                        observed.Scope != check.Scope || observed.Metrics == null)
                        throw new InvalidOperationException("Diagnostic result does not match its registration.");
                    result = observed;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                catch { /* Source failures become Unknown; exception text never enters reports. */ }
            }
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(result);
        }
        return results.AsReadOnly();
    }

    private static HealthCheckResult Unknown(IHealthCheck check, HealthCheckContext context) => new(
        check.CheckId, check.Category, check.Scope, HealthStatus.Unknown,
        $"Plugins.Misc.StorePulse.{check.CheckId}.Title", "Plugins.Misc.StorePulse.Status.Unknown",
        $"Plugins.Misc.StorePulse.{check.CheckId}.Action", context.ObservedAtUtc,
        new Dictionary<string, decimal>());
}
