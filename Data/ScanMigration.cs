using FluentMigrator;
using Nop.Data.Extensions;
using Nop.Data.Migrations;
using Cartenvo.Plugin.Misc.StorePulse.Domain;

namespace Cartenvo.Plugin.Misc.StorePulse.Data;

// NoMatter runs on fresh installation and upgrade. Reversal deletes plugin-owned history.
[NopMigration("2026/10/26 12:00:00:0000000", "StorePulse scan history", MigrationProcessType.NoMatter)]
public class ScanMigration : AutoReversingMigration
{
    public override void Up()
    {
#if NOP_500
        this.CreateTableIfNotExists<StorePulseScan>(); // Create.TableFor was removed in 5.00
#else
        Create.TableFor<StorePulseScan>();
#endif
        Create.Index("IX_StorePulseScan_Store_Completed").OnTable(nameof(StorePulseScan))
            .OnColumn(nameof(StorePulseScan.StoreId)).Ascending()
            .OnColumn(nameof(StorePulseScan.CompletedOnUtc)).Descending();
    }
}
