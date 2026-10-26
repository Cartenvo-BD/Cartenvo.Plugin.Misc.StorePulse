namespace StorePulse.Core;

public sealed record TaskObservation(bool Enabled, int IntervalSeconds,
    DateTimeOffset? LastEnabledUtc, DateTimeOffset? LastStartUtc);

public static class TaskHealthRules
{
    public static HealthStatus Overdue(TaskObservation task, DateTimeOffset now,
        TimeSpan minimumGrace, int intervalMultiplier)
    {
        if (minimumGrace <= TimeSpan.Zero || intervalMultiplier < 1)
            throw new ArgumentOutOfRangeException(nameof(minimumGrace));
        if (!task.Enabled)
            return HealthStatus.NotApplicable;
        if (task.IntervalSeconds <= 0)
            return HealthStatus.Unknown; // TASK-006 owns invalid intervals.
        if (task.LastEnabledUtc > now || task.LastStartUtc > now)
            return HealthStatus.Unknown;

        var thresholdSeconds = Math.Max(minimumGrace.TotalSeconds, (double)task.IntervalSeconds * intervalMultiplier);
        if (task.LastEnabledUtc.HasValue && (now - task.LastEnabledUtc.Value).TotalSeconds <= thresholdSeconds)
            return HealthStatus.NotApplicable; // Still inside enablement grace.
        if (!task.LastStartUtc.HasValue)
            return HealthStatus.Unknown; // TASK-003 owns never-started tasks.
        return (now - task.LastStartUtc.Value).TotalSeconds > thresholdSeconds
            ? HealthStatus.Warning : HealthStatus.Healthy;
    }

    public static HealthStatus NeverExecuted(TaskObservation task, DateTimeOffset now, TimeSpan minimumGrace)
    {
        if (minimumGrace <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(minimumGrace));
        if (!task.Enabled)
            return HealthStatus.NotApplicable;
        if (task.LastEnabledUtc > now || task.LastStartUtc > now)
            return HealthStatus.Unknown;
        if (task.LastStartUtc.HasValue)
            return HealthStatus.NotApplicable;
        if (!task.LastEnabledUtc.HasValue || task.IntervalSeconds <= 0)
            return HealthStatus.Unknown;
        var graceSeconds = Math.Max(minimumGrace.TotalSeconds, task.IntervalSeconds);
        return (now - task.LastEnabledUtc.Value).TotalSeconds > graceSeconds
            ? HealthStatus.Warning : HealthStatus.NotApplicable;
    }
}
