using System.ComponentModel.DataAnnotations;

namespace Cartenvo.Plugin.Misc.StorePulse.Models;

public class SettingsModel
{
    public bool ScheduledScanningEnabled { get; set; }
    [Range(1, 168)] public int ScheduledScanIntervalHours { get; set; } = 24;
    [Range(1, 365)] public int HistoryRetentionDays { get; set; } = 30;
    [Range(1, 1440)] public int TaskGraceMinutes { get; set; } = 10;
    [Range(1, 20)] public int TaskOverdueMultiplier { get; set; } = 3;
    [Range(1, 1440)] public int EmailWarningMinutes { get; set; } = 30;
    [Range(1, 100000)] public int EmailBacklogWarningCount { get; set; } = 100;
    [Range(10, 1000)] public int MaximumRecordsPerCheck { get; set; } = 200;
    [Range(1, int.MaxValue)] public int GuestWarningCount { get; set; } = 100000;
    [Range(1, int.MaxValue)] public int LogWarningCount { get; set; } = 100000;
    [Range(1, int.MaxValue)] public int ActivityLogWarningCount { get; set; } = 500000;
    [Range(1, int.MaxValue)] public int SentEmailWarningCount { get; set; } = 50000;
    [Range(1, int.MaxValue)] public int OldCartWarningCount { get; set; } = 50000;
    public bool CatalogChecksEnabled { get; set; } = true;
    /// <summary>Catalog check ids that are ticked. Unticked ones are saved as disabled.</summary>
    public List<string> EnabledCatalogChecks { get; set; } = [];
    public bool NetworkChecksEnabled { get; set; }
    public bool Saved { get; set; }
}
