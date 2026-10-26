namespace Cartenvo.Plugin.Misc.StorePulse.Infrastructure;

public static class StorePulseResources
{
    /// <summary>Id, title, why it matters, what to do.</summary>
    public static readonly (string Id, string Title, string Why, string Action)[] Checks =
    [
        ("TASK-001", "Scheduled tasks running on time",
            "Background tasks send emails, clean up data and update rates. When they stop, problems pile up quietly.",
            "Open Schedule tasks and look for tasks whose last start is far in the past. Run one manually to see if it works, then check System → Log for errors."),
        ("TASK-002", "Repeated task failures",
            "nopCommerce does not keep a history of task failures, so StorePulse cannot count them.",
            "Check System → Log for task errors. For full run history, a task-history plugin can help."),
        ("TASK-003", "Tasks that never started",
            "An enabled task that has never run usually means the scheduler is not reaching the site.",
            "Check that the store URL is correct and the site is reachable from the server. Then restart the application."),
        ("TASK-006", "Task interval settings",
            "A task with an interval of 0 seconds will never run.",
            "Open Schedule tasks and set a valid interval for each enabled task."),
        ("EMAIL-001", "Emails waiting to be sent",
            "Customers may not receive order confirmations, password resets or notifications.",
            "Open Message queue to see what is waiting. Check that the Send emails task is enabled and running."),
        ("EMAIL-002", "Oldest waiting email",
            "Emails that wait a long time often mean sending has stopped.",
            "Open Message queue, sort by date, and check the Send emails task and your email account settings."),
        ("EMAIL-003", "Emails that failed 3 times",
            "nopCommerce stops retrying after 3 failed attempts, so these emails will never be sent on their own.",
            "Fix the email account (host, port, password), then requeue these messages from Message queue."),
        ("EMAIL-004", "Email sending is stalled",
            "Emails are waiting, but the task that sends them is disabled, missing or overdue.",
            "Open Schedule tasks, enable \"Send emails\" and click Run now. Then check Message queue again."),
        ("EMAIL-005", "Email account setup",
            "A missing default account or an account without a valid host, port or address cannot send email.",
            "Open Email accounts, set a default account, and use \"Send test email\" to confirm it works."),
        ("EMAIL-006", "Emails linked to a deleted account",
            "Queued emails keep the account they were created with. If that account was deleted, they can never be sent.",
            "Open Message queue and delete these emails. Queued emails can't be moved to another account."),
        ("LOG-001", "Fatal errors in the last 24 hours",
            "Fatal errors often mean a page or feature stopped working for customers.",
            "Open System → Log, filter by Fatal, and fix the most recent error first."),
        ("LOG-002", "Sudden increase in errors",
            "A sharp jump in errors often follows a deployment, plugin install or outage.",
            "Open System → Log and compare errors from the last hour with the hour before. Think about what changed recently."),
        ("LOG-003", "The same error repeating",
            "One problem repeating many times usually has a single fix.",
            "Open System → Log and look for the message that appears most often."),
        ("LOG-005", "Errors in the last 24 hours",
            "Some errors are harmless, but a steady stream is worth a look.",
            "Open System → Log, filter by Error, and review the newest entries."),
        ("LOG-006", "How complete the logs are",
            "StorePulse can only see what nopCommerce logged. Cleared or missing logs hide problems.",
            "No action needed. This reminds you that a quiet log does not prove the store is error-free."),
        ("CONFIG-001", "Store URL",
            "A wrong store URL breaks links in emails, redirects and scheduled tasks.",
            "Open Configuration → Stores and enter the full address, for example https://www.example.com/."),
        ("CONFIG-002", "HTTPS setting matches the URL",
            "If the URL says https but SSL is off (or the other way round), customers can get redirect loops or security warnings.",
            "Open Configuration → Stores and make \"SSL enabled\" match the store URL."),
        ("CONFIG-004", "SSL certificate expiry",
            "An expired certificate shows a security warning to every visitor and stops sales.",
            "Renew the certificate with your host before it expires. This check is off until you enable network checks in Settings."),
        ("CONFIG-008", "nopCommerce system warnings",
            "nopCommerce has its own warnings page. StorePulse counts its failures and warnings here so you see everything in one place.",
            "Open System → Warnings and fix the items marked as failures first."),
        ("SEC-001", "Detailed error pages",
            "Showing full error details to visitors can reveal file paths and code to attackers.",
            "Set DisplayFullErrorStack to false in App_Data/appsettings.json on live stores, then restart."),
        ("SEC-002", "CAPTCHA on registration",
            "Without CAPTCHA, bots can register fake accounts and send spam through your forms.",
            "Open Settings → General settings → CAPTCHA, enable it and turn it on for the registration page."),
        ("SEC-003", "Sample admin account",
            "The installer's sample admin email (admin@yourStore.com) is well known and is a common attack target.",
            "Change the administrator's email to a real address and set a strong password."),
        ("PLUGIN-002", "Plugins that support this version",
            "Plugins that don't list this nopCommerce version may fail or behave oddly.",
            "Check Local plugins and get updated versions from each vendor. Listed support is the vendor's claim, not a guarantee."),
        ("PLUGIN-003", "Missing plugin dependencies",
            "A plugin that depends on another plugin will not work without it.",
            "Install the missing plugin, or uninstall the plugin that needs it."),
        ("PLUGIN-004", "Plugins that failed to load",
            "\"Method not found\" or \"Could not load type\" errors usually mean a plugin was built for a different nopCommerce version.",
            "Open Local plugins and System → Log. Update or remove the plugin named in the error."),
        ("PLUGIN-006", "Known nopCommerce bugs in your version",
            "Your installed version has bugs that were fixed in a later release.",
            "See the list below and upgrade to the version where they are fixed."),
        ("DB-001", "Guest customer records",
            "Every visitor, including bots, creates a guest record. Millions of them slow down the database and backups.",
            "Open System → Maintenance and delete guest customers, or make sure the Delete guests task runs."),
        ("DB-002", "Bot traffic",
            "Many new guests but very few orders usually means bots are crawling the site and filling the database.",
            "Review your traffic in analytics. Block bad bots at your host or firewall, and keep the Delete guests task running."),
        ("DB-003", "Delete guests task",
            "This task removes old guest records. If it is off or stuck, the customer table keeps growing.",
            "Open Schedule tasks and enable \"Delete guests\". On very large tables, delete in batches from System → Maintenance first."),
        ("DB-004", "System log size",
            "A huge log table makes the log page slow and the database large.",
            "Open System → Log, fix the errors that repeat, then clear old entries."),
        ("DB-005", "Activity log size",
            "The activity log can grow to millions of rows and slow down the admin area.",
            "Open Customers → Activity Types and turn off types you don't need, then clear old entries from the Activity log."),
        ("DB-006", "Old sent emails",
            "Sent emails stay in the message queue forever unless you delete them.",
            "Open System → Maintenance and use \"Delete already sent emails\" for emails older than 30 days."),
        ("DB-007", "Old shopping carts",
            "Cart items that nobody has touched for 90 days take space and are rarely recovered.",
            "Open System → Maintenance and delete abandoned shopping carts."),
        ("CAT-001", "Products without images",
            "Products without photos sell less and look unfinished.",
            "Open Catalog → Products and add at least one picture to each published product."),
        ("CAT-003", "Duplicate SKUs",
            "The same SKU on several products confuses stock, imports and integrations.",
            "Open Catalog → Products, search by SKU and give each product a unique one."),
        ("CAT-004", "Products without meta description",
            "Search engines show the meta description in results. Without one, they pick random text.",
            "Add a short, useful meta description in the SEO tab of each product."),
        ("CAT-005", "Products priced at 0",
            "A simple product with price 0 can be ordered for free by mistake.",
            "Set a price, or enable \"Call for price\" or \"Customer enters price\" if 0 is intended."),
        ("CAT-007", "Products without a category",
            "Customers can't find products that aren't in any category.",
            "Open Catalog → Products and assign each published product to a category."),
        ("CAT-009", "Duplicate page URLs",
            "Two pages with the same URL name compete in search results, and only one can be reached.",
            "Open the SEO tab of the products or categories involved and give each a unique URL name.")
    ];

