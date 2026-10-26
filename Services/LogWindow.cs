using LinqToDB;
using Nop.Core.Domain.Logging;
using Nop.Data;

namespace Cartenvo.Plugin.Misc.StorePulse.Services;

/// <summary>
/// Reads only the recent tail of the Log table. nopCommerce indexes Log by date alone, so on SQL Server a query
/// that also filters by level or message scans the whole table. Log ids grow with time: one cheap lookup on the
/// date index finds where the window starts, and the id range lets the database read only the rows after it.
/// </summary>
public static class LogWindow
{
    // Rows written at almost the same moment can be stored slightly out of order. Starting a little earlier keeps
    // them in; the date filter below still removes anything outside the window.
    private const int OrderingMargin = 1000;

    public static async Task<IQueryable<Log>> SinceAsync(IRepository<Log> logs, DateTime sinceUtc,
        CancellationToken cancellationToken)
    {
        var first = await logs.Table.Where(log => log.CreatedOnUtc >= sinceUtc)
            .OrderBy(log => log.CreatedOnUtc).Select(log => (int?)log.Id).FirstOrDefaultAsync(cancellationToken);
        if (first is not { } id)
            return logs.Table.Where(log => false);
        var start = id - OrderingMargin;
        return logs.Table.Where(log => log.Id >= start && log.CreatedOnUtc >= sinceUtc);
    }
}
