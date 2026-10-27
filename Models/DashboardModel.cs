using Cartenvo.Plugin.Misc.StorePulse.Domain;
using StorePulse.Core;

namespace Cartenvo.Plugin.Misc.StorePulse.Models;

public class DashboardModel
{
    public IList<StorePulseScan> History { get; set; } = [];
    public IReadOnlyList<HealthCheckResult> Findings { get; set; } = [];
    public HealthScore Score { get; set; } = new(null, 0, 0);
    public decimal? CatalogScore { get; set; }
    public bool CanRun { get; set; }
    public bool CanExport { get; set; }
    public bool CanManage { get; set; }
    public bool Busy { get; set; }
    public int SelectedScanId { get; set; }
    public bool IsLatest { get; set; } = true;
    public DateTime? CompletedOnUtc { get; set; }
    public IList<PluginInventoryItem> Inventory { get; set; } = [];
    public bool InventoryTruncated { get; set; }
    /// <summary>Null when there is no previous scan to compare with.</summary>
    public ScanChange Changes { get; set; }
    public IList<TrendPoint> Trend { get; set; } = [];
    public IReadOnlyList<KnownIssue> KnownIssues { get; set; } = [];
    public string NopVersion { get; set; }
    public string StoreLocation { get; set; }

    public int Count(HealthStatus status) => Findings.Count(f => f.Status == status && f.Category != HealthCategory.Catalog);

    public bool Has(string checkId, HealthStatus status) => Findings.Any(f => f.CheckId == checkId && f.Status == status);

    /// <summary>Critical before Warning; operational problems before catalog quality.</summary>
    public static IEnumerable<HealthCheckResult> Prioritize(IEnumerable<HealthCheckResult> findings) => findings
        .Where(f => ScanComparison.IsProblem(f.Status))
        .OrderBy(f => f.Status == HealthStatus.Critical ? 0 : 1)
        .ThenBy(f => f.Category == HealthCategory.Catalog ? 1 : 0)
        .ThenBy(f => CategoryOrder.IndexOf(f.Category));

    public static readonly List<HealthCategory> CategoryOrder =
    [
        HealthCategory.Email, HealthCategory.ScheduledTasks, HealthCategory.ApplicationErrors,
        HealthCategory.Database, HealthCategory.Configuration, HealthCategory.Plugins,
        HealthCategory.Environment, HealthCategory.Catalog
    ];

    public static string Icon(HealthCategory category) => category switch
    {
        HealthCategory.ScheduledTasks => "fas fa-clock",
        HealthCategory.Email => "fas fa-envelope",
        HealthCategory.ApplicationErrors => "fas fa-bug",
        HealthCategory.Configuration => "fas fa-shield-alt",
        HealthCategory.Plugins => "fas fa-puzzle-piece",
        HealthCategory.Database => "fas fa-database",
        HealthCategory.Catalog => "fas fa-tags",
        _ => "fas fa-server"
    };

    public static string Tone(HealthStatus status) => status switch
    {
        HealthStatus.Critical => "critical",
        HealthStatus.Warning => "warning",
        HealthStatus.Healthy => "healthy",
        _ => "muted"
    };

    public static string StatusIcon(HealthStatus status) => status switch
    {
        HealthStatus.Critical => "fas fa-times-circle",
        HealthStatus.Warning => "fas fa-exclamation-triangle",
        HealthStatus.Healthy => "fas fa-check-circle",
        HealthStatus.NotApplicable => "fas fa-minus-circle",
        _ => "fas fa-question-circle"
    };
}

public sealed record TrendPoint(int ScanId, DateTime CompletedOnUtc, decimal? Score, int Problems);

public class DashboardCardModel
{
    public bool HasScan { get; set; }
    public DateTime? CompletedOnUtc { get; set; }
    public HealthScore Score { get; set; } = new(null, 0, 0);
    public IList<HealthCheckResult> Critical { get; set; } = [];
    public int Warnings { get; set; }
    public IList<HealthCheckResult> TopProblems { get; set; } = [];
}

public class UpgradeModel
{
    public string CurrentVersion { get; set; }
    public string TargetVersion { get; set; }
    public IList<string> TargetOptions { get; set; } = [];
    public IList<UpgradeItem> Plugins { get; set; } = [];
    public int ReadyCount => Plugins.Count(p => p.Ready || p.Bundled);

    /// <summary>True when the target is 5.00 or later and the store is still on 4.x.</summary>
    public bool ShowReadiness { get; set; }
    public IReadOnlyList<Services.ReadinessItem> Readiness { get; set; } = [];
    /// <summary>Upgrade guide on cartenvo.com, or null when guides are off.</summary>
    public string GuideUrl { get; set; }

    public int ReadinessCount(Services.ReadinessStatus status) => Readiness.Count(item => item.Status == status);

    public static string Tone(Services.ReadinessStatus status) => status switch
    {
        Services.ReadinessStatus.ActionNeeded => "critical",
        Services.ReadinessStatus.Check => "warning",
        Services.ReadinessStatus.Ready => "healthy",
        _ => "muted"
    };

    public static string Icon(Services.ReadinessStatus status) => status switch
    {
        Services.ReadinessStatus.ActionNeeded => "fas fa-times-circle",
        Services.ReadinessStatus.Check => "fas fa-exclamation-triangle",
        Services.ReadinessStatus.Ready => "fas fa-check-circle",
        _ => "fas fa-question-circle"
    };
}

public sealed record UpgradeItem(string FriendlyName, string SystemName, string Version, string Author,
    string DeclaredVersions, bool Ready, bool Bundled);