    public static readonly Dictionary<string, string> MetricLabels = new()
    {
        ["CurrentHourErrors"] = "Errors this hour", ["PreviousHourErrors"] = "Errors previous hour",
        ["LargestObservedGroup"] = "Most repeated error count", ["RecordsExamined"] = "Records examined",
        ["Truncated"] = "Sample capped (1 = yes)", ["OverdueTasks"] = "Overdue tasks", ["TasksExamined"] = "Tasks examined",
        ["UnknownTasks"] = "Tasks without data", ["NeverStartedTasks"] = "Never started", ["InvalidIntervals"] = "Invalid intervals",
        ["ReadyMessages"] = "Emails waiting", ["OldestEligibleMinutes"] = "Oldest wait (minutes)",
        ["ExhaustedMessages"] = "Failed 3 times", ["ObservedRecords24Hours"] = "Records in 24 h",
        ["InstalledPlugins"] = "Installed plugins", ["UnsupportedDeclarations"] = "Not listing this version",
        ["MissingDeclaredDependencies"] = "Missing dependencies", ["GuestCustomers"] = "Guest records", ["CountIsMinimum"] = "Counting stopped at the limit",
        ["GuestsCreated24Hours"] = "Guests created in 24 h", ["Orders24Hours"] = "Orders in 24 h",
        ["TaskEnabled"] = "Task enabled (1 = yes)", ["HoursSinceLastRun"] = "Hours since last run",
        ["LogRecords"] = "Log records", ["ActivityLogRecords"] = "Activity log records",
        ["SentEmailsOlderThan30Days"] = "Sent emails > 30 days", ["CartItemsOlderThan90Days"] = "Cart items > 90 days",
        ["EmailAccounts"] = "Email accounts", ["InvalidEmailAccounts"] = "Accounts with problems",
        ["DefaultAccountFound"] = "Default account found (1 = yes)", ["EmailsWithMissingAccount"] = "Emails with deleted account",
        ["NetworkChecksEnabled"] = "Network checks on (1 = yes)", ["CertificateDaysLeft"] = "Days until expiry",
        ["CertificateTrusted"] = "Certificate trusted (1 = yes)", ["CoreFailures"] = "Failures", ["CoreWarnings"] = "Warnings",
        ["FullErrorStackEnabled"] = "Detailed errors on (1 = yes)", ["CaptchaEnabled"] = "CAPTCHA on (1 = yes)",
        ["CaptchaOnRegistration"] = "On registration (1 = yes)", ["SampleAdminAccounts"] = "Sample admin accounts",
        ["IncompatiblePlugins"] = "Incompatible plugins", ["LoadErrors7Days"] = "Load errors in 7 days",
        ["KnownIssues"] = "Known bugs affecting you", ["IssuesInCatalog"] = "Bugs in catalog",
        ["ProductsWithoutImages"] = "Products", ["DuplicateSkus"] = "Duplicated SKUs",
        ["ProductsWithoutMetaDescription"] = "Products", ["ZeroPriceProducts"] = "Products",
        ["ProductsWithoutCategory"] = "Products", ["DuplicateUrlNames"] = "Duplicated URL names"
    };

