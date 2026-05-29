using JobRecord.Core.Abstractions;
using JobRecord.Core.Enums;
using JobRecord.Core.Models;

namespace JobRecord.Core.Services;

public sealed class TimerService(IJobRecordDbContext dbContext, IClock clock) : ITimerService
{
    public async Task<TaskItem> StartTaskAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        var targetTask = GetTaskForActivation(taskId);
        var now = clock.Now;
        var activeTask = dbContext.Tasks.AsEnumerable().SingleOrDefault(task => task.Status == TaskStatus.Running && !task.IsArchived);

        if (activeTask?.Id == taskId)
        {
            return targetTask;
        }

        if (activeTask is not null)
        {
            PauseTaskInternal(activeTask, now);
        }

        ActivateTask(targetTask, now, TimeEntryType.Manual);
        await dbContext.SaveChangesAsync(cancellationToken);
        return targetTask;
    }

    public async Task<TaskItem> PauseTaskAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        var task = dbContext.Tasks.SingleOrDefault(item => item.Id == taskId && !item.IsArchived)
            ?? throw new InvalidOperationException("任务不存在。");

        if (task.Status != TaskStatus.Running)
        {
            throw new InvalidOperationException("只有进行中的任务才能暂停。");
        }

        PauseTaskInternal(task, clock.Now);
        await dbContext.SaveChangesAsync(cancellationToken);
        return task;
    }

    public async Task<TaskItem> ResumeTaskAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        var task = dbContext.Tasks.SingleOrDefault(item => item.Id == taskId && !item.IsArchived)
            ?? throw new InvalidOperationException("任务不存在。");

        if (task.Status != TaskStatus.Paused)
        {
            throw new InvalidOperationException("只有暂停中的任务才能继续。");
        }

        var activeTask = dbContext.Tasks.AsEnumerable().SingleOrDefault(item => item.Status == TaskStatus.Running && !item.IsArchived);
        if (activeTask is not null)
        {
            PauseTaskInternal(activeTask, clock.Now);
        }

        ActivateTask(task, clock.Now, TimeEntryType.Manual);
        await dbContext.SaveChangesAsync(cancellationToken);
        return task;
    }

    public async Task<TaskItem> CompleteTaskAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        var task = dbContext.Tasks.SingleOrDefault(item => item.Id == taskId && !item.IsArchived)
            ?? throw new InvalidOperationException("任务不存在。");
        var now = clock.Now;

        if (task.Status == TaskStatus.Running)
        {
            CloseActiveEntry(task.Id, now);
        }

        task.Status = TaskStatus.Completed;
        task.CompletedAt = now;
        task.UpdatedAt = now;

        var runtimeState = GetOrCreateRuntimeState(now);
        runtimeState.CurrentTaskId = null;
        runtimeState.UpdatedAt = now;

        await dbContext.SaveChangesAsync(cancellationToken);
        return task;
    }

    public Task<TaskItem> SwitchTaskAsync(Guid taskId, CancellationToken cancellationToken = default)
        => StartTaskAsync(taskId, cancellationToken);

    private TaskItem GetTaskForActivation(Guid taskId)
    {
        var task = dbContext.Tasks.SingleOrDefault(item => item.Id == taskId && !item.IsArchived)
            ?? throw new InvalidOperationException("任务不存在。");

        if (task.Status == TaskStatus.Completed)
        {
            throw new InvalidOperationException("已完成的任务不能再次开始。");
        }

        return task;
    }

    private void ActivateTask(TaskItem task, DateTimeOffset now, TimeEntryType entryType)
    {
        var existingActiveEntry = dbContext.TimeEntries.AsEnumerable().SingleOrDefault(entry => entry.EndAt == null);
        if (existingActiveEntry is not null && existingActiveEntry.TaskItemId != task.Id)
        {
            throw new InvalidOperationException("存在未关闭的其他任务计时段。");
        }

        if (existingActiveEntry is null)
        {
            dbContext.Add(new TimeEntry
            {
                TaskItemId = task.Id,
                StartAt = now,
                EndAt = null,
                DurationSeconds = 0,
                EntryType = entryType,
                CreatedAt = now
            });
        }

        task.Status = TaskStatus.Running;
        task.StartedAt ??= now;
        task.CompletedAt = null;
        task.UpdatedAt = now;

        var runtimeState = GetOrCreateRuntimeState(now);
        runtimeState.CurrentTaskId = task.Id;
        runtimeState.LastActiveAt = now;
        runtimeState.UpdatedAt = now;
    }

    private void PauseTaskInternal(TaskItem task, DateTimeOffset now)
    {
        CloseActiveEntry(task.Id, now);
        task.Status = TaskStatus.Paused;
        task.UpdatedAt = now;

        var runtimeState = GetOrCreateRuntimeState(now);
        if (runtimeState.CurrentTaskId == task.Id)
        {
            runtimeState.CurrentTaskId = null;
        }

        runtimeState.LastActiveAt = now;
        runtimeState.UpdatedAt = now;
    }

    private void CloseActiveEntry(Guid taskId, DateTimeOffset endAt)
    {
        var entry = dbContext.TimeEntries.AsEnumerable().SingleOrDefault(item => item.TaskItemId == taskId && item.EndAt == null);
        if (entry is null)
        {
            return;
        }

        entry.EndAt = endAt;
        entry.DurationSeconds = Math.Max(0, (int)(endAt - entry.StartAt).TotalSeconds);
    }

    private RuntimeState GetOrCreateRuntimeState(DateTimeOffset now)
    {
        var state = dbContext.RuntimeStates.SingleOrDefault();
        if (state is not null)
        {
            return state;
        }

        state = new RuntimeState
        {
            Id = RuntimeState.SingletonId,
            LastActiveAt = now,
            UpdatedAt = now
        };

        dbContext.Add(state);
        return state;
    }
}
