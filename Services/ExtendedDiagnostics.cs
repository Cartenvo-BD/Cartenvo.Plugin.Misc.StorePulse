using System.Collections;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using LinqToDB;
using Microsoft.AspNetCore.Http;
using Nop.Core;
using Nop.Core.Configuration;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Logging;
using Nop.Core.Domain.Messages;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Security;
using Nop.Core.Domain.Seo;
using Nop.Data;
using Nop.Services.Configuration;
using Nop.Services.Plugins;
using Nop.Services.ScheduleTasks;
using Nop.Services.Stores;
using StorePulse.Core;

namespace Cartenvo.Plugin.Misc.StorePulse.Services;

/// <summary>
/// Checks added from community research: database hygiene, email accounts, security basics,
/// plugin load problems, known platform issues and catalog quality. All are read-only.
/// Only CONFIG-004 opens a network connection, and only when the administrator enables it.
/// </summary>
public class ExtendedDiagnostics(
    IRepository<Customer> customers,
    IRepository<CustomerRole> roles,
    IRepository<CustomerCustomerRoleMapping> roleMappings,
    IRepository<Order> orders,
    IRepository<ActivityLog> activityLogs,
    IRepository<ShoppingCartItem> carts,
    IRepository<QueuedEmail> queue,
    IRepository<EmailAccount> emailAccounts,
    IRepository<Log> logs,
    IRepository<Product> products,
    IRepository<ProductPicture> productPictures,
    IRepository<ProductCategory> productCategories,
    IRepository<UrlRecord> urlRecords,
    IScheduleTaskService tasks,
    IStoreService stores,
    ISettingService settingService,
    IPluginService plugins,
    AppSettings appSettings,
    IHttpContextAccessor httpContextAccessor,
    IServiceProvider services)
{
    public const string DeleteGuestsTaskType = "Nop.Services.Customers.DeleteGuestsTask";
    public const string SampleAdminEmail = "admin@yourstore.com";
    /// <summary>Metric flag: counting stopped at the warning threshold, so the real number is at least the value shown.</summary>
    public const string CountIsMinimum = MetricFlags.CountIsMinimum;

    public IEnumerable<IHealthCheck> Create(int storeId, DateTimeOffset now, StorePulseSettings settings,
        CancellationToken scanToken)
    {
        // Each check runs with its own time limit; the runner hands it over just before the check starts.
        var token = scanToken;
        long? guestCount = null;
        DateTime Utc(int days) => now.UtcDateTime.AddDays(days);
        async Task<int> GuestRoleIdAsync() => await roles.Table
            .Where(role => role.SystemName == NopCustomerDefaults.GuestsRoleName).Select(role => role.Id)
            .FirstOrDefaultAsync(token);
        // Large stores can hold tens of millions of rows. Stop counting at the threshold: the answer is the same
        // and the database reads a fraction of the table.
        async Task<long> GuestCountAsync()
        {
            var guestRole = await GuestRoleIdAsync();
            return guestRole == 0 ? 0 : await roleMappings.Table
                .Where(mapping => mapping.CustomerRoleId == guestRole).Take(Math.Max(1, settings.GuestWarningCount))
                .CountAsync(token);
        }
        async Task<(HealthStatus Status, Dictionary<string, decimal> Metrics)> BoundedAsync<T>(IQueryable<T> query,
            int threshold, string metric)
        {
            var limit = Math.Max(1, threshold);
            var count = await query.Take(limit).CountAsync(token);
            return (CommunityRules.AtLeast(count, limit), Bounded(metric, count, limit));
        }
        IHealthCheck Check(string id, HealthCategory category,
            Func<Task<(HealthStatus Status, Dictionary<string, decimal> Metrics)>> run, HealthScope scope = HealthScope.System)
            => new ExtendedCheck(id, category, scope, async checkToken =>
            {
                token = checkToken;
                var (status, metrics) = await run();
                return new HealthCheckResult(id, category, scope, status, $"Plugins.Misc.StorePulse.{id}.Title",
                    $"Plugins.Misc.StorePulse.Status.{status}", $"Plugins.Misc.StorePulse.{id}.Action", now, metrics);
            });
        IHealthCheck Catalog(string id, string metric, Func<Task<int>> count)
            => Check(id, HealthCategory.Catalog, async () =>
            {
                var observed = await count();
                return (observed > 0 ? HealthStatus.Warning : HealthStatus.Healthy, new() { [metric] = observed });
            });
        IQueryable<Product> Visible() => products.Table.Where(product =>
            product.Published && !product.Deleted && product.VisibleIndividually);

        var list = new List<IHealthCheck>();
            // Database hygiene
            list.Add(Check("DB-001", HealthCategory.Database, async () =>
            {
                guestCount = await GuestCountAsync();
                var limit = Math.Max(1, settings.GuestWarningCount);
                return (CommunityRules.AtLeast(guestCount.Value, limit), Bounded("GuestCustomers", guestCount.Value, limit));
            }));
            list.Add(Check("DB-002", HealthCategory.Database, async () =>
            {
                var guestRole = await GuestRoleIdAsync();
                var since = Utc(-1);
                var created = guestRole == 0 ? 0 : await (from mapping in roleMappings.Table
                    join customer in customers.Table on mapping.CustomerId equals customer.Id
                    where mapping.CustomerRoleId == guestRole && customer.CreatedOnUtc >= since
                    select customer.Id).CountAsync(token);
                var orderCount = await orders.Table.CountAsync(order => !order.Deleted && order.CreatedOnUtc >= since, token);
                return (CommunityRules.BotTraffic(created, orderCount),
                    new() { ["GuestsCreated24Hours"] = created, ["Orders24Hours"] = orderCount });
            }));
            list.Add(Check("DB-003", HealthCategory.Database, async () =>
            {
                var task = (await tasks.GetAllTasksAsync(true)).FirstOrDefault(item =>
                    (item.Type ?? "").Split(',')[0].Trim() == DeleteGuestsTaskType);
                guestCount ??= await GuestCountAsync();
                var large = guestCount.Value >= Math.Max(1, settings.GuestWarningCount);
                var overdue = task is { Enabled: true } && TaskHealthRules.Overdue(new TaskObservation(task.Enabled,
                    task.Seconds, Offset(task.LastEnabledUtc), Offset(task.LastStartUtc)), now,
                    TimeSpan.FromMinutes(Math.Clamp(settings.TaskGraceMinutes, 1, 1440)),
                    Math.Clamp(settings.TaskOverdueMultiplier, 1, 20)) == HealthStatus.Warning;
                // A disabled cleanup task only matters once guests have piled up.
                var status = task == null || !task.Enabled
                    ? (large ? HealthStatus.Warning : HealthStatus.Healthy)
                    : overdue ? HealthStatus.Warning : HealthStatus.Healthy;
                var metrics = new Dictionary<string, decimal> { ["TaskEnabled"] = task is { Enabled: true } ? 1 : 0 };
                // Left out when the task has never run, rather than showing a meaningless -1.
                if (task?.LastStartUtc is { } started)
                    metrics["HoursSinceLastRun"] = (decimal)Math.Round(Math.Max(0, (now.UtcDateTime - started).TotalHours), 1);
                return (status, metrics);
            }));
            list.Add(Check("DB-004", HealthCategory.Database, () =>
                BoundedAsync(logs.Table, settings.LogWarningCount, "LogRecords")));
            list.Add(Check("DB-005", HealthCategory.Database, () =>
                BoundedAsync(activityLogs.Table, settings.ActivityLogWarningCount, "ActivityLogRecords")));
            list.Add(Check("DB-006", HealthCategory.Database, () =>
            {
                var cutoff = Utc(-30);
                return BoundedAsync(queue.Table.Where(email => email.SentOnUtc != null && email.SentOnUtc < cutoff),
                    settings.SentEmailWarningCount, "SentEmailsOlderThan30Days");
            }));
            list.Add(Check("DB-007", HealthCategory.Database, () =>
            {
                var cutoff = Utc(-90);
                return BoundedAsync(carts.Table.Where(item => item.UpdatedOnUtc < cutoff),
                    settings.OldCartWarningCount, "CartItemsOlderThan90Days");
            }));

            // Email accounts
            list.Add(Check("EMAIL-005", HealthCategory.Email, async () =>
            {
                var accounts = await emailAccounts.Table
                    .Select(account => new { account.Id, account.Email, account.Host, account.Port }).ToListAsync(token);
                var defaultId = (await settingService.LoadSettingAsync<EmailAccountSettings>()).DefaultEmailAccountId;
                var (status, invalid) = CommunityRules.EmailAccounts(accounts.Select(account =>
                    new CommunityRules.EmailAccountObservation(account.Email, account.Host, account.Port)).ToArray(),
                    accounts.Any(account => account.Id == defaultId));
                return (status, new() { ["EmailAccounts"] = accounts.Count, ["InvalidEmailAccounts"] = invalid,
                    ["DefaultAccountFound"] = accounts.Any(account => account.Id == defaultId) ? 1 : 0 });
            }));
            list.Add(Check("EMAIL-006", HealthCategory.Email, async () =>
            {
                var orphaned = await queue.Table.CountAsync(email => email.SentOnUtc == null &&
                    !emailAccounts.Table.Any(account => account.Id == email.EmailAccountId), token);
                return (orphaned > 0 ? HealthStatus.Critical : HealthStatus.Healthy,
                    new() { ["EmailsWithMissingAccount"] = orphaned });
            }));

            // Configuration and security
            list.Add(Check("CONFIG-004", HealthCategory.Configuration, async () =>
            {
                if (!settings.NetworkChecksEnabled)
                    return (HealthStatus.NotApplicable, new() { ["NetworkChecksEnabled"] = 0 });
                var store = await stores.GetStoreByIdAsync(storeId);
                if (store == null || !Uri.TryCreate(store.Url, UriKind.Absolute, out var url) || url.Scheme != Uri.UriSchemeHttps)
                    return (HealthStatus.NotApplicable, new() { ["NetworkChecksEnabled"] = 1 });
                var (days, trusted) = await ReadCertificateAsync(url.IdnHost, url.Port, token);
                var status = CommunityRules.CertificateExpiry(days);
                if (status == HealthStatus.Healthy && !trusted)
                    status = HealthStatus.Warning;
                return (status, days.HasValue
                    ? new() { ["CertificateDaysLeft"] = (decimal)Math.Floor(days.Value), ["CertificateTrusted"] = trusted ? 1 : 0 }
                    : new());
            }, HealthScope.Store));
            list.Add(Check("CONFIG-008", HealthCategory.Configuration, async () =>
            {
                // nopCommerce's own Warnings page needs a request (it compares the current host).
                if (httpContextAccessor.HttpContext == null)
                    return (HealthStatus.NotApplicable, new());
                var (fail, warning) = await CoreWarningsAsync();
                return (fail > 0 ? HealthStatus.Critical : warning > 0 ? HealthStatus.Warning : HealthStatus.Healthy,
                    new() { ["CoreFailures"] = fail, ["CoreWarnings"] = warning });
            }));
            list.Add(Check("SEC-001", HealthCategory.Configuration, () =>
            {
                var enabled = appSettings.Get<CommonConfig>()?.DisplayFullErrorStack == true;
                return Task.FromResult((enabled ? HealthStatus.Warning : HealthStatus.Healthy,
                    new Dictionary<string, decimal> { ["FullErrorStackEnabled"] = enabled ? 1 : 0 }));
            }));
            list.Add(Check("SEC-002", HealthCategory.Configuration, async () =>
            {
                var captcha = await settingService.LoadSettingAsync<CaptchaSettings>(storeId);
                var protectedRegistration = captcha.Enabled && captcha.ShowOnRegistrationPage;
                return (protectedRegistration ? HealthStatus.Healthy : HealthStatus.Warning, new()
                {
                    ["CaptchaEnabled"] = captcha.Enabled ? 1 : 0,
                    ["CaptchaOnRegistration"] = captcha.Enabled && captcha.ShowOnRegistrationPage ? 1 : 0
                });
            }, HealthScope.Store));
            list.Add(Check("SEC-003", HealthCategory.Configuration, async () =>
            {
                var adminRole = await roles.Table.Where(role => role.SystemName == NopCustomerDefaults.AdministratorsRoleName)
                    .Select(role => role.Id).FirstOrDefaultAsync(token);
                var count = await (from mapping in roleMappings.Table
                    join customer in customers.Table on mapping.CustomerId equals customer.Id
                    where mapping.CustomerRoleId == adminRole && !customer.Deleted && customer.Active &&
                        customer.Email != null && customer.Email.ToLower() == SampleAdminEmail
                    select customer.Id).CountAsync(token);
                return (count > 0 ? HealthStatus.Warning : HealthStatus.Healthy, new() { ["SampleAdminAccounts"] = count });
            }));

            // Plugins and platform
            list.Add(Check("PLUGIN-004", HealthCategory.Plugins, async () =>
            {
                var incompatible = plugins.GetIncompatiblePlugins()?.Count ?? 0;
                var since = Utc(-7);
                var loadErrors = await (await LogWindow.SinceAsync(logs, since, token)).CountAsync(log =>
                    log.LogLevelId >= (int)LogLevel.Error && log.ShortMessage != null &&
                    (log.ShortMessage.Contains("MissingMethodException") || log.ShortMessage.Contains("Method not found") ||
                     log.ShortMessage.Contains("TypeLoadException") || log.ShortMessage.Contains("Could not load type") ||
                     log.ShortMessage.Contains("Could not load file or assembly")), token);
                return (incompatible + loadErrors > 0 ? HealthStatus.Warning : HealthStatus.Healthy,
                    new() { ["IncompatiblePlugins"] = incompatible, ["LoadErrors7Days"] = loadErrors });
            }));
            list.Add(Check("PLUGIN-006", HealthCategory.Plugins, () =>
            {
                var affected = KnownIssues.For(NopVersion.FULL_VERSION).Count;
                return Task.FromResult((affected > 0 ? HealthStatus.Warning : HealthStatus.Healthy,
                    new Dictionary<string, decimal> { ["KnownIssues"] = affected, ["IssuesInCatalog"] = KnownIssues.All.Count }));
            }));

        if (!settings.CatalogChecksEnabled)
            return list;

            // Catalog quality (separate score)
            list.Add(Catalog("CAT-001", "ProductsWithoutImages", () => Visible().CountAsync(product =>
                !productPictures.Table.Any(picture => picture.ProductId == product.Id), token)));
            list.Add(Catalog("CAT-003", "DuplicateSkus", () => products.Table
                .Where(product => !product.Deleted && product.Sku != null && product.Sku != "")
                .GroupBy(product => product.Sku).Where(group => group.Count() > 1).Select(group => group.Key).CountAsync(token)));
            list.Add(Catalog("CAT-004", "ProductsWithoutMetaDescription", () => Visible().CountAsync(product =>
                product.MetaDescription == null || product.MetaDescription == "", token)));
            list.Add(Catalog("CAT-005", "ZeroPriceProducts", () => Visible().CountAsync(product =>
                product.ProductTypeId == (int)ProductType.SimpleProduct && product.Price == 0 &&
                !product.CallForPrice && !product.CustomerEntersPrice, token)));
            list.Add(Catalog("CAT-007", "ProductsWithoutCategory", () => Visible().CountAsync(product =>
                !productCategories.Table.Any(mapping => mapping.ProductId == product.Id), token)));
            list.Add(Catalog("CAT-009", "DuplicateUrlNames", () => urlRecords.Table
                .Where(record => record.IsActive)
                .GroupBy(record => new { record.Slug, record.LanguageId }).Where(group => group.Count() > 1)
                .Select(group => group.Key.Slug).CountAsync(token)));
        return list;
    }

        private async Task<(int Fail, int Warning)> CoreWarningsAsync()
        {
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("Nop.Web.Areas.Admin.Factories.ICommonModelFactory", false))
                .FirstOrDefault(found => found != null) ?? throw new InvalidOperationException("Warnings factory unavailable.");
            var factory = services.GetService(type) ?? throw new InvalidOperationException("Warnings factory unavailable.");
            var task = (Task)type.GetMethod("PrepareSystemWarningModelsAsync")!.Invoke(factory, null)!;
            await task;
            var items = (IEnumerable)task.GetType().GetProperty("Result")!.GetValue(task)!;
            int fail = 0, warning = 0;
            foreach (var item in items)
            {
                var level = item.GetType().GetProperty("Level")?.GetValue(item)?.ToString();
                if (level == "Fail") fail++;
                else if (level == "Warning") warning++;
            }
            return (fail, warning);
        }


    /// <summary>Reads only the certificate. No HTTP request is sent and no data is transmitted.</summary>
    private static async Task<(double? DaysLeft, bool Trusted)> ReadCertificateAsync(string host, int port,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        using var client = new TcpClient();
        await client.ConnectAsync(host, port, timeout.Token);
        DateTime? notAfter = null;
        var trusted = false;
        await using var ssl = new SslStream(client.GetStream(), false, (_, certificate, _, errors) =>
        {
            if (certificate is X509Certificate2 parsed)
                notAfter = parsed.NotAfter.ToUniversalTime();
            trusted = errors == SslPolicyErrors.None;
            return true; // Inspect expiry even when the chain is untrusted; nothing is sent afterwards.
        });
        await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost = host }, timeout.Token);
        return (notAfter.HasValue ? (notAfter.Value - DateTime.UtcNow).TotalDays : null, trusted);
    }

    private static Dictionary<string, decimal> Bounded(string metric, long count, int limit)
    {
        var metrics = new Dictionary<string, decimal> { [metric] = count };
        if (count >= limit)
            metrics[CountIsMinimum] = 1;
        return metrics;
    }

    private static DateTimeOffset? Offset(DateTime? date) => date.HasValue
        ? new DateTimeOffset(DateTime.SpecifyKind(date.Value, DateTimeKind.Utc)) : null;

    private sealed class ExtendedCheck(string id, HealthCategory category, HealthScope scope,
        Func<CancellationToken, Task<HealthCheckResult>> execute) : IHealthCheck
    {
        public string CheckId => id;
        public HealthCategory Category => category;
        public HealthScope Scope => scope;
        public Task<HealthCheckResult> ExecuteAsync(HealthCheckContext context, CancellationToken cancellationToken)
            => execute(cancellationToken);
    }
}
