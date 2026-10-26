namespace Cartenvo.Plugin.Misc.StorePulse.Infrastructure;

/// <summary>Where each finding sends the administrator: the matching admin page and a public help article.</summary>
public static class StorePulseLinks
{
    public const string Website = "https://cartenvo.com/";
    public const string HelpBase = "https://cartenvo.com/storepulse/checks/";

    public static string Help(string checkId) => HelpBase + checkId.ToLowerInvariant();

    /// <summary>Relative admin path (no leading slash) for the page where the finding can be fixed.</summary>
    public static string AdminPath(string checkId) => checkId switch
    {
        "EMAIL-005" => "Admin/EmailAccount/List",
        "CONFIG-008" => "Admin/Common/Warnings",
        "SEC-001" => "Admin/Common/Warnings",
        "SEC-002" => "Admin/Setting/GeneralCommon",
        "SEC-003" => "Admin/Customer/List",
        "DB-003" => "Admin/ScheduleTask/List",
        "DB-004" => "Admin/Log/List",
        "DB-005" => "Admin/ActivityLog/ActivityLogs",
        "DB-006" => "Admin/QueuedEmail/List",
        "PLUGIN-004" => "Admin/Plugin/List",
        _ when checkId.StartsWith("TASK-") => "Admin/ScheduleTask/List",
        _ when checkId.StartsWith("EMAIL-") => "Admin/QueuedEmail/List",
        _ when checkId.StartsWith("LOG-") => "Admin/Log/List",
        _ when checkId.StartsWith("CONFIG-") => "Admin/Store/List",
        _ when checkId.StartsWith("PLUGIN-") => "Admin/Plugin/List",
        _ when checkId.StartsWith("DB-") => "Admin/Common/Maintenance",
        _ when checkId.StartsWith("CAT-") => "Admin/Product/List",
        _ => null
    };
}
