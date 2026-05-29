using JobRecord.Core.Abstractions;
using JobRecord.Core.Dtos;
using JobRecord.Core.Enums;
using JobRecord.Core.Models;

namespace JobRecord.Core.Services;

public sealed class RuntimeRecoveryService(IJobRecordDbContext dbContext, IClock clock) : IRuntimeRecoveryService
{
    public async Task<RuntimeRecoveryResult> RecoverAsync(CancellationToken cancellationToken = default)
    {
        var now = clock.Now;
        var fixedEntries = 0;
        var pausedTaskIds = new HashSet<Guid>();

        foreach (var entry in dbContext.TimeEntries.Where(item => item.EndAt == null).ToList())
        {
            entry.EndAt = now;
            entry.DurationSeconds = Math.Max(0, (int)(now - entry.StartAt).TotalSeconds);
            entry.EntryType = entry.EntryType == TimeEntryType.Manual ? TimeEntryType.Recovery : entry.EntryType;
            fixedEntries++;
            pausedTaskIds.Add(entry.TaskItemId);
        }

        foreach (var task in dbContext.Tasks.Where(task => pausedTaskIds.Contains(task.Id)).ToList())
        {
            task.Status = TaskStatus.Paused;
            task.UpdatedAt = now;
        }

        var runtimeState = dbContext.RuntimeStates.SingleOrDefault() ?? new RuntimeState
        {
            Id = RuntimeState.SingletonId
        };

        if (!dbContext.RuntimeStates.Any())
        {
            dbContext.Add(runtimeState);
        }

        runtimeState.CurrentTaskId = null;
        runtimeState.LastActiveAt = now;
        runtimeState.UpdatedAt = now;

        await dbContext.SaveChangesAsync(cancellationToken);

        return new RuntimeRecoveryResult
        {
            FixedEntriesCount = fixedEntries,
            PausedTasksCount = pausedTaskIds.Count
        };
    }
}
