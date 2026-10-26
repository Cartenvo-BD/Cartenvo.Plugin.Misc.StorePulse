using FluentMigrator;
using Nop.Core.Domain.Cms;
using Nop.Core.Infrastructure;
using Nop.Data.Migrations;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Cartenvo.Plugin.Misc.StorePulse.Infrastructure;

namespace Cartenvo.Plugin.Misc.StorePulse.Data;

/// <summary>Version 1.1: new check texts and the admin dashboard card for existing installations.</summary>
[NopMigration("2026/10/27 09:00:00:0000000", "StorePulse community checks and dashboard card", MigrationProcessType.NoMatter)]
public class CommunityChecksMigration : MigrationBase
{
    public override void Up()
    {
        EngineContext.Current.Resolve<ILocalizationService>().AddOrUpdateLocaleResource(StorePulseResources.All());
        var settings = EngineContext.Current.Resolve<ISettingService>();
        var widgets = settings.LoadSetting<WidgetSettings>();
        if (!widgets.ActiveWidgetSystemNames.Contains(StorePulsePlugin.SystemName))
        {
            widgets.ActiveWidgetSystemNames.Add(StorePulsePlugin.SystemName);
            settings.SaveSetting(widgets);
        }
    }

    public override void Down() { } // Uninstall removes resources by prefix and deactivates the card.
}
