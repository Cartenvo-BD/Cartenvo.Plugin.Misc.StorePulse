using FluentMigrator;
using Nop.Data.Migrations;
using Nop.Core.Infrastructure;
using Nop.Services.Localization;
using Cartenvo.Plugin.Misc.StorePulse.Infrastructure;

namespace Cartenvo.Plugin.Misc.StorePulse.Data;

[NopMigration("2026/10/26 12:04:00:0000000", "StorePulse history and inventory resources", MigrationProcessType.NoMatter)]
public class InventoryResourcesMigration : MigrationBase
{
    public override void Up() => EngineContext.Current.Resolve<ILocalizationService>().AddOrUpdateLocaleResource(StorePulseResources.All());
    public override void Down() { }
}
