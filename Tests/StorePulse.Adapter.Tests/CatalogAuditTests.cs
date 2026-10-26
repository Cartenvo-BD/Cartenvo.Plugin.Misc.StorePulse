using Cartenvo.Plugin.Misc.StorePulse;
using Cartenvo.Plugin.Misc.StorePulse.Infrastructure;
using Cartenvo.Plugin.Misc.StorePulse.Services;
using Moq;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Security;
using Nop.Core.Domain.Seo;
using Nop.Core.Domain.Stores;
using Nop.Data;
using Nop.Services.Configuration;
using NUnit.Framework;
using StorePulse.Core;

namespace StorePulse.Adapter.Tests;

public class CatalogAuditTests
{
    private static IRepository<T> Failing<T>() where T : Nop.Core.BaseEntity
    {
        var repository = new Mock<IRepository<T>>();
        repository.SetupGet(item => item.Table).Throws(new Exception("Server=db;Password=secret Product 'Secret name'"));
        return repository.Object;
    }

    private static CatalogAudit Create()
    {
        var settings = new Mock<ISettingService>();
        settings.Setup(service => service.LoadSettingAsync<SeoSettings>(It.IsAny<int>()))
            .ReturnsAsync(new SeoSettings { GenerateProductMetaDescription = true });
        settings.Setup(service => service.LoadSettingAsync<CatalogSettings>(It.IsAny<int>()))
            .ReturnsAsync(new CatalogSettings());
        return new CatalogAudit(Failing<Product>(), Failing<ProductPicture>(), Failing<ProductCategory>(),
            Failing<ProductManufacturer>(), Failing<Category>(), Failing<Manufacturer>(), Failing<UrlRecord>(),
            Failing<StoreMapping>(), Failing<AclRecord>(), Failing<ProductReview>(), settings.Object);
    }

    [Test]
    public async Task EveryCatalogCheckHasTextsAndIsolatesFailures()
    {
        var checks = Create().Create(1, DateTimeOffset.UtcNow, new StorePulseSettings()).ToList();
        Assert.That(checks.Select(check => check.CheckId), Is.EqualTo(CatalogAudit.CheckIds));
        var results = await new HealthCheckRunner().RunAsync(new HealthCheckRegistry(checks),
            new HealthCheckContext(1, DateTimeOffset.UtcNow), TimeSpan.FromSeconds(30), CancellationToken.None);

        var resources = StorePulseResources.All();
        foreach (var result in results)
        {
            Assert.That(result.Category, Is.EqualTo(HealthCategory.Catalog));
            Assert.That(result.Status, Is.EqualTo(HealthStatus.Unknown), result.CheckId);
            foreach (var suffix in new[] { "Title", "Why", "Action", "Help.Checks", "Help.Causes", "Help.Steps", "Help.Ignore" })
                Assert.That(resources.ContainsKey($"Plugins.Misc.StorePulse.{result.CheckId}.{suffix}"), $"{result.CheckId}.{suffix}");
            Assert.That(StorePulseLinks.AdminPath(result.CheckId), Is.Not.Null, result.CheckId);
            Assert.That(StorePulseLinks.GuideSlug(result.CheckId), Is.Not.Null, result.CheckId);
        }
        var json = System.Text.Json.JsonSerializer.Serialize(results);
        Assert.That(json, Does.Not.Contain("secret").And.Not.Contain("Secret name"));
    }

    [Test]
    public void EveryMetricHasALabel()
    {
        var resources = StorePulseResources.All();
        foreach (var metric in new[] { "ProductsWithoutImages", "ProductsWithoutSku", "DuplicateSkus",
                     "ProductsWithoutMetaDescription", "ZeroPriceProducts", "SoldOutProducts", "ProductsWithoutCategory",
                     "ProductsWithoutIdentifier", "DuplicateUrlNames", "ProductsWithoutDescription",
                     "ProductsWithoutManufacturer", "ExpiredProducts", "OldPriceBelowPrice", "ProductsWithoutUrlName",
                     "HiddenByStoreLimits", "HiddenByAcl", "EmptyCategories", "CategoriesWithoutMetaDescription",
                     "ManufacturersWithoutMetaDescription", "DuplicateMetaDescriptions", "ReviewsWaiting" })
            Assert.That(resources.ContainsKey($"Plugins.Misc.StorePulse.Metric.{metric}"), metric);
        foreach (var kind in Enum.GetNames<CatalogItemKind>())
            Assert.That(resources.ContainsKey($"Plugins.Misc.StorePulse.Items.Kind.{kind}"), kind);
    }

    [Test]
    public void CatalogChecksCanBeTurnedOffTogetherOrOneByOne()
    {
        Assert.That(Create().Create(1, DateTimeOffset.UtcNow, new StorePulseSettings { CatalogChecksEnabled = false }), Is.Empty);
        var ids = Create().Create(1, DateTimeOffset.UtcNow,
            new StorePulseSettings { DisabledCatalogChecks = "cat-002, CAT-008,NOT-A-CHECK" }).Select(check => check.CheckId).ToList();
        Assert.That(ids, Does.Not.Contain("CAT-002").And.Not.Contain("CAT-008"));
        Assert.That(ids, Has.Count.EqualTo(CatalogAudit.CheckIds.Count - 2));
    }

    [Test]
    public void DisabledSettingRoundTrips()
    {
        Assert.That(CatalogAudit.FormatDisabled(["CAT-008", "cat-002", "CAT-002", "X-1"]), Is.EqualTo("CAT-002,CAT-008"));
        Assert.That(CatalogAudit.ParseDisabled(null), Is.Empty);
        Assert.That(CatalogAudit.ParseDisabled(" CAT-010 ,,PLUGIN-002"), Is.EquivalentTo(new[] { "CAT-010" }));
    }

    [Test]
    public async Task UnknownCheckHasNoList()
    {
        Assert.That(await Create().ListAsync("EMAIL-001", 1, CancellationToken.None), Is.Null);
    }
}