    public static Dictionary<string, string> All()
    {
        var items = new Dictionary<string, string>();
        void Add(string key, string value) => items[$"Plugins.Misc.StorePulse.{key}"] = value;

        Add("Title", "StorePulse");
        Add("Introduction", "Store health and diagnostics");
        Add("Tagline", "Find problems early. Understand what's wrong. Fix the right thing first.");
        Add("Privacy", "StorePulse is read-only and does not send any data outside your server.");
        Add("Retention", "Scan history is deleted when the plugin is uninstalled.");
        Add("Run", "Run health scan"); Add("Running", "Scanning your store…"); Add("RunAgain", "Scan again");
        Add("Busy", "A scan is already running or just finished. Please wait 30 seconds and try again.");
        Add("NoScan", "No scan yet");
        Add("NoScan.Hint", "Run your first scan. It takes a few seconds, changes nothing, and checks tasks, emails, errors, database size, security basics, plugins and your catalog.");
        Add("Score", "Health score"); Add("CatalogScore", "Catalog quality");
        Add("Coverage", "Checked"); Add("Coverage.Hint", "Share of checks that could be evaluated. Unknown results are never counted as passes.");
        Add("Critical", "Critical"); Add("Warnings", "Warnings"); Add("Healthy", "Healthy"); Add("NotEvaluated", "Not checked");
        Add("Verdict.Good", "Your store looks healthy"); Add("Verdict.Attention", "A few things need attention");
        Add("Verdict.Critical", "Action needed"); Add("Verdict.Unknown", "Not enough information yet");
        Add("LastScan", "Last scan"); Add("ViewingOld", "You are viewing an older scan.");
        Add("ViewLatest", "View latest");
        Add("FixFirst", "Fix these first"); Add("FixFirst.Empty", "Nothing urgent. Nice work.");
        Add("Changes", "Since the previous scan"); Add("Changes.New", "New problems"); Add("Changes.Resolved", "Fixed");
        Add("Changes.None", "No changes since the previous scan."); Add("Changes.First", "This is your first scan. Run another after you fix something to see what changed.");
        Add("Trend", "Score over time");
        Add("Filter.Problems", "Problems only"); Add("Filter.All", "All checks"); Add("Search", "Search checks…");
        Add("Why", "Why it matters"); Add("WhatToDo", "What to do"); Add("Evidence", "What we found");
        Add("OpenPage", "Open page"); Add("LearnMore", "Learn more");
        Add("Help.Checks", "What StorePulse checks"); Add("Help.Causes", "Common causes");
        Add("Help.Steps", "How to fix it"); Add("Help.Ignore", "When it's safe to ignore");
        Add("Help.Guide", "Read the full guide on cartenvo.com"); Add("Help.Close", "Close");
        Add("Tab.Overview", "Overview"); Add("Tab.Checks", "All checks"); Add("Tab.History", "History");
        Add("Tab.Plugins", "Plugins"); Add("Tab.Upgrade", "Upgrade readiness");
        Add("Export", "Download report"); Add("Export.Html", "Report (HTML)"); Add("Export.Json", "Data (JSON)");
        Add("Settings", "Settings"); Add("Settings.Back", "Back to dashboard"); Add("Settings.Saved", "Settings saved.");
        Add("History", "Recent scans"); Add("History.Empty", "No scans yet.");
        Add("History.Date", "Date (UTC)"); Add("History.Score", "Score"); Add("History.Problems", "Problems"); Add("History.View", "View");
        Add("ClearHistory", "Delete oldest 100 scans");
        Add("ClearWarning", "This permanently deletes up to 100 of this store's oldest scans. Download any reports you need first.");
        Add("ClearConfirm", "Delete up to 100 old scans? This can't be undone.");
        Add("Inventory", "Installed plugins");
        Add("InventoryHint", "Live list of installed plugins. \"Supports this version\" is what the vendor declares; it does not guarantee the plugin works.");
        Add("InventoryTruncated", "Only the first 200 installed plugins are shown.");
        Add("PluginName", "System name"); Add("PluginVersion", "Version"); Add("DeclaresSupport", "Supports this version");
        Add("KnownIssues", "Known bugs in nopCommerce {0}"); Add("KnownIssues.None", "No known bugs from our list affect nopCommerce {0}.");
        Add("KnownIssues.FixedIn", "Fixed in {0}");
        Add("Upgrade.Title", "Upgrade readiness"); Add("Upgrade.Target", "Target version");
        Add("Upgrade.Hint", "Before upgrading nopCommerce, check that every installed plugin has a version for the target release. Plugins that ship with nopCommerce are updated with it.");
        Add("Upgrade.Ready", "Ready"); Add("Upgrade.NotReady", "Needs an update"); Add("Upgrade.Bundled", "Ships with nopCommerce");
        Add("Upgrade.Summary", "{0} of {1} plugins are ready for {2}");
        Add("Upgrade.SummaryDetail", "{0} declare support · {1} ship with nopCommerce · {2} need an update from the vendor");
        Add("Upgrade.Author", "Author"); Add("Upgrade.Declared", "Declared versions"); Add("Upgrade.Status", "Status");
        Add("Widget.Title", "Store health"); Add("Widget.Open", "Open StorePulse"); Add("Widget.Problems", "{0} problems found");
        Add("Widget.NoScan", "No health scan yet. Run one to check tasks, emails, errors and database size.");
        Add("Limits", "Scores cover the checks StorePulse can run. Unknown results are never passes. Counts come from records nopCommerce kept; StorePulse can't prove nothing was missed.");
        Add("Correlation.Email", "Emails are waiting and the task that sends them is off, missing or overdue. Fix the Send emails task first — that is the most likely cause.");
        Add("Correlation.Guests", "Lots of guests are being created and the Delete guests task isn't keeping up. Your database will keep growing until this is fixed.");
        Add("Correlation.Plugins", "Plugins failed to load. Errors in the log may be caused by these plugins, so fix them first.");
        Add("Check", "Check"); Add("Status", "Status"); Add("Action", "What to do");
        Add("Scope.System", "Whole system"); Add("Scope.Store", "This store");
        foreach (var status in Enum.GetNames<global::StorePulse.Core.HealthStatus>())
            Add($"Status.{status}", status switch
            {
                "Unknown" => "Couldn't check",
                "NotApplicable" => "Not applicable",
                "Warning" => "Needs attention",
                _ => status
            });
        foreach (var category in Enum.GetNames<global::StorePulse.Core.HealthCategory>())
            Add($"Category.{category}", category switch
            {
                "ScheduledTasks" => "Scheduled tasks",
                "ApplicationErrors" => "Errors",
                "Configuration" => "Configuration & security",
                "Catalog" => "Catalog quality",
                "Database" => "Database",
                _ => category
            });

        // Settings page
        Add("Settings.Scheduling", "Automatic scans"); Add("Settings.Thresholds", "Task and email thresholds");
        Add("Settings.Database", "Database size limits"); Add("Settings.Optional", "Optional checks"); Add("Settings.History", "History");
        Add("ScheduledScanningEnabled", "Scan this store automatically");
        Add("ScheduledScanIntervalHours", "Scan every (hours)");
        Add("SchedulingHint", "Off by default. Uses the nopCommerce scheduler, so it can't warn you if the scheduler itself is down — run a manual scan in that case. Restart the application after turning this on.");
        Add("SettingsScope", "These settings apply to the current store.");
        Add("HistoryRetentionDays", "Keep scans for (days)"); Add("TaskGraceMinutes", "Grace period for tasks (minutes)");
        Add("TaskOverdueMultiplier", "A task is overdue after this many missed intervals");
        Add("EmailWarningMinutes", "Warn when an email waits longer than (minutes)");
        Add("EmailBacklogWarningCount", "Warn when this many emails are waiting");
        Add("MaximumRecordsPerCheck", "Error records to group per scan");
        Add("GuestWarningCount", "Warn at this many guest records");
        Add("LogWarningCount", "Warn at this many log records");
        Add("ActivityLogWarningCount", "Warn at this many activity log records");
        Add("SentEmailWarningCount", "Warn at this many sent emails older than 30 days");
        Add("OldCartWarningCount", "Warn at this many cart items older than 90 days");
        Add("CatalogChecksEnabled", "Check catalog quality (images, SKUs, SEO, prices)");
        Add("CatalogChecksEnabled.Hint", "Catalog results get their own score and never lower the health score.");
        Add("NetworkChecksEnabled", "Check SSL certificate expiry");
        Add("NetworkChecksEnabled.Hint", "Connects only to this store's own address to read the certificate. Nothing is sent.");

        foreach (var (id, title, why, action) in Checks)
        {
            Add($"{id}.Title", title);
            Add($"{id}.Why", why);
            Add($"{id}.Action", action);
        }
        foreach (var help in StorePulseHelp.All)
        {
            var separator = StorePulseHelp.ItemSeparator.ToString();
            Add($"{help.Id}.Help.Checks", help.Checks);
            Add($"{help.Id}.Help.Causes", string.Join(separator, help.Causes));
            Add($"{help.Id}.Help.Steps", string.Join(separator, help.Steps));
            Add($"{help.Id}.Help.Ignore", help.Ignore);
        }
        foreach (var (metric, label) in MetricLabels)
            Add($"Metric.{metric}", label);
        return items;
    }
}
