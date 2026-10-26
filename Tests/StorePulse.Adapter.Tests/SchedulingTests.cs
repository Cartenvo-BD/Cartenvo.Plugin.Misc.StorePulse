using System.Linq.Expressions;
using Cartenvo.Plugin.Misc.StorePulse;
using Cartenvo.Plugin.Misc.StorePulse.Domain;
using Cartenvo.Plugin.Misc.StorePulse.Services;
using Moq;
using Nop.Core.Domain.Messages;
using Nop.Core.Domain.ScheduleTasks;
using Nop.Core.Domain.Stores;
using Nop.Data;
using Nop.Services.Configuration;
using Nop.Services.Logging;
using Nop.Services.Plugins;
using Nop.Services.ScheduleTasks;
using Nop.Services.Stores;
using NUnit.Framework;

namespace StorePulse.Adapter.Tests;

public class SchedulingTests
{
    [Test]
    public async Task ScansOnlyExplicitlyEnabledDueStoresWithTheirOwnSettings()
    {
        var settings = new Mock<ISettingService>();
        var stores = new Mock<IStoreService>();
        stores.Setup(service => service.GetAllStoresAsync()).ReturnsAsync(Enumerable.Range(1, 4).Select(id => new Store { Id = id }).ToList());
        var configurations = Enumerable.Range(1, 4).ToDictionary(id => id, id => new StorePulseSettings
            { ScheduledScanningEnabled = id != 3, EmailWarningMinutes = id * 10 });
        foreach (var (id, configuration) in configurations)
            settings.Setup(service => service.LoadSettingAsync<StorePulseSettings>(id)).ReturnsAsync(configuration);
        settings.Setup(service => service.SettingExistsAsync(It.IsAny<StorePulseSettings>(),
            It.IsAny<Expression<Func<StorePulseSettings, bool>>>(), It.IsAny<int>()))
            .ReturnsAsync((StorePulseSettings _, Expression<Func<StorePulseSettings, bool>> _, int id) => id != 2);
        var history = new Mock<ScanHistory>(Mock.Of<IRepository<StorePulseScan>>());
        history.Setup(service => service.RecentAsync(It.IsAny<int>())).ReturnsAsync(new List<StorePulseScan>());
        history.Setup(service => service.RecentAsync(4)).ReturnsAsync(new List<StorePulseScan> { new() { CompletedOnUtc = DateTime.UtcNow } });
        var scanner = Scanner(history.Object);
        scanner.Setup(service => service.RunAsync(1, configurations[1], CancellationToken.None)).ReturnsAsync(true);
        await new HealthScanTask(stores.Object, settings.Object,
            new StorePulseSchedule(settings.Object, stores.Object, Mock.Of<IScheduleTaskService>()), scanner.Object, history.Object).ExecuteAsync();
        scanner.Verify(service => service.RunAsync(1, configurations[1], CancellationToken.None), Times.Once);
        scanner.VerifyNoOtherCalls();
    }

    [Test]
    public void FailedStoreDoesNotPreventOtherEnabledStoresFromRunning()
    {
        var configuration = new StorePulseSettings { ScheduledScanningEnabled = true };
        var settings = new Mock<ISettingService>();
        settings.Setup(service => service.LoadSettingAsync<StorePulseSettings>(It.IsAny<int>())).ReturnsAsync(configuration);
        settings.Setup(service => service.SettingExistsAsync(configuration,
            It.IsAny<Expression<Func<StorePulseSettings, bool>>>(), It.IsAny<int>())).ReturnsAsync(true);
        var stores = new Mock<IStoreService>();
        stores.Setup(service => service.GetAllStoresAsync()).ReturnsAsync(new List<Store> { new() { Id = 1 }, new() { Id = 2 } });
        var history = new Mock<ScanHistory>(Mock.Of<IRepository<StorePulseScan>>());
        history.Setup(service => service.RecentAsync(It.IsAny<int>())).ReturnsAsync(new List<StorePulseScan>());
        var scanner = Scanner(history.Object);
        scanner.Setup(service => service.RunAsync(1, configuration, CancellationToken.None)).ThrowsAsync(new Exception("private source failure"));
        scanner.Setup(service => service.RunAsync(2, configuration, CancellationToken.None)).ReturnsAsync(true);
        var task = new HealthScanTask(stores.Object, settings.Object,
            new StorePulseSchedule(settings.Object, stores.Object, Mock.Of<IScheduleTaskService>()), scanner.Object, history.Object);
        var failure = Assert.ThrowsAsync<InvalidOperationException>(task.ExecuteAsync);
        Assert.That(failure.Message, Does.Not.Contain("private"));
        scanner.Verify(service => service.RunAsync(2, configuration, CancellationToken.None), Times.Once);
    }

