using FluentMigrator;
using Nop.Core.Infrastructure;
using Nop.Data.Migrations;
using Nop.Services.Localization;
using Cartenvo.Plugin.Misc.StorePulse.Infrastructure;

namespace Cartenvo.Plugin.Misc.StorePulse.Data;

[NopMigration("2026/10/26 12:01:00:0000000", "StorePulse diagnostic resources", MigrationProcessType.NoMatter)]
public class ResourcesMigration : MigrationBase
{
    public override void Up() => EngineContext.Current.Resolve<ILocalizationService>()
        .AddOrUpdateLocaleResource(StorePulseResources.All());
    public override void Down() { } // Plugin lifecycle removes resources by prefix.
}
