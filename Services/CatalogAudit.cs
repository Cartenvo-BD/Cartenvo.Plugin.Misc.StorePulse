using LinqToDB;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Security;
using Nop.Core.Domain.Seo;
using Nop.Core.Domain.Stores;
using Nop.Data;
using Nop.Services.Configuration;
using StorePulse.Core;

namespace Cartenvo.Plugin.Misc.StorePulse.Services;

/// <summary>One row of a catalog finding's "affected items" list. Read live, never stored.</summary>
public sealed record CatalogItem(CatalogItemKind Kind, int Id, string Name, string Detail);

public enum CatalogItemKind { Product, Category, Manufacturer, UrlRecord, Review }

public sealed record CatalogItemList(string CheckId, IReadOnlyList<CatalogItem> Items, bool More);

/// <summary>
/// Catalog quality checks. Each rule defines its count and its item list together, so the number in a finding
/// and the list behind it always use the same conditions. Uses only fields that exist in nopCommerce 4.80,
/// 4.90 and 5.00 (see docs/storepulse-catalog-audit.md). All reads; nothing is changed.
/// </summary>
public class CatalogAudit(
    IRepository<Product> products,
    IRepository<ProductPicture> productPictures,
    IRepository<ProductCategory> productCategories,
    IRepository<ProductManufacturer> productManufacturers,
    IRepository<Category> categories,
    IRepository<Manufacturer> manufacturers,
    IRepository<UrlRecord> urlRecords,
    IRepository<StoreMapping> storeMappings,
    IRepository<AclRecord> aclRecords,
    IRepository<ProductReview> reviews,
    ISettingService settingService)
{
    /// <summary>Most items shown in a list. One more is read to know whether there are others.</summary>
    public const int ListLimit = 50;
    public const int ReviewWaitDays = 7;

    private const string ProductEntity = nameof(Product);

    /// <summary>All catalog checks, in the order they're shown in Settings.</summary>
    public static readonly IReadOnlyList<string> CheckIds =
    [
        "CAT-001", "CAT-002", "CAT-003", "CAT-004", "CAT-005", "CAT-006", "CAT-007", "CAT-008", "CAT-009",
        "CAT-010", "CAT-011", "CAT-012", "CAT-013", "CAT-014", "CAT-015", "CAT-016", "CAT-017", "CAT-018", "CAT-019"
    ];

    public static bool IsCatalogCheck(string checkId) => CheckIds.Contains(checkId, StringComparer.OrdinalIgnoreCase);

    /// <summary>Parses the comma-separated setting. Unknown ids are dropped.</summary>
    public static IReadOnlySet<string> ParseDisabled(string value) => (value ?? "")
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(IsCatalogCheck).Select(id => id.ToUpperInvariant()).ToHashSet(StringComparer.OrdinalIgnoreCase);

    public static string FormatDisabled(IEnumerable<string> ids)
        => string.Join(",", (ids ?? []).Where(IsCatalogCheck).Select(id => id.ToUpperInvariant()).Distinct().OrderBy(id => id));

    private sealed record Context(int StoreId, DateTime NowUtc, CancellationToken Token);

    private sealed record Rule(string Id,
        Func<Context, Task<(HealthStatus Status, Dictionary<string, decimal> Metrics)>> Count,
        Func<Context, Task<IReadOnlyList<CatalogItem>>> List);

    public IEnumerable<IHealthCheck> Create(int storeId, DateTimeOffset now, StorePulseSettings settings)
    {
        if (!settings.CatalogChecksEnabled)
            yield break;
        var disabled = ParseDisabled(settings.DisabledCatalogChecks);
        foreach (var rule in Rules())
        {
            if (disabled.Contains(rule.Id))
                continue;
            yield return new CatalogCheck(rule.Id, async token =>
            {
                var (status, metrics) = await rule.Count(new Context(storeId, now.UtcDateTime, token));
                return new HealthCheckResult(rule.Id, HealthCategory.Catalog, HealthScope.System, status,
                    $"Plugins.Misc.StorePulse.{rule.Id}.Title", $"Plugins.Misc.StorePulse.Status.{status}",
                    $"Plugins.Misc.StorePulse.{rule.Id}.Action", now, metrics);
            });
        }
    }

    /// <summary>The items behind a finding, read now. Null for an unknown check id.</summary>
    public async Task<CatalogItemList> ListAsync(string checkId, int storeId, CancellationToken token)
    {
        var rule = Rules().FirstOrDefault(item => string.Equals(item.Id, checkId, StringComparison.OrdinalIgnoreCase));
        if (rule == null)
            return null;
        var items = await rule.List(new Context(storeId, DateTime.UtcNow, token));
        return new CatalogItemList(rule.Id, items.Take(ListLimit).ToArray(), items.Count > ListLimit);
    }

    // Shown on its own, so a customer can find it. Children of grouped products are excluded.
    private IQueryable<Product> Visible() => products.Table.Where(product =>
        product.Published && !product.Deleted && product.VisibleIndividually);

    private IQueryable<Product> VisibleSimple() => Visible().Where(product =>
        product.ProductTypeId == (int)ProductType.SimpleProduct);

    private IEnumerable<Rule> Rules()
    {
        yield return Products("CAT-001", "ProductsWithoutImages", _ => Visible().Where(product =>
            !productPictures.Table.Any(picture => picture.ProductId == product.Id)));

        yield return Products("CAT-002", "ProductsWithoutSku", _ => VisibleSimple().Where(product =>
            product.Sku == null || product.Sku == ""));

        yield return new Rule("CAT-003",
            async context => Warn("DuplicateSkus", await DuplicateSkus().CountAsync(context.Token)),
            async context =>
            {
                // Read the duplicated values first: filtering by the grouped subquery fails on SQL Server.
                var skus = await DuplicateSkus().OrderBy(sku => sku).Take(ListLimit + 1).ToListAsync(context.Token);
                return skus.Count == 0 ? [] : await ListProducts(products.Table
                    .Where(product => !product.Deleted && skus.Contains(product.Sku))
                    .OrderBy(product => product.Sku).ThenBy(product => product.Id), context);
            });

        // nopCommerce uses the short description when "Generate product meta description" is on (the default).
        // Only products that end up with the store's default description are counted.
        yield return Products("CAT-004", "ProductsWithoutMetaDescription", async context =>
        {
            var generate = (await settingService.LoadSettingAsync<SeoSettings>(context.StoreId)).GenerateProductMetaDescription;
            return Visible().Where(product => (product.MetaDescription == null || product.MetaDescription == "") &&
                (!generate || product.ShortDescription == null || product.ShortDescription == ""));
        });

        yield return Products("CAT-005", "ZeroPriceProducts", _ => VisibleSimple().Where(product =>
            product.Price == 0 && !product.CallForPrice && !product.CustomerEntersPrice));

        // Stock in several warehouses lives in ProductWarehouseInventory; those products are left out.
        yield return Products("CAT-006", "SoldOutProducts", _ => VisibleSimple().Where(product =>
            product.ManageInventoryMethodId == (int)ManageInventoryMethod.ManageStock && !product.UseMultipleWarehouses &&
            product.StockQuantity <= 0 && product.BackorderModeId == (int)BackorderMode.NoBackorders));

        yield return Products("CAT-007", "ProductsWithoutCategory", _ => Visible().Where(product =>
            !productCategories.Table.Any(mapping => mapping.ProductId == product.Id)));

        yield return Products("CAT-008", "ProductsWithoutIdentifier", _ => VisibleSimple().Where(product =>
            (product.Gtin == null || product.Gtin == "") &&
            (product.ManufacturerPartNumber == null || product.ManufacturerPartNumber == "")));

        yield return new Rule("CAT-009",
            async context => Warn("DuplicateUrlNames", await DuplicateSlugs().CountAsync(context.Token)),
            async context =>
            {
                // Read the duplicated names first, then their records (a correlated subquery is slow on large stores).
                var keys = await DuplicateSlugs().OrderBy(key => key.Slug).Take(ListLimit + 1).ToListAsync(context.Token);
                if (keys.Count == 0)
                    return [];
                var slugs = keys.Select(key => key.Slug).Distinct().ToArray();
                var rows = (await urlRecords.Table.Where(record => record.IsActive && slugs.Contains(record.Slug))
                        .OrderBy(record => record.Slug).ThenBy(record => record.Id).Take(5 * (ListLimit + 1))
                        .Select(record => new { record.Id, record.Slug, record.EntityName, record.EntityId, record.LanguageId })
                        .ToListAsync(context.Token))
                    .Where(row => keys.Any(key => key.Slug == row.Slug && key.LanguageId == row.LanguageId))
                    .Take(ListLimit + 1);
                // Link to the page that owns the URL name when it's a catalog page.
                return rows.Select(row => new CatalogItem(row.EntityName switch
                    {
                        nameof(Product) => CatalogItemKind.Product,
                        nameof(Category) => CatalogItemKind.Category,
                        nameof(Manufacturer) => CatalogItemKind.Manufacturer,
                        _ => CatalogItemKind.UrlRecord
                    }, row.EntityId, row.Slug,
                    $"{row.EntityName} {row.EntityId}" + (row.LanguageId == 0 ? "" : $" · language {row.LanguageId}"))).ToArray();
            });

        yield return Products("CAT-010", "ProductsWithoutDescription", _ => Visible().Where(product =>
            (product.ShortDescription == null || product.ShortDescription == "") &&
            (product.FullDescription == null || product.FullDescription == "")));

        yield return Products("CAT-011", "ProductsWithoutManufacturer", _ => Visible().Where(product =>
            !productManufacturers.Table.Any(mapping => mapping.ProductId == product.Id)));

        // nopCommerce returns 404 for these (ProductService.ProductIsAvailable), but they still look published.
        yield return Products("CAT-012", "ExpiredProducts", context => Visible().Where(product =>
            product.AvailableEndDateTimeUtc != null && product.AvailableEndDateTimeUtc < context.NowUtc));

        // The old price is shown crossed out whenever it is above 0 and differs from the price.
        yield return Products("CAT-013", "OldPriceBelowPrice", _ => Visible().Where(product =>
            product.OldPrice > 0 && product.OldPrice < product.Price));

        yield return Products("CAT-014", "ProductsWithoutUrlName", _ => Visible().Where(product =>
            !urlRecords.Table.Any(record => record.EntityName == ProductEntity && record.EntityId == product.Id &&
                record.LanguageId == 0 && record.IsActive)));

        yield return new Rule("CAT-015", async context =>
            {
                var (stores, acl) = await HiddenAsync(context);
                if (stores == null && acl == null)
                    return (HealthStatus.NotApplicable, new());
                var byStores = stores == null ? 0 : await stores.CountAsync(context.Token);
                var byAcl = acl == null ? 0 : await acl.CountAsync(context.Token);
                return (byStores + byAcl > 0 ? HealthStatus.Warning : HealthStatus.Healthy,
                    new() { ["HiddenByStoreLimits"] = byStores, ["HiddenByAcl"] = byAcl });
            },
            async context =>
            {
                var (stores, acl) = await HiddenAsync(context);
                var query = stores != null && acl != null ? stores.Union(acl) : stores ?? acl;
                return query == null ? [] : await ListProducts(query.OrderBy(product => product.Id), context);
            });

        yield return new Rule("CAT-016",
            async context => Warn("EmptyCategories", await EmptyCategories().CountAsync(context.Token)),
            async context => (await EmptyCategories().OrderBy(category => category.Id).Take(ListLimit + 1)
                    .Select(category => new { category.Id, category.Name }).ToListAsync(context.Token))
                .Select(row => new CatalogItem(CatalogItemKind.Category, row.Id, row.Name, "")).ToArray());

        yield return new Rule("CAT-017", async context =>
            {
                var withoutCategory = await CategoriesWithoutMeta().CountAsync(context.Token);
                var withoutManufacturer = await ManufacturersWithoutMeta().CountAsync(context.Token);
                return (withoutCategory + withoutManufacturer > 0 ? HealthStatus.Warning : HealthStatus.Healthy,
                    new() { ["CategoriesWithoutMetaDescription"] = withoutCategory,
                        ["ManufacturersWithoutMetaDescription"] = withoutManufacturer });
            },
            async context =>
            {
                var first = (await CategoriesWithoutMeta().OrderBy(category => category.Id).Take(ListLimit + 1)
                        .Select(category => new { category.Id, category.Name }).ToListAsync(context.Token))
                    .Select(row => new CatalogItem(CatalogItemKind.Category, row.Id, row.Name, ""));
                var second = (await ManufacturersWithoutMeta().OrderBy(manufacturer => manufacturer.Id).Take(ListLimit + 1)
                        .Select(manufacturer => new { manufacturer.Id, manufacturer.Name }).ToListAsync(context.Token))
                    .Select(row => new CatalogItem(CatalogItemKind.Manufacturer, row.Id, row.Name, ""));
                return first.Concat(second).Take(ListLimit + 1).ToArray();
            });

        yield return new Rule("CAT-018",
            async context => Warn("DuplicateMetaDescriptions", await DuplicateMeta().CountAsync(context.Token)),
            async context =>
            {
                // Same two steps as CAT-003. Long texts can't be sorted on every database, so order by id.
                var texts = await DuplicateMeta().Take(ListLimit + 1).ToListAsync(context.Token);
                return texts.Count == 0 ? [] : await ListProducts(Visible()
                    .Where(product => texts.Contains(product.MetaDescription)).OrderBy(product => product.Id), context);
            });

        yield return new Rule("CAT-019", async context =>
            {
                var cutoff = context.NowUtc.AddDays(-ReviewWaitDays);
                return Warn("ReviewsWaiting", await reviews.Table
                    .CountAsync(review => !review.IsApproved && review.CreatedOnUtc < cutoff, context.Token));
            },
            async context =>
            {
                var cutoff = context.NowUtc.AddDays(-ReviewWaitDays);
                var rows = await (from review in reviews.Table
                        join product in products.Table on review.ProductId equals product.Id
                        where !review.IsApproved && review.CreatedOnUtc < cutoff
                        orderby review.CreatedOnUtc
                        select new { review.Id, product.Name, review.CreatedOnUtc })
                    .Take(ListLimit + 1).ToListAsync(context.Token);
                return rows.Select(row => new CatalogItem(CatalogItemKind.Review, row.Id, row.Name,
                    row.CreatedOnUtc.ToString("yyyy-MM-dd"))).ToArray();
            });
    }

    private Rule Products(string id, string metric, Func<Context, IQueryable<Product>> query)
        => Products(id, metric, context => Task.FromResult(query(context)));

    private Rule Products(string id, string metric, Func<Context, Task<IQueryable<Product>>> query)
        => new(id,
            async context => Warn(metric, await (await query(context)).CountAsync(context.Token)),
            async context => await ListProducts((await query(context)).OrderBy(product => product.Id), context));

    private static async Task<IReadOnlyList<CatalogItem>> ListProducts(IQueryable<Product> query, Context context)
        => (await query.Take(ListLimit + 1).Select(product => new { product.Id, product.Name, product.Sku })
                .ToListAsync(context.Token))
            .Select(row => new CatalogItem(CatalogItemKind.Product, row.Id, row.Name, row.Sku ?? "")).ToArray();

    private static (HealthStatus, Dictionary<string, decimal>) Warn(string metric, int count)
        => (count > 0 ? HealthStatus.Warning : HealthStatus.Healthy, new() { [metric] = count });

    private IQueryable<string> DuplicateSkus() => products.Table
        .Where(product => !product.Deleted && product.Sku != null && product.Sku != "")
        .GroupBy(product => product.Sku).Where(group => group.Count() > 1).Select(group => group.Key);

    private IQueryable<SlugKey> DuplicateSlugs() => urlRecords.Table
        .Where(record => record.IsActive)
        .GroupBy(record => new { record.Slug, record.LanguageId }).Where(group => group.Count() > 1)
        .Select(group => new SlugKey { Slug = group.Key.Slug, LanguageId = group.Key.LanguageId });

    private sealed class SlugKey
    {
        public string Slug { get; set; }
        public int LanguageId { get; set; }
    }

    private IQueryable<string> DuplicateMeta() => Visible()
        .Where(product => product.MetaDescription != null && product.MetaDescription != "")
        .GroupBy(product => product.MetaDescription).Where(group => group.Count() > 1).Select(group => group.Key);

    /// <summary>Null for a part that nopCommerce is set to ignore.</summary>
    private async Task<(IQueryable<Product> Stores, IQueryable<Product> Acl)> HiddenAsync(Context context)
    {
        var catalog = await settingService.LoadSettingAsync<CatalogSettings>(context.StoreId);
        var stores = catalog.IgnoreStoreLimitations ? null : Visible().Where(product => product.LimitedToStores &&
            !storeMappings.Table.Any(mapping => mapping.EntityName == ProductEntity && mapping.EntityId == product.Id));
        var acl = catalog.IgnoreAcl ? null : Visible().Where(product => product.SubjectToAcl &&
            !aclRecords.Table.Any(record => record.EntityName == ProductEntity && record.EntityId == product.Id));
        return (stores, acl);
    }

    private IQueryable<Category> EmptyCategories() => categories.Table.Where(category =>
        category.Published && !category.Deleted &&
        !(from mapping in productCategories.Table
          join product in products.Table on mapping.ProductId equals product.Id
          where mapping.CategoryId == category.Id && product.Published && !product.Deleted && product.VisibleIndividually
          select mapping.Id).Any() &&
        !categories.Table.Any(child => child.ParentCategoryId == category.Id && child.Published && !child.Deleted));

    private IQueryable<Category> CategoriesWithoutMeta() => categories.Table.Where(category =>
        category.Published && !category.Deleted && (category.MetaDescription == null || category.MetaDescription == ""));

    private IQueryable<Manufacturer> ManufacturersWithoutMeta() => manufacturers.Table.Where(manufacturer =>
        manufacturer.Published && !manufacturer.Deleted &&
        (manufacturer.MetaDescription == null || manufacturer.MetaDescription == ""));

    private sealed class CatalogCheck(string id, Func<CancellationToken, Task<HealthCheckResult>> execute) : IHealthCheck
    {
        public string CheckId => id;
        public HealthCategory Category => HealthCategory.Catalog;
        public HealthScope Scope => HealthScope.System;
        public Task<HealthCheckResult> ExecuteAsync(HealthCheckContext context, CancellationToken cancellationToken)
            => execute(cancellationToken);
    }
}
