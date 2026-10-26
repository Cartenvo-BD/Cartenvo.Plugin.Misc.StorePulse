using Nop.Core;

namespace Cartenvo.Plugin.Misc.StorePulse.Domain;

public class StorePulseScan : BaseEntity
{
    public int StoreId { get; set; }
    public DateTime StartedOnUtc { get; set; }
    public DateTime CompletedOnUtc { get; set; }
    public string FindingsJson { get; set; }
}
