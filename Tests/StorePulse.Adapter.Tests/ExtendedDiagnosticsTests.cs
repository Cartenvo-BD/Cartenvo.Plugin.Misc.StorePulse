using Cartenvo.Plugin.Misc.StorePulse;
using Cartenvo.Plugin.Misc.StorePulse.Domain;
using Cartenvo.Plugin.Misc.StorePulse.Infrastructure;
using Cartenvo.Plugin.Misc.StorePulse.Services;
using Microsoft.AspNetCore.Http;
using Moq;
using Nop.Core.Configuration;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Logging;
using Nop.Core.Domain.Messages;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Seo;
using Nop.Data;
using Nop.Services.Configuration;
using Nop.Services.Logging;
using Nop.Services.Plugins;
using Nop.Services.ScheduleTasks;
using Nop.Services.Stores;
using NUnit.Framework;
using StorePulse.Core;

namespace StorePulse.Adapter.Tests;

public class ExtendedDiagnosticsTests
{
    private static IRepository<T> Failing<T>() where T : Nop.Core.BaseEntity
    {
        var repository = new Mock<IRepository<T>>();
        repository.SetupGet(item => item.Table).Throws(new Exception("Server=db;Password=secret customer@example.com"));
        return repository.Object;
    }

    private static ExtendedDiagnostics Create(IHttpContextAccessor http = null)
    {
        var settings = new Mock<ISettingService>();
        settings.Setup(service => service.LoadSettingAsync<Nop.Core.Domain.Security.CaptchaSettings>(It.IsAny<int>()))
            .ReturnsAsync(new Nop.Core.Domain.Security.CaptchaSettings { Enabled = true, ShowOnRegistrationPage = true });
        var plugins = new Mock<IPluginService>();
        plugins.Setup(service => service.GetIncompatiblePlugins()).Returns(new Dictionary<string, PluginIncompatibleType>());
        return new ExtendedDiagnostics(Failing<Customer>(), Failing<CustomerRole>(), Failing<CustomerCustomerRoleMapping>(),
            Failing<Order>(), Failing<ActivityLog>(), Failing<ShoppingCartItem>(), Failing<QueuedEmail>(),
            Failing<EmailAccount>(), Failing<Log>(), Failing<Product>(), Failing<ProductPicture>(),
            Failing<ProductCategory>(), Failing<UrlRecord>(), Mock.Of<IScheduleTaskService>(), Mock.Of<IStoreService>(),
            settings.Object, plugins.Object, new AppSettings(new List<IConfig> { new CommonConfig() }), http ?? Mock.Of<IHttpContextAccessor>(),
            Mock.Of<IServiceProvider>());
    }

    [Test]
    public async Task EveryNewCheckHasTextsAndIsolatesFailures()
    {
        var checks = Create().Create(1, DateTimeOffset.UtcNow, new StorePulseSettings(), CancellationToken.None).ToList();
        var results = await new HealthCheckRunner().RunAsync(new HealthCheckRegistry(checks),
            new HealthCheckContext(1, DateTimeOffset.UtcNow), TimeSpan.FromSeconds(30), CancellationToken.None);

        Assert.That(results.Count, Is.EqualTo(22));
        var resources = StorePulseResources.All();
        foreach (var result in results)
        {
            Assert.That(resources.ContainsKey($"Plugins.Misc.StorePulse.{result.CheckId}.Title"), result.CheckId);
            Assert.That(resources.ContainsKey($"Plugins.Misc.StorePulse.{result.CheckId}.Why"), result.CheckId);
            Assert.That(resources.ContainsKey($"Plugins.Misc.StorePulse.{result.CheckId}.Action"), result.CheckId);
            foreach (var metric in result.Metrics.Keys)
                Assert.That(resources.ContainsKey($"Plugins.Misc.StorePulse.Metric.{metric}"), metric);
            Assert.That(StorePulseLinks.AdminPath(result.CheckId), Is.Not.Null, result.CheckId);
        }
        Assert.That(results.Where(r => r.CheckId.StartsWith("DB-")).All(r => r.Status == HealthStatus.Unknown), Is.True);
        Assert.That(results.Single(r => r.CheckId == "SEC-002").Status, Is.EqualTo(HealthStatus.Healthy));
        Assert.That(results.Single(r => r.CheckId == "SEC-001").Status, Is.EqualTo(HealthStatus.Healthy));
        Assert.That(results.Single(r => r.CheckId == "CONFIG-004").Status, Is.EqualTo(HealthStatus.NotApplicable),
            "Network checks are off by default");
        Assert.That(results.Single(r => r.CheckId == "CONFIG-008").Status, Is.EqualTo(HealthStatus.NotApplicable),
            "Core warnings need a request; background scans skip them");
        var json = System.Text.Json.JsonSerializer.Serialize(results);
        Assert.That(json, Does.Not.Contain("secret").And.Not.Contain("customer@example.com"));
    }

    [Test]
    public void CatalogChecksCanBeTurnedOff()
    {
        var checks = Create().Create(1, DateTimeOffset.UtcNow, new StorePulseSettings { CatalogChecksEnabled = false },
            CancellationToken.None);
        Assert.That(checks.Any(check => check.Category == HealthCategory.Catalog), Is.False);
    }

    [Test]
    public void ExistingChecksHaveWhyTexts()
    {
        var resources = StorePulseResources.All();
        foreach (var id in new[] { "TASK-001", "TASK-002", "TASK-003", "TASK-006", "EMAIL-001", "EMAIL-002", "EMAIL-003",
                     "EMAIL-004", "LOG-001", "LOG-002", "LOG-003", "LOG-005", "LOG-006", "CONFIG-001", "CONFIG-002",
                     "PLUGIN-002", "PLUGIN-003" })
            Assert.That(resources.ContainsKey($"Plugins.Misc.StorePulse.{id}.Why"), id);
    }
}
