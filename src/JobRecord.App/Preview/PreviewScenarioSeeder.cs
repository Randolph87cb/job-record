using JobRecord.Core.Abstractions;
using JobRecord.Core.Enums;
using JobRecord.Core.Models;

namespace JobRecord.App.Preview;

public sealed class PreviewScenarioSeeder(IJobRecordDbContext dbContext, IClock clock)
{
    public async Task SeedAsync(PreviewLaunchOptions options, CancellationToken cancellationToken = default)
    {
        if (!options.IsEnabled)
        {
            return;
        }

        var now = clock.Now;
        var settings = dbContext.Settings.Single();
        var runtimeState = dbContext.RuntimeStates.Single();

        settings.DockMode = options.DockMode;
        settings.IsBarVisible = true;
        settings.AutoCollapseEnabled = false;
        settings.MarginTop = 12;
        settings.MarginSide = 12;
        settings.BarWidth = 548;
        settings.BarHeight = 36;
        settings.WindowLeft = null;
        settings.WindowTop = null;
        settings.UpdatedAt = now;

        runtimeState.IsBarVisible = true;
        runtimeState.IsExpanded = options.IsExpanded;
        runtimeState.LastActiveAt = now;
        runtimeState.UpdatedAt = now;

        var runningTask = CreateTask("月报整理", TaskPriority.P1, TaskStatus.Running, 90, now.AddMinutes(-52), now, 0);
        AddClosedEntry(runningTask, now.AddMinutes(-52), now.AddMinutes(-37), now);
        AddOpenEntry(runningTask, now.AddMinutes(-27), now);

        var pausedTask = CreateTask("回访整理", TaskPriority.P2, TaskStatus.Paused, 45, now.AddMinutes(-90), now, 1);
        AddClosedEntry(pausedTask, now.AddMinutes(-90), now.AddMinutes(-58), now);
        AddClosedEntry(pausedTask, now.AddMinutes(-42), now.AddMinutes(-28), now);

        var pendingTask = CreateTask("测试清单", TaskPriority.P1, TaskStatus.Pending, 35, null, now, 2);
        var notesTask = CreateTask("需求备注", TaskPriority.P3, TaskStatus.Pending, 20, null, now, 3);
        var completedTask = CreateTask("日报归档", TaskPriority.P3, TaskStatus.Completed, 15, now.AddMinutes(-130), now, 4);
        completedTask.CompletedAt = now.AddMinutes(-18);
        AddClosedEntry(completedTask, now.AddMinutes(-130), now.AddMinutes(-114), now);

        switch (options.WorkState)
        {
            case PreviewWorkState.Paused:
                runningTask.Status = TaskStatus.Pending;
                runningTask.StartedAt = null;
                runningTask.TimeEntries.RemoveAll(static entry => entry.EndAt is null);
                pausedTask.Status = TaskStatus.Paused;
                runtimeState.CurrentTaskId = pausedTask.Id;
                break;
            case PreviewWorkState.Idle:
                runningTask.Status = TaskStatus.Pending;
                runningTask.StartedAt = null;
                runningTask.TimeEntries.RemoveAll(static entry => entry.EndAt is null);
                pausedTask.Status = TaskStatus.Pending;
                runtimeState.CurrentTaskId = null;
                break;
            default:
                runtimeState.CurrentTaskId = runningTask.Id;
                break;
        }

        foreach (var task in new[] { runningTask, pausedTask, pendingTask, notesTask, completedTask })
        {
            dbContext.Add(task);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static TaskItem CreateTask(
        string title,
        TaskPriority priority,
        TaskStatus status,
        int? estimateMinutes,
        DateTimeOffset? startedAt,
        DateTimeOffset now,
        int sortOrder)
        => new()
        {
            Title = title,
            Priority = priority,
            Status = status,
            EstimateMinutes = estimateMinutes,
            CreatedAt = now.AddHours(-4).AddMinutes(sortOrder * 3),
            UpdatedAt = now,
            StartedAt = startedAt,
            SortOrder = sortOrder,
            Notes = $"{title} 预览数据"
        };

    private static void AddClosedEntry(TaskItem task, DateTimeOffset start, DateTimeOffset end, DateTimeOffset createdAt)
    {
        task.TimeEntries.Add(new TimeEntry
        {
            TaskItemId = task.Id,
            StartAt = start,
            EndAt = end,
            DurationSeconds = (int)(end - start).TotalSeconds,
            CreatedAt = createdAt
        });
    }

    private static void AddOpenEntry(TaskItem task, DateTimeOffset start, DateTimeOffset createdAt)
    {
        task.TimeEntries.Add(new TimeEntry
        {
            TaskItemId = task.Id,
            StartAt = start,
            EndAt = null,
            DurationSeconds = 0,
            CreatedAt = createdAt
        });
    }
}
