using FluentMigrator.Builders.Create.Table;
using Nop.Data.Mapping.Builders;
using Cartenvo.Plugin.Misc.StorePulse.Domain;

namespace Cartenvo.Plugin.Misc.StorePulse.Data;

public class ScanBuilder : NopEntityBuilder<StorePulseScan>
{
    public override void MapEntity(CreateTableExpressionBuilder table)
        => table.WithColumn(nameof(StorePulseScan.FindingsJson)).AsString(int.MaxValue).NotNullable();
}