    [TestCase(true, false)]
    [TestCase(false, true)]
    public async Task RefreshTracksExplicitOptInWithoutCreatingDuplicateTask(bool enabled, bool existingEnabled)
    {
        var configuration = new StorePulseSettings { ScheduledScanningEnabled = enabled };
        var settings = new Mock<ISettingService>();
        settings.Setup(service => service.LoadSettingAsync<StorePulseSettings>(1)).ReturnsAsync(configuration);
        settings.Setup(service => service.SettingExistsAsync(configuration,
            It.IsAny<Expression<Func<StorePulseSettings, bool>>>(), 1)).ReturnsAsync(true);
        var stores = new Mock<IStoreService>();
        stores.Setup(service => service.GetAllStoresAsync()).ReturnsAsync(new List<Store> { new() { Id = 1 } });
        var registration = new ScheduleTask { Type = StorePulseSchedule.TaskType, Enabled = existingEnabled };
        var tasks = new Mock<IScheduleTaskService>(MockBehavior.Strict);
        tasks.Setup(service => service.GetTaskByTypeAsync(StorePulseSchedule.TaskType)).ReturnsAsync(registration);
        tasks.Setup(service => service.UpdateTaskAsync(registration)).Returns(Task.CompletedTask);
        await new StorePulseSchedule(settings.Object, stores.Object, tasks.Object).RefreshAsync();
        Assert.That(registration.Enabled, Is.EqualTo(enabled));
        if (enabled) Assert.That(registration.LastEnabledUtc, Is.Not.Null);
        tasks.Verify(service => service.UpdateTaskAsync(registration), Times.Once);
    }

    [Test]
    public async Task NoOptInRegistersDisabledTaskAndDoesNotTouchUnrelatedTasks()
    {
        var stores = new Mock<IStoreService>();
        stores.Setup(service => service.GetAllStoresAsync()).ReturnsAsync(new List<Store>());
        var tasks = new Mock<IScheduleTaskService>(MockBehavior.Strict);
        tasks.Setup(service => service.GetTaskByTypeAsync(StorePulseSchedule.TaskType)).ReturnsAsync((ScheduleTask)null);
        tasks.Setup(service => service.InsertTaskAsync(It.Is<ScheduleTask>(task => !task.Enabled &&
            task.Type == StorePulseSchedule.TaskType && task.Seconds == 3600 && !task.StopOnError))).Returns(Task.CompletedTask);
        await new StorePulseSchedule(Mock.Of<ISettingService>(), stores.Object, tasks.Object).RefreshAsync();
        tasks.Verify(service => service.InsertTaskAsync(It.IsAny<ScheduleTask>()), Times.Once);
    }

    private static Mock<StorePulseScanner> Scanner(ScanHistory history) => new(MockBehavior.Strict,
        Mock.Of<IScheduleTaskService>(), Mock.Of<IRepository<QueuedEmail>>(), Mock.Of<ILogger>(),
        Mock.Of<IStoreService>(), Mock.Of<IPluginService>(), history, new StorePulseSettings(), null, null, null);
}
