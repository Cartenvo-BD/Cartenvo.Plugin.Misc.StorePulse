using Nop.Core.Domain.ScheduleTasks;
using Nop.Services.Configuration;
using Nop.Services.ScheduleTasks;
using Nop.Services.Stores;

namespace Cartenvo.Plugin.Misc.StorePulse.Services;

public class StorePulseSchedule(ISettingService settings, IStoreService stores, IScheduleTaskService tasks)
{
    public const string TaskType = "Cartenvo.Plugin.Misc.StorePulse.Services.HealthScanTask, Cartenvo.Plugin.Misc.StorePulse";

    public async Task<bool> IsEnabledAsync(int storeId, StorePulseSettings configuration) =>
        configuration.ScheduledScanningEnabled && await settings.SettingExistsAsync(configuration,
            item => item.ScheduledScanningEnabled, storeId);

    public async Task RefreshAsync()
    {
        var enabled = false;
        foreach (var store in await stores.GetAllStoresAsync())
            if (await IsEnabledAsync(store.Id, await settings.LoadSettingAsync<StorePulseSettings>(store.Id)))
                enabled = true;
        var task = await tasks.GetTaskByTypeAsync(TaskType);
        if (task == null)
        {
            await tasks.InsertTaskAsync(new ScheduleTask { Type = TaskType, Name = "StorePulse health scans",
                Seconds = 3600, Enabled = enabled, StopOnError = false,
                LastEnabledUtc = enabled ? DateTime.UtcNow : null });
        }
        else if (task.Enabled != enabled)
        {
            task.Enabled = enabled;
            if (enabled) task.LastEnabledUtc = DateTime.UtcNow;
            await tasks.UpdateTaskAsync(task);
        }
    }
}

public class HealthScanTask(IStoreService stores, ISettingService settings, StorePulseSchedule schedule,
    StorePulseScanner scanner, ScanHistory history) : IScheduleTask
{
    public async Task ExecuteAsync()
    {
        var failed = false;
        foreach (var store in await stores.GetAllStoresAsync())
        {
            try
            {
                var configuration = await settings.LoadSettingAsync<StorePulseSettings>(store.Id);
                if (!await schedule.IsEnabledAsync(store.Id, configuration)) continue;
                var latest = (await history.RecentAsync(store.Id)).FirstOrDefault();
                if (latest != null && DateTime.UtcNow - latest.CompletedOnUtc <
                    TimeSpan.FromHours(Math.Clamp(configuration.ScheduledScanIntervalHours, 1, 168))) continue;
                await scanner.RunAsync(store.Id, configuration, CancellationToken.None);
            }
            catch { failed = true; } // Continue other stores; never log raw source exception details.
        }
        if (failed) throw new InvalidOperationException("StorePulse could not complete one or more scheduled store scans.");
    }
}
