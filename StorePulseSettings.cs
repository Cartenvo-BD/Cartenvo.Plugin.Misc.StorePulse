using Nop.Core.Configuration;

namespace Cartenvo.Plugin.Misc.StorePulse;

public class StorePulseSettings : ISettings
{
    public bool ScheduledScanningEnabled { get; set; }
    public int ScheduledScanIntervalHours { get; set; } = 24;
    public int HistoryRetentionDays { get; set; } = 30;
    public int TaskGraceMinutes { get; set; } = 10;
    public int TaskOverdueMultiplier { get; set; } = 3;
    public int EmailWarningMinutes { get; set; } = 30;
    public int MaximumRecordsPerCheck { get; set; } = 200;
    public int EmailBacklogWarningCount { get; set; } = 100;

    // Database hygiene thresholds (record counts).
    public int GuestWarningCount { get; set; } = 100000;
    public int LogWarningCount { get; set; } = 100000;
    public int ActivityLogWarningCount { get; set; } = 500000;
    public int SentEmailWarningCount { get; set; } = 50000;
    public int OldCartWarningCount { get; set; } = 50000;

    // Optional groups.
    public bool CatalogChecksEnabled { get; set; } = true;
    /// <summary>Off by default. Allows the SSL check to connect to configured store hosts only.</summary>
    public bool NetworkChecksEnabled { get; set; }
}
