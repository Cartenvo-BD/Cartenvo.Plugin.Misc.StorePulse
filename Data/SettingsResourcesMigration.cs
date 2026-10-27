using FluentMigrator;
using Nop.Data.Migrations;
using Cartenvo.Plugin.Misc.StorePulse.Infrastructure;

namespace Cartenvo.Plugin.Misc.StorePulse.Data;

[NopMigration("2026/10/26 12:02:00:0000000", "StorePulse settings resources", MigrationProcessType.NoMatter)]
public class SettingsResourcesMigration : MigrationBase
{
    public override void Up() => MigrationCompat.AddResources(this);
    public override void Down() { }
}
