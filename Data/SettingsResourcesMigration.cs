using FluentMigrator;
using Nop.Data.Migrations;
using Nop.Core.Infrastructure;
using Nop.Services.Localization;
using Cartenvo.Plugin.Misc.StorePulse.Infrastructure;

namespace Cartenvo.Plugin.Misc.StorePulse.Data;

[NopMigration("2026/10/26 12:02:00:0000000", "StorePulse settings resources", MigrationProcessType.NoMatter)]
public class SettingsResourcesMigration : MigrationBase
{
    public override void Up() => EngineContext.Current.Resolve<ILocalizationService>()
        .AddOrUpdateLocaleResource(StorePulseResources.All());
    public override void Down() { }
}
