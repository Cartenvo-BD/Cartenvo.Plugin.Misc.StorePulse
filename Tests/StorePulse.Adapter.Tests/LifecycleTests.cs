using Cartenvo.Plugin.Misc.StorePulse;
using Cartenvo.Plugin.Misc.StorePulse.Infrastructure;
using Moq;
using Nop.Core;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Security;
using NUnit.Framework;
using Nop.Services.ScheduleTasks;
using Nop.Core.Domain.ScheduleTasks;
using Cartenvo.Plugin.Misc.StorePulse.Services;

namespace StorePulse.Adapter.Tests;

public class LifecycleTests
{
    [Test]
    public async Task UninstallDeletesOnlyPluginOwnedSettingsResourcesAndPermissions()
    {
        var settings = new Mock<ISettingService>(MockBehavior.Strict);
        settings.Setup(service => service.DeleteSettingAsync<StorePulseSettings>()).Returns(Task.CompletedTask);
        var localization = new Mock<ILocalizationService>(MockBehavior.Strict);
        localization.Setup(service => service.DeleteLocaleResourcesAsync("Plugins.Misc.StorePulse.", null)).Returns(Task.CompletedTask);
        var permissions = new Mock<IPermissionService>(MockBehavior.Strict);
        foreach (var config in new StorePulsePermissions().AllConfigs)
            permissions.Setup(service => service.DeletePermissionAsync(config.SystemName)).Returns(Task.CompletedTask);
        var owned = new ScheduleTask { Type = StorePulseSchedule.TaskType };
        var tasks = new Mock<IScheduleTaskService>(MockBehavior.Strict);
        tasks.Setup(service => service.GetAllTasksAsync(true)).ReturnsAsync(new List<ScheduleTask> { owned, new() { Type = "Other.Plugin.Task" } });
        tasks.Setup(service => service.DeleteTaskAsync(owned)).Returns(Task.CompletedTask);
        await new StorePulsePlugin(settings.Object, localization.Object, permissions.Object, Mock.Of<IWebHelper>(), tasks.Object, new Nop.Core.Domain.Cms.WidgetSettings()).UninstallAsync();
        tasks.Verify(service => service.DeleteTaskAsync(owned), Times.Once);
        settings.Verify(service => service.DeleteSettingAsync<StorePulseSettings>(), Times.Once);
        localization.Verify(service => service.DeleteLocaleResourcesAsync("Plugins.Misc.StorePulse.", null), Times.Once);
        foreach (var config in new StorePulsePermissions().AllConfigs)
            permissions.Verify(service => service.DeletePermissionAsync(config.SystemName), Times.Once);
        settings.VerifyNoOtherCalls(); localization.VerifyNoOtherCalls(); permissions.VerifyNoOtherCalls();
    }
}
