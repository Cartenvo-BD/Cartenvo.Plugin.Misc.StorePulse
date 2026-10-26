using FluentMigrator;
using Nop.Core.Infrastructure;
using Nop.Data.Migrations;
using Nop.Services.Localization;
using Cartenvo.Plugin.Misc.StorePulse.Infrastructure;

namespace Cartenvo.Plugin.Misc.StorePulse.Data;

/// <summary>
/// Re-applies all texts on upgrade. When texts change, move the timestamp forward (and bump PluginRevision in the .csproj)
/// instead of adding another migration.
/// </summary>
[NopMigration("2026/10/27 12:00:00:0000000", "StorePulse wording refresh", MigrationProcessType.NoMatter)]
public class ResourceRefreshMigration : MigrationBase
{
    public override void Up() => EngineContext.Current.Resolve<ILocalizationService>()
        .AddOrUpdateLocaleResource(StorePulseResources.All());

    public override void Down() { }
}
