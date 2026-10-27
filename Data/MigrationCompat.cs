using FluentMigrator;
using Nop.Core.Domain.Cms;
using Cartenvo.Plugin.Misc.StorePulse.Infrastructure;
#if NOP_500
using Nop.Web.Framework.Extensions;
#else
using Nop.Core.Infrastructure;
using Nop.Services.Configuration;
using Nop.Services.Localization;
#endif

namespace Cartenvo.Plugin.Misc.StorePulse.Data;

/// <summary>
/// Migration steps that differ by nopCommerce version. 5.00 removed the synchronous service methods
/// (AddOrUpdateLocaleResource, LoadSetting, SaveSetting) and offers migration extensions instead.
/// </summary>
internal static class MigrationCompat
{
    public static void AddResources(IMigration migration)
    {
#if NOP_500
        migration.AddOrUpdateLocaleResource(StorePulseResources.All());
#else
        EngineContext.Current.Resolve<ILocalizationService>().AddOrUpdateLocaleResource(StorePulseResources.All());
#endif
    }

    /// <summary>Turns on the admin dashboard card if it isn't on yet.</summary>
    public static void ActivateWidget(IMigration migration)
    {
#if NOP_500
        migration.SetSetting<WidgetSettings, List<string>>(settings => settings.ActiveWidgetSystemNames, settings =>
        {
            if (!settings.ActiveWidgetSystemNames.Contains(StorePulsePlugin.SystemName))
                settings.ActiveWidgetSystemNames.Add(StorePulsePlugin.SystemName);
        });
#else
        var settings = EngineContext.Current.Resolve<ISettingService>();
        var widgets = settings.LoadSetting<WidgetSettings>();
        if (!widgets.ActiveWidgetSystemNames.Contains(StorePulsePlugin.SystemName))
        {
            widgets.ActiveWidgetSystemNames.Add(StorePulsePlugin.SystemName);
            settings.SaveSetting(widgets);
        }
#endif
    }
}
