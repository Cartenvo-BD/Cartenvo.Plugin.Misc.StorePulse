using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Services.Security;
using Nop.Web.Framework.Components;
using Cartenvo.Plugin.Misc.StorePulse.Infrastructure;
using Cartenvo.Plugin.Misc.StorePulse.Models;
using Cartenvo.Plugin.Misc.StorePulse.Services;
using StorePulse.Core;

namespace Cartenvo.Plugin.Misc.StorePulse.Components;

/// <summary>Small card on the admin home page. Reads the latest stored scan; never runs a scan.</summary>
public class StorePulseDashboardCardViewComponent(IPermissionService permissions, IStoreContext storeContext,
    ScanHistory history) : NopViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync(string widgetZone, object additionalData)
    {
        if (!await permissions.AuthorizeAsync(StorePulsePermissions.View))
            return Content(string.Empty);
        var store = await storeContext.GetCurrentStoreAsync();
        var latest = (await history.RecentAsync(store.Id)).FirstOrDefault();
        var findings = latest == null ? [] : ScanHistory.Read(latest);
        return View("~/Plugins/Misc.StorePulse/Views/DashboardCard.cshtml", new DashboardCardModel
        {
            HasScan = latest != null,
            CompletedOnUtc = latest?.CompletedOnUtc,
            Score = HealthScoring.Calculate(findings),
            Critical = findings.Where(f => f.Status == HealthStatus.Critical && f.Category != HealthCategory.Catalog).ToList(),
            Warnings = findings.Count(f => f.Status == HealthStatus.Warning && f.Category != HealthCategory.Catalog),
            TopProblems = DashboardModel.Prioritize(findings).Take(3).ToList()
        });
    }
}
