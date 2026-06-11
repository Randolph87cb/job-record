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
        var hasIncompleteSubTasks = dbContext.SubTasks.Any(item =>
            item.TaskItemId == taskId &&
            !item.IsArchived &&
            item.Status != TaskStatus.Completed);
        if (hasIncompleteSubTasks)
        {
            throw new InvalidOperationException("存在未完成的子任务，不能完成任务。");
        }

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

    public async Task<SubTaskItem> StartSubTaskAsync(Guid taskId, Guid subTaskId, CancellationToken cancellationToken = default)
    {
        var task = GetTaskForActivation(taskId);
        var subTask = GetSubTaskForActivation(task.Id, subTaskId);
        var now = clock.Now;
        var activeEntry = dbContext.TimeEntries.AsEnumerable().SingleOrDefault(entry => entry.EndAt == null);

        if (activeEntry?.SubTaskItemId == subTask.Id)
        {
            task.Status = TaskStatus.Running;
            subTask.Status = TaskStatus.Running;
            task.UpdatedAt = now;
            subTask.UpdatedAt = now;
            await dbContext.SaveChangesAsync(cancellationToken);
            return subTask;
        }

        if (activeEntry is not null)
        {
            var activeTask = dbContext.Tasks.AsEnumerable().SingleOrDefault(item => item.Id == activeEntry.TaskItemId && !item.IsArchived);
            if (activeTask is not null)
            {
                PauseTaskInternal(activeTask, now);
            }
        }

        ActivateTask(task, now, TimeEntryType.Manual, subTask);
        await dbContext.SaveChangesAsync(cancellationToken);
        return subTask;
    }

    public async Task<SubTaskItem> PauseSubTaskAsync(Guid subTaskId, CancellationToken cancellationToken = default)
    {
        var subTask = dbContext.SubTasks.SingleOrDefault(item => item.Id == subTaskId && !item.IsArchived)
            ?? throw new InvalidOperationException("子任务不存在。");

        if (subTask.Status != TaskStatus.Running)
        {
            throw new InvalidOperationException("只有进行中的子任务才能暂停。");
        }

        var task = dbContext.Tasks.SingleOrDefault(item => item.Id == subTask.TaskItemId && !item.IsArchived)
            ?? throw new InvalidOperationException("任务不存在。");
        var now = clock.Now;

        CloseActiveEntry(task.Id, now);
        subTask.Status = TaskStatus.Paused;
        subTask.UpdatedAt = now;
        task.Status = TaskStatus.Paused;
        task.UpdatedAt = now;

        var runtimeState = GetOrCreateRuntimeState(now);
        if (runtimeState.CurrentTaskId == task.Id)
        {
            runtimeState.CurrentTaskId = null;
        }

        runtimeState.LastActiveAt = now;
        runtimeState.UpdatedAt = now;

        await dbContext.SaveChangesAsync(cancellationToken);
        return subTask;
    }

    public Task<SubTaskItem> ResumeSubTaskAsync(Guid subTaskId, CancellationToken cancellationToken = default)
    {
        var subTask = dbContext.SubTasks.SingleOrDefault(item => item.Id == subTaskId && !item.IsArchived)
            ?? throw new InvalidOperationException("子任务不存在。");

        if (subTask.Status != TaskStatus.Paused)
        {
            throw new InvalidOperationException("只有暂停中的子任务才能继续。");
        }

        return StartSubTaskAsync(subTask.TaskItemId, subTask.Id, cancellationToken);
    }

    public async Task<SubTaskItem> CompleteSubTaskAsync(Guid subTaskId, CancellationToken cancellationToken = default)
    {
        var subTask = dbContext.SubTasks.SingleOrDefault(item => item.Id == subTaskId && !item.IsArchived)
            ?? throw new InvalidOperationException("子任务不存在。");
        var task = dbContext.Tasks.SingleOrDefault(item => item.Id == subTask.TaskItemId && !item.IsArchived)
            ?? throw new InvalidOperationException("任务不存在。");
        var now = clock.Now;

        if (subTask.Status == TaskStatus.Running)
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

        subTask.Status = TaskStatus.Completed;
        subTask.CompletedAt = now;
        subTask.UpdatedAt = now;

        await dbContext.SaveChangesAsync(cancellationToken);
        return subTask;
    }

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

    private SubTaskItem GetSubTaskForActivation(Guid taskId, Guid subTaskId)
    {
        var subTask = dbContext.SubTasks.SingleOrDefault(item => item.Id == subTaskId && item.TaskItemId == taskId && !item.IsArchived)
            ?? throw new InvalidOperationException("子任务不存在。");

        if (subTask.Status == TaskStatus.Completed)
        {
            throw new InvalidOperationException("已完成的子任务不能再次开始。");
        }

        return subTask;
    }

    private void ActivateTask(TaskItem task, DateTimeOffset now, TimeEntryType entryType, SubTaskItem? subTask = null)
    {
        var existingActiveEntry = dbContext.TimeEntries.AsEnumerable().SingleOrDefault(entry => entry.EndAt == null);
        if (existingActiveEntry is not null &&
            (existingActiveEntry.TaskItemId != task.Id || existingActiveEntry.SubTaskItemId != subTask?.Id))
        {
            throw new InvalidOperationException("存在未关闭的其他任务计时段。");
        }

        if (existingActiveEntry is null)
        {
            dbContext.Add(new TimeEntry
            {
                TaskItemId = task.Id,
                SubTaskItemId = subTask?.Id,
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

        if (subTask is not null)
        {
            subTask.Status = TaskStatus.Running;
            subTask.StartedAt ??= now;
            subTask.CompletedAt = null;
            subTask.UpdatedAt = now;
        }

        var runtimeState = GetOrCreateRuntimeState(now);
        runtimeState.CurrentTaskId = task.Id;
        runtimeState.LastActiveAt = now;
        runtimeState.UpdatedAt = now;
    }

    private void PauseTaskInternal(TaskItem task, DateTimeOffset now)
    {
        var closedEntry = CloseActiveEntry(task.Id, now);
        PauseSubTaskFromEntry(closedEntry, now);
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

    private TimeEntry? CloseActiveEntry(Guid taskId, DateTimeOffset endAt)
    {
        var entry = dbContext.TimeEntries.AsEnumerable().SingleOrDefault(item => item.TaskItemId == taskId && item.EndAt == null);
        if (entry is null)
        {
            return null;
        }

        entry.EndAt = endAt;
        entry.DurationSeconds = Math.Max(0, (int)(endAt - entry.StartAt).TotalSeconds);
        return entry;
    }

    private void PauseSubTaskFromEntry(TimeEntry? entry, DateTimeOffset now)
    {
        if (entry?.SubTaskItemId is null)
        {
            return;
        }

        var subTask = dbContext.SubTasks.SingleOrDefault(item => item.Id == entry.SubTaskItemId && !item.IsArchived);
        if (subTask is null || subTask.Status != TaskStatus.Running)
        {
            return;
        }

        subTask.Status = TaskStatus.Paused;
        subTask.UpdatedAt = now;
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
