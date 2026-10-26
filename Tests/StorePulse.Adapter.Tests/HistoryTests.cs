using Cartenvo.Plugin.Misc.StorePulse.Domain;
using Cartenvo.Plugin.Misc.StorePulse.Services;
using Moq;
using Nop.Core.Caching;
using Nop.Data;
using NUnit.Framework;

namespace StorePulse.Adapter.Tests;

public class HistoryTests
{
    [Test]
    public async Task ClearingHistoryIsBoundedAndCannotDeleteAnotherStoresScans()
    {
        var rows = Enumerable.Range(1, 250).Select(id => new StorePulseScan
        {
            Id = id, StoreId = id % 2 == 0 ? 1 : 2
        }).ToList();
        var repository = Repository(rows);
        IList<StorePulseScan> deleted = null;
        repository.Setup(repo => repo.DeleteAsync(It.IsAny<IList<StorePulseScan>>(), false))
            .Callback<IList<StorePulseScan>, bool>((items, _) => deleted = items).Returns(Task.CompletedTask);
        var count = await new ScanHistory(repository.Object).ClearBatchAsync(1);
        Assert.That(count, Is.EqualTo(100));
        Assert.That(deleted.All(scan => scan.StoreId == 1), Is.True);
        Assert.That(deleted.Select(scan => scan.Id), Is.Ordered.Ascending);
        Assert.That(deleted.Max(scan => scan.Id), Is.EqualTo(200));
    }

    [Test]
    public async Task RecentHistoryUsesOnlyRequestedStoreAndLatestTwenty()
    {
        var rows = Enumerable.Range(1, 60).Select(id => new StorePulseScan { Id = id, StoreId = id % 2 }).ToList();
        var result = await new ScanHistory(Repository(rows).Object).RecentAsync(1);
        Assert.That(result.Count, Is.EqualTo(20));
        Assert.That(result.All(scan => scan.StoreId == 1), Is.True);
        Assert.That(result.First().Id, Is.EqualTo(59));
        Assert.That(result.Last().Id, Is.EqualTo(21));
    }

    private static Mock<IRepository<StorePulseScan>> Repository(List<StorePulseScan> rows)
    {
        var repository = new Mock<IRepository<StorePulseScan>>();
        repository.Setup(repo => repo.GetAllAsync(
                It.IsAny<Func<IQueryable<StorePulseScan>, IQueryable<StorePulseScan>>>(),
                It.IsAny<Func<ICacheKeyService, CacheKey>>(), true))
            .Returns((Func<IQueryable<StorePulseScan>, IQueryable<StorePulseScan>> query,
                Func<ICacheKeyService, CacheKey> cache, bool _) =>
            {
                Assert.That(cache, Is.Null, "Filtered history queries must bypass the shared default cache key");
                return Task.FromResult<IList<StorePulseScan>>(query(rows.AsQueryable()).ToList());
            });
        return repository;
    }
}
