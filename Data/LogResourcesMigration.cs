using FluentMigrator;
using Nop.Data.Migrations;
using Cartenvo.Plugin.Misc.StorePulse.Infrastructure;

namespace Cartenvo.Plugin.Misc.StorePulse.Data;

[NopMigration("2026/10/26 12:03:00:0000000", "StorePulse error-analysis resources", MigrationProcessType.NoMatter)]
public class LogResourcesMigration : MigrationBase
{
    public override void Up() => MigrationCompat.AddResources(this);
    public override void Down() { }
}
