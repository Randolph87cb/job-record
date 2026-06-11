using JobRecord.Core.Abstractions;
using JobRecord.Core.Dtos;
using JobRecord.Core.Enums;
using JobRecord.Core.Models;

namespace JobRecord.Core.Services;

public sealed class TaskService(IJobRecordDbContext dbContext, IClock clock) : ITaskService
{
    public async Task<TaskItem> CreateTaskAsync(TaskCreateRequest request, CancellationToken cancellationToken = default)
    {
        var title = NormalizeTitle(request.Title);
        var now = clock.Now;
        var nextSortOrder = dbContext.Tasks
            .Where(task => !task.IsArchived)
            .AsEnumerable()
            .Select(task => (int?)task.SortOrder)
            .DefaultIfEmpty(0)
            .Max() ?? 0;

        var task = new TaskItem
        {
            Title = title,
            Priority = request.Priority,
            EstimateMinutes = request.EstimateMinutes,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            CreatedAt = now,
            UpdatedAt = now,
            SortOrder = nextSortOrder + 1,
            Status = TaskStatus.Pending
        };

        dbContext.Add(task);
        await dbContext.SaveChangesAsync(cancellationToken);
        return task;
    }

    public async Task<TaskItem> RenameTaskAsync(Guid taskId, string title, CancellationToken cancellationToken = default)
    {
        var task = dbContext.Tasks.SingleOrDefault(task => task.Id == taskId && !task.IsArchived)
            ?? throw new InvalidOperationException("任务不存在。");

        task.Title = NormalizeTitle(title);
        task.UpdatedAt = clock.Now;

        await dbContext.SaveChangesAsync(cancellationToken);
        return task;
    }

    public async Task<TaskItem> UpdateTaskAsync(Guid taskId, TaskUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var task = dbContext.Tasks.SingleOrDefault(task => task.Id == taskId && !task.IsArchived)
            ?? throw new InvalidOperationException("任务不存在。");

        task.Title = NormalizeTitle(request.Title);
        task.Priority = request.Priority;
        task.EstimateMinutes = request.EstimateMinutes;
        task.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        task.SortOrder = request.SortOrder;
        task.UpdatedAt = clock.Now;

        await dbContext.SaveChangesAsync(cancellationToken);
        return task;
    }

    public async Task<SubTaskItem> CreateSubTaskAsync(Guid taskId, SubTaskCreateRequest request, CancellationToken cancellationToken = default)
    {
        var task = dbContext.Tasks.SingleOrDefault(task => task.Id == taskId && !task.IsArchived)
            ?? throw new InvalidOperationException("任务不存在。");
        var title = NormalizeTitle(request.Title);
        var now = clock.Now;
        var nextSortOrder = dbContext.SubTasks
            .Where(subTask => subTask.TaskItemId == task.Id && !subTask.IsArchived)
            .AsEnumerable()
            .Select(subTask => (int?)subTask.SortOrder)
            .DefaultIfEmpty(0)
            .Max() ?? 0;

        var subTask = new SubTaskItem
        {
            TaskItemId = task.Id,
            Title = title,
            EstimateMinutes = request.EstimateMinutes,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            CreatedAt = now,
            UpdatedAt = now,
            SortOrder = nextSortOrder + 1,
            Status = TaskStatus.Pending
        };

        task.UpdatedAt = now;
        dbContext.Add(subTask);
        await dbContext.SaveChangesAsync(cancellationToken);
        return subTask;
    }

    public async Task<SubTaskItem> RenameSubTaskAsync(Guid subTaskId, string title, CancellationToken cancellationToken = default)
    {
        var subTask = dbContext.SubTasks.SingleOrDefault(subTask => subTask.Id == subTaskId && !subTask.IsArchived)
            ?? throw new InvalidOperationException("子任务不存在。");

        subTask.Title = NormalizeTitle(title);
        subTask.UpdatedAt = clock.Now;

        await dbContext.SaveChangesAsync(cancellationToken);
        return subTask;
    }

    public Task<IReadOnlyList<SubTaskItem>> GetSubTasksAsync(Guid taskId, bool includeArchived = false, CancellationToken cancellationToken = default)
    {
        var query = dbContext.SubTasks.Where(subTask => subTask.TaskItemId == taskId).AsEnumerable();

        if (!includeArchived)
        {
            query = query.Where(subTask => !subTask.IsArchived);
        }

        var items = query
            .OrderBy(subTask => subTask.Status == TaskStatus.Running ? 0 : subTask.Status == TaskStatus.Paused ? 1 : subTask.Status == TaskStatus.Pending ? 2 : 3)
            .ThenByDescending(subTask => subTask.Status == TaskStatus.Paused ? subTask.UpdatedAt : DateTimeOffset.MinValue)
            .ThenBy(subTask => subTask.SortOrder)
            .ThenBy(subTask => subTask.CreatedAt)
            .ToList();

        return Task.FromResult<IReadOnlyList<SubTaskItem>>(items);
    }

    public Task<IReadOnlyList<TaskItem>> GetTaskListAsync(bool includeArchived = false, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Tasks.AsEnumerable();

        if (!includeArchived)
        {
            query = query.Where(task => !task.IsArchived);
        }

        var items = query
            .OrderBy(task => task.Status == TaskStatus.Running ? 0 : task.Status == TaskStatus.Paused ? 1 : task.Status == TaskStatus.Pending ? 2 : 3)
            .ThenBy(task => task.Priority)
            .ThenByDescending(task => task.Status == TaskStatus.Paused ? task.UpdatedAt : DateTimeOffset.MinValue)
            .ThenBy(task => task.SortOrder)
            .ThenBy(task => task.CreatedAt)
            .ToList();

        return Task.FromResult<IReadOnlyList<TaskItem>>(items);
    }

    public Task<TaskItem?> GetTaskByIdAsync(Guid taskId, CancellationToken cancellationToken = default)
        => Task.FromResult(dbContext.Tasks.SingleOrDefault(task => task.Id == taskId && !task.IsArchived));

    public Task<TaskItem?> GetCurrentTaskAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(dbContext.Tasks.SingleOrDefault(task => task.Status == TaskStatus.Running && !task.IsArchived));

    public async Task ArchiveTaskAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        var task = dbContext.Tasks.SingleOrDefault(item => item.Id == taskId && !item.IsArchived)
            ?? throw new InvalidOperationException("任务不存在。");

        if (task.Status == TaskStatus.Running)
        {
            throw new InvalidOperationException("进行中的任务不能删除或归档。");
        }

        var hasEntries = dbContext.TimeEntries.Any(entry => entry.TaskItemId == taskId);
        if (hasEntries)
        {
            task.IsArchived = true;
            task.UpdatedAt = clock.Now;
        }
        else
        {
            dbContext.Remove(task);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string NormalizeTitle(string title)
    {
        var normalized = title.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException("任务标题不能为空。");
        }

        if (normalized.Length > 80)
        {
            throw new InvalidOperationException("任务标题长度不能超过 80 个字符。");
        }

        return normalized;
    }
}
