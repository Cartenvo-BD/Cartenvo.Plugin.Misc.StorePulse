using System.Net.Mail;

namespace StorePulse.Core;

/// <summary>Threshold rules for checks added from community research (see storepulse.md section 17).</summary>
public static class CommunityRules
{
    /// <summary>Healthy below the threshold, Warning at or above it.</summary>
    public static HealthStatus AtLeast(long observed, long threshold)
    {
        if (threshold < 1)
            throw new ArgumentOutOfRangeException(nameof(threshold));
        return observed >= threshold ? HealthStatus.Warning : HealthStatus.Healthy;
    }

    /// <summary>Many new guests per order usually means crawlers, not shoppers. It is a hint only.</summary>
    public static HealthStatus BotTraffic(long guestsLast24Hours, long ordersLast24Hours,
        long minimumGuests = 1000, long guestsPerOrder = 200)
    {
        if (guestsLast24Hours < minimumGuests)
            return HealthStatus.Healthy;
        return guestsLast24Hours > Math.Max(1, ordersLast24Hours) * guestsPerOrder
            ? HealthStatus.Warning : HealthStatus.Healthy;
    }

    /// <summary>Unknown when no certificate was read. Days are whole days until NotAfter.</summary>
    public static HealthStatus CertificateExpiry(double? daysLeft, int warningDays = 30, int criticalDays = 7)
    {
        if (!daysLeft.HasValue)
            return HealthStatus.Unknown;
        return daysLeft.Value <= criticalDays ? HealthStatus.Critical
            : daysLeft.Value <= warningDays ? HealthStatus.Warning : HealthStatus.Healthy;
    }

    public sealed record EmailAccountObservation(string Email, string Host, int Port);

    /// <summary>Counts accounts that cannot send. A missing default account is Critical.</summary>
    public static (HealthStatus Status, int InvalidAccounts) EmailAccounts(
        IReadOnlyCollection<EmailAccountObservation> accounts, bool defaultAccountExists)
    {
        var invalid = accounts.Count(account => !IsValidAddress(account.Email) ||
            string.IsNullOrWhiteSpace(account.Host) || account.Port is < 1 or > 65535);
        if (!defaultAccountExists)
            return (HealthStatus.Critical, invalid);
        return (invalid > 0 ? HealthStatus.Warning : HealthStatus.Healthy, invalid);
    }

    private static bool IsValidAddress(string email)
        => !string.IsNullOrWhiteSpace(email) && MailAddress.TryCreate(email, out var parsed) &&
           string.Equals(parsed.Address, email.Trim(), StringComparison.OrdinalIgnoreCase);
}

/// <summary>A nopCommerce bug that affects a known range of releases.</summary>
public sealed record KnownIssue(string Id, string Title, string FirstAffected, string FixedIn, string SourceUrl)
{
    public bool Affects(string installedVersion)
    {
        if (!KnownIssues.TryParse(installedVersion, out var installed) ||
            !KnownIssues.TryParse(FirstAffected, out var first) || !KnownIssues.TryParse(FixedIn, out var fixedIn))
            return false;
        return installed >= first && installed < fixedIn;
    }
}

/// <summary>Bundled locally; StorePulse never downloads advisories. Each entry is verified against GitHub.</summary>
public static class KnownIssues
{
    public static readonly IReadOnlyList<KnownIssue> All =
    [
        new("NOP-8105", "Scheduled task runs can be skipped", "4.90.0", "4.90.4",
            "https://github.com/nopSolutions/nopCommerce/issues/8105"),
        new("NOP-8271", "Exceptions during application startup are ignored", "4.90.0", "4.90.7",
            "https://github.com/nopSolutions/nopCommerce/issues/8271")
    ];

    public static IReadOnlyList<KnownIssue> For(string installedVersion)
        => All.Where(issue => issue.Affects(installedVersion)).ToArray();

    public static bool TryParse(string value, out Version version)
    {
        version = null!;
        if (string.IsNullOrWhiteSpace(value))
            return false;
        var parts = value.Trim().Split('.');
        if (parts.Length == 2)
            value += ".0";
        return Version.TryParse(value.Trim(), out version!);
    }
}

public sealed record ScanChange(IReadOnlyList<HealthCheckResult> NewProblems,
    IReadOnlyList<HealthCheckResult> Resolved, IReadOnlyList<HealthCheckResult> Worse);

/// <summary>Compares two scans, for "what changed since the last scan" and deployment checks.</summary>
public static class ScanComparison
{
    public static bool IsProblem(HealthStatus status) => status is HealthStatus.Warning or HealthStatus.Critical;

    public static ScanChange Compare(IReadOnlyList<HealthCheckResult> previous, IReadOnlyList<HealthCheckResult> current)
    {
        var before = previous.GroupBy(result => result.CheckId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Status, StringComparer.OrdinalIgnoreCase);
        var newProblems = current.Where(result => IsProblem(result.Status) &&
            (!before.TryGetValue(result.CheckId, out var old) || !IsProblem(old))).ToArray();
        var worse = current.Where(result => result.Status == HealthStatus.Critical &&
            before.TryGetValue(result.CheckId, out var old) && old == HealthStatus.Warning).ToArray();
        var now = current.ToDictionary(result => result.CheckId, StringComparer.OrdinalIgnoreCase);
        var resolved = previous.Where(result => IsProblem(result.Status) &&
            now.TryGetValue(result.CheckId, out var latest) && latest.Status == HealthStatus.Healthy).ToArray();
        return new ScanChange(newProblems, resolved, worse);
    }
}

/// <summary>Special metric keys that change how evidence is shown rather than being shown themselves.</summary>
public static class MetricFlags
{
    /// <summary>Counting stopped at a limit, so each count in the result is a minimum ("100,000+").</summary>
    public const string CountIsMinimum = "CountIsMinimum";

    public static bool IsFlag(string key) => key == CountIsMinimum;

    public static string Format(IReadOnlyDictionary<string, decimal> metrics, decimal value, string format,
        IFormatProvider? provider = null)
        => value.ToString(format, provider) + (metrics.ContainsKey(CountIsMinimum) ? "+" : "");
}
