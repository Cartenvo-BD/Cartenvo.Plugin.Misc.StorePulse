using Microsoft.AspNetCore.Mvc;
using Cartenvo.Plugin.Misc.StorePulse.Infrastructure;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;
using Nop.Core;
using Nop.Services.Security;
using Cartenvo.Plugin.Misc.StorePulse.Services;
using Cartenvo.Plugin.Misc.StorePulse.Models;
using StorePulse.Core;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Plugins;

namespace Cartenvo.Plugin.Misc.StorePulse.Controllers;

[AuthorizeAdmin]
[Area(AreaNames.ADMIN)]
[AutoValidateAntiforgeryToken]
public class StorePulseController(IStoreContext storeContext, ScanHistory history,
    StorePulseScanner scanner, IPermissionService permissions, ISettingService settingService,
    ILocalizationService localization, IPluginService pluginService, StorePulseSchedule schedule,
    IWebHelper webHelper) : BasePluginController
{
    private const string BundledAuthor = "nopCommerce team";

    [HttpGet]
    [CheckPermission(StorePulsePermissions.View)]
    public async Task<IActionResult> Configure(int scanId = 0, bool busy = false)
    {
        var store = await storeContext.GetCurrentStoreAsync();
        var scans = await history.RecentAsync(store.Id);
        var selected = scanId == 0 ? scans.FirstOrDefault() : scans.FirstOrDefault(scan => scan.Id == scanId);
        if (scanId != 0 && selected == null)
            return NotFound();
        var findings = selected == null ? [] : ScanHistory.Read(selected);
        var index = selected == null ? -1 : scans.IndexOf(selected);
        var previous = index >= 0 && index + 1 < scans.Count ? ScanHistory.Read(scans[index + 1]) : null;
        var installed = await pluginService.GetPluginDescriptorsAsync<IPlugin>();
        return View("~/Plugins/Misc.StorePulse/Views/Configure.cshtml", new DashboardModel
        {
            History = scans, Findings = findings, Score = HealthScoring.Calculate(findings),
            CatalogScore = HealthScoring.CalculateCatalog(findings),
            CanRun = await permissions.AuthorizeAsync(StorePulsePermissions.Run),
            CanExport = await permissions.AuthorizeAsync(StorePulsePermissions.Export),
            CanManage = await permissions.AuthorizeAsync(StorePulsePermissions.Manage),
            Busy = busy, SelectedScanId = selected?.Id ?? 0, IsLatest = index <= 0,
            CompletedOnUtc = selected?.CompletedOnUtc,
            Changes = previous == null ? null : ScanComparison.Compare(previous, findings),
            Trend = scans.AsEnumerable().Reverse().Select(scan =>
            {
                var items = ScanHistory.Read(scan);
                return new TrendPoint(scan.Id, scan.CompletedOnUtc, HealthScoring.Calculate(items).Score,
                    items.Count(item => ScanComparison.IsProblem(item.Status) && item.Category != HealthCategory.Catalog));
            }).ToList(),
            KnownIssues = KnownIssues.For(NopVersion.FULL_VERSION),
            NopVersion = NopVersion.FULL_VERSION,
            StoreLocation = webHelper.GetStoreLocation(),
            Inventory = installed.OrderBy(plugin => plugin.SystemName).Take(200)
                .Select(plugin => new PluginInventoryItem(Bound(plugin.SystemName), Bound(plugin.Version),
                    plugin.SupportedVersions.Contains(NopVersion.CURRENT_VERSION))).ToList(),
            InventoryTruncated = installed.Count > 200
        });
    }

    private static string Bound(string value) => string.IsNullOrEmpty(value) ? "—" : value[..Math.Min(value.Length, 128)];

    [HttpGet]
    [CheckPermission(StorePulsePermissions.View)]
    public async Task<IActionResult> Upgrade(string target = null)
    {
        var installed = await pluginService.GetPluginDescriptorsAsync<IPlugin>(LoadPluginsMode.All);
        KnownIssues.TryParse(NopVersion.CURRENT_VERSION, out var current);
        var options = installed.SelectMany(plugin => plugin.SupportedVersions ?? [])
            .Append(NopVersion.CURRENT_VERSION).Append("4.90").Append("5.00")
            .Where(version => KnownIssues.TryParse(version, out var parsed) && parsed >= current)
            .Distinct().OrderBy(version => KnownIssues.TryParse(version, out var parsed) ? parsed : current).ToList();
        target = options.Contains(target) ? target : options.FirstOrDefault(version => version != NopVersion.CURRENT_VERSION)
            ?? NopVersion.CURRENT_VERSION;
        return View("~/Plugins/Misc.StorePulse/Views/Upgrade.cshtml", new UpgradeModel
        {
            CurrentVersion = NopVersion.FULL_VERSION, TargetVersion = target, TargetOptions = options,
            Plugins = installed.Where(plugin => plugin.Installed).Take(500).Select(plugin => new UpgradeItem(
                    Bound(plugin.FriendlyName), Bound(plugin.SystemName), Bound(plugin.Version), Bound(plugin.Author),
                    string.Join(", ", (plugin.SupportedVersions ?? []).Take(10)),
                    (plugin.SupportedVersions ?? []).Contains(target),
                    (plugin.Author ?? "").Contains(BundledAuthor, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(item => item.Ready || item.Bundled).ThenBy(item => item.FriendlyName).ToList()
        });
    }

    [HttpPost]
    [CheckPermission(StorePulsePermissions.Manage)]
    [CheckPermission(StorePulsePermissions.View)]
    public async Task<IActionResult> ClearHistory()
    {
        var storeId = (await storeContext.GetCurrentStoreAsync()).Id;
        await history.ClearBatchAsync(storeId);
        return RedirectToAction(nameof(Configure));
    }

    [HttpGet]
    [CheckPermission(StorePulsePermissions.Manage)]
    public async Task<IActionResult> Settings(bool saved = false)
    {
        var storeId = (await storeContext.GetCurrentStoreAsync()).Id;
        var s = await settingService.LoadSettingAsync<StorePulseSettings>(storeId);
        return View("~/Plugins/Misc.StorePulse/Views/Settings.cshtml", new SettingsModel
        {
            HistoryRetentionDays = s.HistoryRetentionDays, TaskGraceMinutes = s.TaskGraceMinutes,
            TaskOverdueMultiplier = s.TaskOverdueMultiplier, EmailWarningMinutes = s.EmailWarningMinutes,
            EmailBacklogWarningCount = s.EmailBacklogWarningCount,
            ScheduledScanningEnabled = await schedule.IsEnabledAsync(storeId, s),
            ScheduledScanIntervalHours = s.ScheduledScanIntervalHours,
            MaximumRecordsPerCheck = s.MaximumRecordsPerCheck,
            GuestWarningCount = s.GuestWarningCount, LogWarningCount = s.LogWarningCount,
            ActivityLogWarningCount = s.ActivityLogWarningCount, SentEmailWarningCount = s.SentEmailWarningCount,
            OldCartWarningCount = s.OldCartWarningCount, CatalogChecksEnabled = s.CatalogChecksEnabled,
            NetworkChecksEnabled = s.NetworkChecksEnabled, Saved = saved
        });
    }

    [HttpPost]
    [CheckPermission(StorePulsePermissions.Manage)]
    public async Task<IActionResult> Settings(SettingsModel model)
    {
        if (!ModelState.IsValid)
            return View("~/Plugins/Misc.StorePulse/Views/Settings.cshtml", model);
        var storeId = (await storeContext.GetCurrentStoreAsync()).Id;
        await settingService.SaveSettingAsync(new StorePulseSettings
        {
            HistoryRetentionDays = model.HistoryRetentionDays, TaskGraceMinutes = model.TaskGraceMinutes,
            TaskOverdueMultiplier = model.TaskOverdueMultiplier, EmailWarningMinutes = model.EmailWarningMinutes,
            EmailBacklogWarningCount = model.EmailBacklogWarningCount,
            ScheduledScanningEnabled = model.ScheduledScanningEnabled,
            ScheduledScanIntervalHours = model.ScheduledScanIntervalHours,
            MaximumRecordsPerCheck = model.MaximumRecordsPerCheck,
            GuestWarningCount = model.GuestWarningCount, LogWarningCount = model.LogWarningCount,
            ActivityLogWarningCount = model.ActivityLogWarningCount, SentEmailWarningCount = model.SentEmailWarningCount,
            OldCartWarningCount = model.OldCartWarningCount, CatalogChecksEnabled = model.CatalogChecksEnabled,
            NetworkChecksEnabled = model.NetworkChecksEnabled
        }, storeId);
        await schedule.RefreshAsync();
        return RedirectToAction(nameof(Settings), new { saved = true });
    }

    [HttpPost]
    [CheckPermission(StorePulsePermissions.Run)]
    [CheckPermission(StorePulsePermissions.View)]
    public async Task<IActionResult> Run()
    {
        var store = await storeContext.GetCurrentStoreAsync();
        var configuration = await settingService.LoadSettingAsync<StorePulseSettings>(store.Id);
        var completed = await scanner.RunAsync(store.Id, configuration, HttpContext.RequestAborted);
        return RedirectToAction(nameof(Configure), new { busy = !completed });
    }

    [HttpGet]
    [CheckPermission(StorePulsePermissions.Export)]
    [CheckPermission(StorePulsePermissions.View)]
    public async Task<IActionResult> Export(int scanId, string format = "json")
    {
        if (format != "json" && format != "html")
            return BadRequest();
        var store = await storeContext.GetCurrentStoreAsync();
        var scan = (await history.RecentAsync(store.Id)).FirstOrDefault(item => item.Id == scanId);
        if (scan == null)
            return NotFound();
        var findings = ScanHistory.Read(scan);
        if (format == "html")
        {
            var translations = new Dictionary<string, string>();
            foreach (var key in findings.SelectMany(finding => new[]
                { finding.TitleResourceKey, finding.SummaryResourceKey, finding.RecommendationResourceKey,
                  $"Plugins.Misc.StorePulse.{finding.CheckId}.Why", $"Plugins.Misc.StorePulse.Category.{finding.Category}" }
                .Concat(finding.Metrics.Keys.Select(metric => $"Plugins.Misc.StorePulse.Metric.{metric}"))).Distinct())
                translations[key] = await localization.GetResourceAsync(key);
            return File(System.Text.Encoding.UTF8.GetBytes(HtmlReport.Render(findings,
                    key => translations.TryGetValue(key, out var text) ? text : key,
                    new HtmlReportInfo(NopVersion.FULL_VERSION, scan.CompletedOnUtc, StorePulseLinks.Guide))),
                "text/html", $"storepulse-{scan.Id}.html");
        }
        // Explicit projection excludes URLs, account identity, raw logs and plugin settings.
        var report = new { SchemaVersion = 1, NopVersion = NopVersion.FULL_VERSION, StoreId = store.Id,
            scan.StartedOnUtc, scan.CompletedOnUtc, Score = HealthScoring.Calculate(findings),
            CatalogScore = HealthScoring.CalculateCatalog(findings), Findings = findings };
        return File(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(report,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true }), "application/json", $"storepulse-{scan.Id}.json");
    }
}
