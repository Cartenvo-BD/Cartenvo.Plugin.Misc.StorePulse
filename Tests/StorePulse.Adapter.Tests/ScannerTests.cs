using Cartenvo.Plugin.Misc.StorePulse;
using Cartenvo.Plugin.Misc.StorePulse.Domain;
using Cartenvo.Plugin.Misc.StorePulse.Services;
using Moq;
using Nop.Core.Domain.Messages;
using Nop.Core.Domain.Stores;
using Nop.Data;
using Nop.Services.Logging;
using Nop.Services.Plugins;
using Nop.Services.ScheduleTasks;
using Nop.Services.Stores;
using NUnit.Framework;
using StorePulse.Core;

namespace StorePulse.Adapter.Tests;

public class ScannerTests
{
    [Test]
    public async Task SourceFailuresAreUnknownAndDoNotLeakExceptionText()
    {
        var tasks = new Mock<IScheduleTaskService>();
        tasks.Setup(service => service.GetAllTasksAsync(true)).ThrowsAsync(new Exception("private@example.com password=secret"));
        var queue = new Mock<IRepository<QueuedEmail>>();
        queue.SetupGet(repository => repository.Table).Throws(new Exception("connection string secret"));
        var stores = new Mock<IStoreService>();
        stores.Setup(service => service.GetStoreByIdAsync(1)).ReturnsAsync(new Store { Id = 1, Url = "https://example.com/", SslEnabled = true });
        var plugins = new Mock<IPluginService>();
        plugins.Setup(service => service.GetPluginDescriptorsAsync<IPlugin>(LoadPluginsMode.InstalledOnly, null, 0, null, "", null, null))
            .ReturnsAsync(new List<PluginDescriptor>());
        var repository = new Mock<IRepository<StorePulseScan>>();
        repository.SetReturnsDefault(Task.FromResult<IList<StorePulseScan>>(new List<StorePulseScan>()));
        StorePulseScan saved = null;
        repository.Setup(service => service.InsertAsync(It.IsAny<StorePulseScan>(), false))
            .Callback<StorePulseScan, bool>((scan, _) => saved = scan).Returns(Task.CompletedTask);
        var scanner = new StorePulseScanner(tasks.Object, queue.Object, new Mock<ILogger>().Object,
            stores.Object, plugins.Object, new ScanHistory(repository.Object), new StorePulseSettings());

        Assert.That(await scanner.RunAsync(1, CancellationToken.None), Is.True);
        Assert.That(saved, Is.Not.Null);
        var findings = ScanHistory.Read(saved);
        Assert.That(findings.Count, Is.EqualTo(17));
        Assert.That(findings.Single(finding => finding.CheckId == "TASK-001").Status, Is.EqualTo(HealthStatus.Unknown));
        Assert.That(findings.Single(finding => finding.CheckId == "EMAIL-001").Status, Is.EqualTo(HealthStatus.Unknown));
        Assert.That(findings.Single(finding => finding.CheckId == "CONFIG-001").Status, Is.EqualTo(HealthStatus.Healthy));
        Assert.That(saved.FindingsJson, Does.Not.Contain("private@example.com").And.Not.Contain("secret"));
        Assert.That(repository.Invocations.Where(call => call.Method.Name == "GetAllAsync")
            .All(call => call.Arguments[1] == null), Is.True, "History must not use the default shared cache key");
        Assert.That(await scanner.RunAsync(1, CancellationToken.None), Is.False, "Immediate repeat must respect cooldown");
        Assert.That(await scanner.RunAsync(2, new StorePulseSettings(), CancellationToken.None), Is.True,
            "A scan for a different store must not be suppressed by the first store's cooldown");
    }

    [Test]
    public void CanceledScanDoesNotEnterSources()
    {
        var tasks = new Mock<IScheduleTaskService>(MockBehavior.Strict);
        var scanner = new StorePulseScanner(tasks.Object, Mock.Of<IRepository<QueuedEmail>>(), Mock.Of<ILogger>(),
            Mock.Of<IStoreService>(), Mock.Of<IPluginService>(), new ScanHistory(Mock.Of<IRepository<StorePulseScan>>()), new StorePulseSettings());
        Assert.CatchAsync<OperationCanceledException>(async () => await scanner.RunAsync(1, new CancellationToken(true)));
        tasks.VerifyNoOtherCalls();
    }
}

