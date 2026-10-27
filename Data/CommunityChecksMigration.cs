using FluentMigrator;
using Nop.Data.Migrations;
using Cartenvo.Plugin.Misc.StorePulse.Infrastructure;

namespace Cartenvo.Plugin.Misc.StorePulse.Data;

/// <summary>Version 1.1: new check texts and the admin dashboard card for existing installations.</summary>
[NopMigration("2026/10/27 09:00:00:0000000", "StorePulse community checks and dashboard card", MigrationProcessType.NoMatter)]
public class CommunityChecksMigration : MigrationBase
{
    public override void Up()
    {
        MigrationCompat.AddResources(this);
        MigrationCompat.ActivateWidget(this);
    }

    public override void Down() { } // Uninstall removes resources by prefix and deactivates the card.
}
