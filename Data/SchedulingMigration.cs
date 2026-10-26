using FluentMigrator;
using Nop.Core.Domain.ScheduleTasks;
using Nop.Core.Infrastructure;
using Nop.Data.Mapping;
using Nop.Data.Migrations;
using Nop.Services.Localization;
using Nop.Services.ScheduleTasks;
using Cartenvo.Plugin.Misc.StorePulse.Infrastructure;
using Cartenvo.Plugin.Misc.StorePulse.Services;

namespace Cartenvo.Plugin.Misc.StorePulse.Data;

[NopMigration("2026/10/26 12:05:00:0000000", "StorePulse opt-in scheduled scans", MigrationProcessType.NoMatter)]
public class SchedulingMigration : MigrationBase
{
    public override void Up()
    {
        EngineContext.Current.Resolve<ILocalizationService>().AddOrUpdateLocaleResource(StorePulseResources.All());
        if (EngineContext.Current.Resolve<IScheduleTaskService>().GetTaskByTypeAsync(StorePulseSchedule.TaskType)
            .GetAwaiter().GetResult() != null) return;
        Insert.IntoTable(NameCompatibilityManager.GetTableName(typeof(ScheduleTask))).Row(new
        {
            Name = "StorePulse health scans", Type = StorePulseSchedule.TaskType,
            Seconds = 3600, Enabled = false, StopOnError = false
        });
    }
    public override void Down() { } // The lifecycle removes only this plugin's task registrations.
}
