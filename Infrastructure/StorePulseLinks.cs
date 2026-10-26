namespace Cartenvo.Plugin.Misc.StorePulse.Infrastructure;

/// <summary>Where each finding sends the administrator: the matching admin page and a public help article.</summary>
public static class StorePulseLinks
{
    public const string Website = "https://cartenvo.com/";
    public const string GuideBase = "https://cartenvo.com/plugins/storepulse/guides/";

    /// <summary>
    /// Set to false to hide every link to the website guides, for example if they are taken offline.
    /// The in-plugin help popup works either way.
    /// </summary>
    public const bool GuidesPublished = true;

    /// <summary>Tells the website's analytics that a visit came from the plugin. Nothing about the store is sent.</summary>
    private const string Tracking = "?utm_source=storepulse&utm_medium=plugin&utm_campaign=help";

    /// <summary>Website guide for each topic. One guide covers several related checks, each with its own anchor.</summary>
    public static string GuideSlug(string checkId) => checkId switch
    {
        "EMAIL-005" => "nopcommerce-email-account-setup",
        "CONFIG-001" or "CONFIG-002" or "CONFIG-004" => "nopcommerce-store-url-https-ssl",
        "CONFIG-008" => "nopcommerce-system-warnings",
        "PLUGIN-004" => "nopcommerce-plugin-load-errors",
        "DB-001" or "DB-002" or "DB-003" => "nopcommerce-guest-customers",
        _ when checkId.StartsWith("TASK-") => "nopcommerce-scheduled-tasks-not-running",
        _ when checkId.StartsWith("EMAIL-") => "nopcommerce-emails-not-sending",
        _ when checkId.StartsWith("LOG-") => "nopcommerce-error-log",
        _ when checkId.StartsWith("SEC-") => "nopcommerce-security-checklist",
        _ when checkId.StartsWith("PLUGIN-") => "nopcommerce-plugin-compatibility",
        _ when checkId.StartsWith("DB-") => "nopcommerce-database-cleanup",
        _ when checkId.StartsWith("CAT-") => "nopcommerce-catalog-quality",
        _ => null
    };

    /// <summary>Full guide on cartenvo.com for a check, or null when guides are off or the check has none.</summary>
    public static string Guide(string checkId)
    {
        if (!GuidesPublished || string.IsNullOrEmpty(checkId) || GuideSlug(checkId) is not { } slug)
            return null;
        return GuideBase + slug + Tracking + "#" + checkId.ToLowerInvariant();
    }

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
        "CAT-009" => "Admin/Common/SeNames",
        "CAT-016" or "CAT-017" => "Admin/Category/List",
        "CAT-019" => "Admin/ProductReview/List",
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
