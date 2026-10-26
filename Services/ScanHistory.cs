using System.Text.Json;
using Cartenvo.Plugin.Misc.StorePulse.Domain;
using Nop.Data;
using StorePulse.Core;

namespace Cartenvo.Plugin.Misc.StorePulse.Services;

public class ScanHistory(IRepository<StorePulseScan> repository)
{
    public async Task<int> ClearBatchAsync(int storeId)
    {
        var batch = await repository.GetAllAsync(query => query.Where(scan => scan.StoreId == storeId)
            .OrderBy(scan => scan.Id).Take(100), getCacheKey: null);
        if (batch.Count > 0)
            await repository.DeleteAsync(batch, publishEvent: false);
        return batch.Count;
    }

    public virtual Task<IList<StorePulseScan>> RecentAsync(int storeId) => repository.GetAllAsync(query =>
        query.Where(scan => scan.StoreId == storeId).OrderByDescending(scan => scan.Id).Take(20),
        getCacheKey: null);

    public async Task SaveAsync(int storeId, DateTime started, IReadOnlyList<HealthCheckResult> results, int retentionDays)
    {
        var json = JsonSerializer.Serialize(results);
        if (json.Length > 131072)
            throw new InvalidOperationException("Scan result exceeds the history limit.");
        await repository.InsertAsync(new StorePulseScan
        {
            StoreId = storeId, StartedOnUtc = started, CompletedOnUtc = DateTime.UtcNow, FindingsJson = json
        }, publishEvent: false);
        var cutoff = DateTime.UtcNow.AddDays(-Math.Clamp(retentionDays, 1, 365));
        var expired = await repository.GetAllAsync(query => query.Where(scan => scan.StoreId == storeId && scan.CompletedOnUtc < cutoff)
            .OrderBy(scan => scan.Id).Take(100), getCacheKey: null);
        if (expired.Count > 0)
            await repository.DeleteAsync(expired, publishEvent: false);
    }

    public static IReadOnlyList<HealthCheckResult> Read(StorePulseScan scan)
        => JsonSerializer.Deserialize<List<HealthCheckResult>>(scan.FindingsJson) ?? [];
}

