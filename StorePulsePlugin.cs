using Nop.Core;
using Nop.Core.Domain.Cms;
using Nop.Services.Cms;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Plugins;
using Nop.Services.Security;
using Nop.Web.Framework.Infrastructure;
using Cartenvo.Plugin.Misc.StorePulse.Components;
using Cartenvo.Plugin.Misc.StorePulse.Infrastructure;
using Cartenvo.Plugin.Misc.StorePulse.Services;
using Nop.Services.ScheduleTasks;

namespace Cartenvo.Plugin.Misc.StorePulse;

public class StorePulsePlugin(ISettingService settings, ILocalizationService localization,
    IPermissionService permissions, IWebHelper webHelper, IScheduleTaskService tasks,
    WidgetSettings widgetSettings) : BasePlugin, IMiscPlugin, IWidgetPlugin
{
    public const string SystemName = "Misc.StorePulse";

    public override string GetConfigurationPageUrl() => $"{webHelper.GetStoreLocation()}Admin/StorePulse/Configure";

    // The admin home card can be switched off in Configuration → Widgets.
    public bool HideInWidgetList => false;

    public Task<IList<string>> GetWidgetZonesAsync()
        => Task.FromResult<IList<string>>([AdminWidgetZones.DashboardTop]);

    public Type GetWidgetViewComponent(string widgetZone) => typeof(StorePulseDashboardCardViewComponent);

    public override async Task InstallAsync()
    {
        await settings.SaveSettingAsync(new StorePulseSettings());
        await localization.AddOrUpdateLocaleResourceAsync(StorePulseResources.All());
        if (!widgetSettings.ActiveWidgetSystemNames.Contains(SystemName))
        {
            widgetSettings.ActiveWidgetSystemNames.Add(SystemName);
            await settings.SaveSettingAsync(widgetSettings);
        }
        await base.InstallAsync();
    }

    public override async Task UninstallAsync()
    {
        foreach (var task in (await tasks.GetAllTasksAsync(true)).Where(task => task.Type == StorePulseSchedule.TaskType))
            await tasks.DeleteTaskAsync(task);
        if (widgetSettings.ActiveWidgetSystemNames.Remove(SystemName))
            await settings.SaveSettingAsync(widgetSettings);
        // nopCommerce reverses this assembly's migrations after UninstallAsync,
        // dropping the plugin-owned scan history according to the deletion policy.
        await settings.DeleteSettingAsync<StorePulseSettings>();
        await localization.DeleteLocaleResourcesAsync("Plugins.Misc.StorePulse.");
        foreach (var permission in new StorePulsePermissions().AllConfigs)
            await permissions.DeletePermissionAsync(permission.SystemName);
        await base.UninstallAsync();
    }
}
