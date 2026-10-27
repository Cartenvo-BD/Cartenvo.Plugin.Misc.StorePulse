using FluentMigrator;
using Nop.Data.Migrations;
using Cartenvo.Plugin.Misc.StorePulse.Infrastructure;

namespace Cartenvo.Plugin.Misc.StorePulse.Data;

/// <summary>
/// Re-applies all texts on upgrade. When texts change, move the timestamp forward (and bump PluginRevision in the .csproj)
/// instead of adding another migration.
/// </summary>
[NopMigration("2026/10/30 09:00:00:0000000", "StorePulse wording refresh", MigrationProcessType.NoMatter)]
public class ResourceRefreshMigration : MigrationBase
{
    public override void Up() => MigrationCompat.AddResources(this);

    public override void Down() { }
}
