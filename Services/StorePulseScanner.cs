using Cartenvo.Plugin.Misc.StorePulse.Domain;
using LinqToDB;
using Nop.Core;
using Nop.Core.Domain.Messages;
using Nop.Core.Domain.Logging;
using Nop.Data;
using Nop.Services.Logging;
using Nop.Services.Plugins;
using Nop.Services.ScheduleTasks;
using Nop.Services.Stores;
using StorePulse.Core;

namespace Cartenvo.Plugin.Misc.StorePulse.Services;

public class StorePulseScanner(IScheduleTaskService tasks, IRepository<QueuedEmail> queue,
    ILogger logger, IStoreService stores, IPluginService plugins, ScanHistory history, StorePulseSettings settings,
    IRepository<Log> logRepository = null, ExtendedDiagnostics extended = null,
    CatalogAudit catalog = null)
{
    // Global sources are read once per scan. Keep the gate held until all DB work completes.
    // This gate protects one application process; multi-instance coordination is not claimed.
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly Dictionary<int, DateTimeOffset> LastAttempts = new();

    public async Task<bool> RunAsync(int storeId, CancellationToken cancellationToken)
        => await RunAsync(storeId, settings, cancellationToken);

    public virtual async Task<bool> RunAsync(int storeId, StorePulseSettings settings, CancellationToken cancellationToken)
    {
        if (!await Gate.WaitAsync(0, cancellationToken))
            return false;
        try
        {
            var now = DateTimeOffset.UtcNow;
            if (LastAttempts.TryGetValue(storeId, out var lastAttempt) && now - lastAttempt < TimeSpan.FromSeconds(30))
                return false;
            foreach (var expired in LastAttempts.Where(item => now - item.Value >= TimeSpan.FromSeconds(30)).Select(item => item.Key).ToArray())
                LastAttempts.Remove(expired);
            LastAttempts[storeId] = now;
            var checks = new List<IHealthCheck>();
            HealthCheckResult currentResult = null;
            var taskList = new List<Nop.Core.Domain.ScheduleTasks.ScheduleTask>();
            var taskRead = false;
            var readyCount = 0;
            var readyRead = false;
            var overdue = 0;
            var grace = TimeSpan.FromMinutes(Math.Clamp(settings.TaskGraceMinutes, 1, 1440));
            var multiplier = Math.Clamp(settings.TaskOverdueMultiplier, 1, 20);
            // Per-check token from the runner, so a slow query is cancelled instead of stalling the scan.
            var ct = cancellationToken;

            void Add(string id, HealthCategory category, HealthStatus status, Dictionary<string, decimal> metrics = null,
                HealthScope scope = HealthScope.System) => currentResult = new(id, category, scope, status,
                    $"Plugins.Misc.StorePulse.{id}.Title", $"Plugins.Misc.StorePulse.Status.{status}",
                    $"Plugins.Misc.StorePulse.{id}.Action", now, metrics ?? new());

            void Check(string id, HealthCategory category, Func<Task> run,
                HealthScope scope = HealthScope.System)
            {
                checks.Add(new RegisteredCheck(id, category, scope, async checkToken =>
                {
                    ct = checkToken;
                    currentResult = null;
                    await run();
                    return currentResult ?? throw new InvalidOperationException("Missing diagnostic result.");
                }));
            }
            Check("TASK-001", HealthCategory.ScheduledTasks, async () =>
            {
                taskList = (await tasks.GetAllTasksAsync(true)).Where(task =>
                    !(task.Type ?? "").Contains("StorePulse", StringComparison.OrdinalIgnoreCase)).ToList();
                taskRead = true;
                var statuses = taskList.Select(task => TaskHealthRules.Overdue(Observation(task), now, grace, multiplier)).ToArray();
                overdue = statuses.Count(status => status == HealthStatus.Warning);
                Add("TASK-001", HealthCategory.ScheduledTasks, Combine(statuses), new()
                {
                    ["OverdueTasks"] = overdue, ["TasksExamined"] = taskList.Count,
                    ["UnknownTasks"] = statuses.Count(status => status == HealthStatus.Unknown)
                });
            });
            Check("TASK-003", HealthCategory.ScheduledTasks, () =>
            {
                var statuses = taskList.Select(task => TaskHealthRules.NeverExecuted(Observation(task), now, grace)).ToArray();
                Add("TASK-003", HealthCategory.ScheduledTasks, taskRead ? Combine(statuses) : HealthStatus.Unknown,
                    new() { ["NeverStartedTasks"] = statuses.Count(status => status == HealthStatus.Warning) });
                return Task.CompletedTask;
            });
            Check("TASK-006", HealthCategory.ScheduledTasks, () =>
            {
                var invalid = taskList.Count(task => task.Enabled && task.Seconds <= 0);
                Add("TASK-006", HealthCategory.ScheduledTasks, !taskRead ? HealthStatus.Unknown :
                    invalid > 0 ? HealthStatus.Warning : HealthStatus.Healthy, new() { ["InvalidIntervals"] = invalid });
                return Task.CompletedTask;
            });
            Check("TASK-002", HealthCategory.ScheduledTasks, () => { Add("TASK-002", HealthCategory.ScheduledTasks, HealthStatus.Unknown); return Task.CompletedTask; });

            // Project only numeric/timestamp evidence; no addresses, subjects or bodies are loaded.
            IQueryable<QueuedEmail> Eligible() => queue.Table.Where(email => email.SentOnUtc == null &&
                (email.DontSendBeforeDateUtc == null || email.DontSendBeforeDateUtc <= now.UtcDateTime));
            Check("EMAIL-001", HealthCategory.Email, async () =>
            {
                readyCount = await Eligible().Where(email => email.SentTries < 3).CountAsync(ct);
                readyRead = true;
                Add("EMAIL-001", HealthCategory.Email, readyCount >= Math.Clamp(settings.EmailBacklogWarningCount, 1, 100000)
                    ? HealthStatus.Warning : HealthStatus.Healthy, new() { ["ReadyMessages"] = readyCount });
            });
            Check("EMAIL-002", HealthCategory.Email, async () =>
            {
                var oldest = await Eligible().Where(email => email.SentTries < 3)
                    .Select(email => email.DontSendBeforeDateUtc.HasValue && email.DontSendBeforeDateUtc > email.CreatedOnUtc
                        ? email.DontSendBeforeDateUtc.Value : email.CreatedOnUtc)
                    .OrderBy(date => date).Select(date => (DateTime?)date).FirstOrDefaultAsync(ct);
                var minutes = oldest.HasValue ? Math.Max(0, (now.UtcDateTime - oldest.Value).TotalMinutes) : 0;
                Add("EMAIL-002", HealthCategory.Email, minutes > Math.Clamp(settings.EmailWarningMinutes, 1, 1440)
                    ? HealthStatus.Warning : HealthStatus.Healthy, new() { ["OldestEligibleMinutes"] = (decimal)minutes });
            });
            Check("EMAIL-003", HealthCategory.Email, async () =>
            {
                var exhausted = await Eligible().Where(email => email.SentTries >= 3).CountAsync(ct);
                Add("EMAIL-003", HealthCategory.Email, exhausted > 0 ? HealthStatus.Warning : HealthStatus.Healthy,
                    new() { ["ExhaustedMessages"] = exhausted });
            });
            Check("EMAIL-004", HealthCategory.Email, () =>
            {
                var processor = taskList.FirstOrDefault(task => (task.Type ?? "").Split(',')[0].Trim() == "Nop.Services.Messages.QueuedMessagesSendTask");
                var status = !readyRead || !taskRead ? HealthStatus.Unknown : readyCount == 0 ? HealthStatus.NotApplicable :
                    processor == null || !processor.Enabled ? HealthStatus.Critical :
                    TaskHealthRules.Overdue(Observation(processor), now, grace, multiplier) switch
                    {
                        HealthStatus.Warning => HealthStatus.Critical,
                        HealthStatus.Healthy => HealthStatus.Healthy,
                        _ => HealthStatus.Unknown
                    };
                Add("EMAIL-004", HealthCategory.Email, status, new() { ["ReadyMessages"] = readyCount });
                return Task.CompletedTask;
            });
            foreach (var (id, level) in new[] { ("LOG-001", Nop.Core.Domain.Logging.LogLevel.Fatal), ("LOG-005", Nop.Core.Domain.Logging.LogLevel.Error) })
                Check(id, HealthCategory.ApplicationErrors, async () =>
                {
                    var since = now.AddHours(-24).UtcDateTime;
                    var count = logRepository != null
                        ? await (await LogWindow.SinceAsync(logRepository, since, ct))
                            .Where(log => log.LogLevelId == (int)level && log.CreatedOnUtc <= now.UtcDateTime).CountAsync(ct)
                        : (await logger.GetAllLogsAsync(since, now.UtcDateTime, logLevel: level, pageSize: 1)).TotalCount;
                    Add(id, HealthCategory.ApplicationErrors, count > 0
                        ? (level == Nop.Core.Domain.Logging.LogLevel.Fatal ? HealthStatus.Critical : HealthStatus.Warning)
                        : HealthStatus.Healthy, new() { ["ObservedRecords24Hours"] = count });
                });
            Check("LOG-006", HealthCategory.ApplicationErrors, () => { Add("LOG-006", HealthCategory.ApplicationErrors, HealthStatus.Unknown); return Task.CompletedTask; });
            Check("LOG-002", HealthCategory.ApplicationErrors, async () =>
            {
                if (logRepository == null)
                {
                    Add("LOG-002", HealthCategory.ApplicationErrors, HealthStatus.Unknown);
                    return;
                }
                var recent = await LogWindow.SinceAsync(logRepository, now.AddHours(-2).UtcDateTime, ct);
                var current = await recent.Where(log => log.LogLevelId >= (int)LogLevel.Error &&
                    log.CreatedOnUtc >= now.AddHours(-1).UtcDateTime && log.CreatedOnUtc <= now.UtcDateTime)
                    .CountAsync(ct);
                var previous = await recent.Where(log => log.LogLevelId >= (int)LogLevel.Error &&
                    log.CreatedOnUtc >= now.AddHours(-2).UtcDateTime && log.CreatedOnUtc < now.AddHours(-1).UtcDateTime)
                    .CountAsync(ct);
                Add("LOG-002", HealthCategory.ApplicationErrors, current >= 10 && current >= Math.Max(1, previous) * 3
                    ? HealthStatus.Warning : HealthStatus.Healthy,
                    new() { ["CurrentHourErrors"] = current, ["PreviousHourErrors"] = previous });
            });
            Check("LOG-003", HealthCategory.ApplicationErrors, async () =>
            {
                if (logRepository == null)
                {
                    Add("LOG-003", HealthCategory.ApplicationErrors, HealthStatus.Unknown);
                    return;
                }
                var cap = Math.Clamp(settings.MaximumRecordsPerCheck, 10, 1000);
                var messages = await (await LogWindow.SinceAsync(logRepository, now.AddHours(-24).UtcDateTime, ct))
                    .Where(log => log.LogLevelId >= (int)LogLevel.Error && log.CreatedOnUtc <= now.UtcDateTime)
                    .OrderByDescending(log => log.CreatedOnUtc).ThenByDescending(log => log.Id)
                    .Select(log => log.ShortMessage == null ? "" : log.ShortMessage.Substring(0, Math.Min(log.ShortMessage.Length, 2048)))
                    .Take(cap + 1).ToListAsync(ct);
                var maximum = messages.Take(cap).Where(message => !string.IsNullOrWhiteSpace(message))
                    .GroupBy(ErrorFingerprint.Create).Select(group => group.Count()).DefaultIfEmpty(0).Max();
                // A repeat found in the newest records is real even when older ones were not read (busy stores).
                Add("LOG-003", HealthCategory.ApplicationErrors, maximum >= 3 ? HealthStatus.Warning :
                    messages.Count > cap ? HealthStatus.Unknown : HealthStatus.Healthy,
                    new() { ["LargestObservedGroup"] = maximum, ["RecordsExamined"] = Math.Min(cap, messages.Count),
                        ["Truncated"] = messages.Count > cap ? 1 : 0 });
            });
            Check("CONFIG-001", HealthCategory.Configuration, async () =>
            {
                var store = await stores.GetStoreByIdAsync(storeId);
                var valid = store != null && Uri.TryCreate(store.Url, UriKind.Absolute, out var url) &&
                    (url.Scheme == "http" || url.Scheme == "https") && string.IsNullOrEmpty(url.UserInfo) &&
                    string.IsNullOrEmpty(url.Query) && string.IsNullOrEmpty(url.Fragment);
                Add("CONFIG-001", HealthCategory.Configuration, valid ? HealthStatus.Healthy : HealthStatus.Warning,
                    scope: HealthScope.Store);
            }, HealthScope.Store);
            Check("CONFIG-002", HealthCategory.Configuration, async () =>
            {
                var store = await stores.GetStoreByIdAsync(storeId);
                var valid = store != null && Uri.TryCreate(store.Url, UriKind.Absolute, out var url) &&
                    (url.Scheme == "http" || url.Scheme == "https") &&
                    (url.Scheme == "https") == store.SslEnabled;
                Add("CONFIG-002", HealthCategory.Configuration, valid ? HealthStatus.Healthy : HealthStatus.Warning,
                    scope: HealthScope.Store);
            }, HealthScope.Store);
            Check("PLUGIN-002", HealthCategory.Plugins, async () =>
            {
                var installed = await plugins.GetPluginDescriptorsAsync<IPlugin>();
                var incompatible = installed.Count(plugin => !plugin.SupportedVersions.Contains(NopVersion.CURRENT_VERSION));
                Add("PLUGIN-002", HealthCategory.Plugins, incompatible > 0 ? HealthStatus.Warning : HealthStatus.Healthy,
                    new() { ["InstalledPlugins"] = installed.Count, ["UnsupportedDeclarations"] = incompatible });
            });
            Check("PLUGIN-003", HealthCategory.Plugins, async () =>
            {
                var installed = await plugins.GetPluginDescriptorsAsync<IPlugin>();
                var names = installed.Select(plugin => plugin.SystemName).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var missing = installed.Sum(plugin => (plugin.DependsOn ?? []).Count(name => !names.Contains(name)));
                Add("PLUGIN-003", HealthCategory.Plugins, missing > 0 ? HealthStatus.Warning : HealthStatus.Healthy,
                    new() { ["MissingDeclaredDependencies"] = missing });
            });
            if (extended != null)
                checks.AddRange(extended.Create(storeId, now, settings, cancellationToken));
            if (catalog != null)
                checks.AddRange(catalog.Create(storeId, now, settings));
            cancellationToken.ThrowIfCancellationRequested();
            var results = await new HealthCheckRunner().RunAsync(new HealthCheckRegistry(checks),
                new HealthCheckContext(storeId, now), TimeSpan.FromSeconds(30), cancellationToken,
                checkTimeout: TimeSpan.FromSeconds(10));
            cancellationToken.ThrowIfCancellationRequested();
            await history.SaveAsync(storeId, now.UtcDateTime, results, settings.HistoryRetentionDays);
            return true;
        }
        finally { Gate.Release(); }
    }

    private sealed class RegisteredCheck(string id, HealthCategory category, HealthScope scope,
        Func<CancellationToken, Task<HealthCheckResult>> execute) : IHealthCheck
    {
        public string CheckId => id;
        public HealthCategory Category => category;
        public HealthScope Scope => scope;
        public Task<HealthCheckResult> ExecuteAsync(HealthCheckContext context, CancellationToken cancellationToken)
            => execute(cancellationToken);
    }
    private static TaskObservation Observation(Nop.Core.Domain.ScheduleTasks.ScheduleTask task) => new(
        task.Enabled, task.Seconds, Utc(task.LastEnabledUtc), Utc(task.LastStartUtc));
    private static DateTimeOffset? Utc(DateTime? date) => date.HasValue
        ? new DateTimeOffset(DateTime.SpecifyKind(date.Value, DateTimeKind.Utc)) : null;
    private static HealthStatus Combine(HealthStatus[] statuses) => statuses.Contains(HealthStatus.Warning)
        ? HealthStatus.Warning : statuses.Contains(HealthStatus.Unknown) ? HealthStatus.Unknown :
        statuses.Contains(HealthStatus.Healthy) ? HealthStatus.Healthy : HealthStatus.NotApplicable;
}

