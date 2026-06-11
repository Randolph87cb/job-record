using JobRecord.Core.Dtos;
using JobRecord.Core.Models;

namespace JobRecord.Core.Abstractions;

public interface ITaskService
{
    Task<TaskItem> CreateTaskAsync(TaskCreateRequest request, CancellationToken cancellationToken = default);
    Task<TaskItem> RenameTaskAsync(Guid taskId, string title, CancellationToken cancellationToken = default);
    Task<TaskItem> UpdateTaskAsync(Guid taskId, TaskUpdateRequest request, CancellationToken cancellationToken = default);
    Task<SubTaskItem> CreateSubTaskAsync(Guid taskId, SubTaskCreateRequest request, CancellationToken cancellationToken = default);
    Task<SubTaskItem> RenameSubTaskAsync(Guid subTaskId, string title, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SubTaskItem>> GetSubTasksAsync(Guid taskId, bool includeArchived = false, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TaskItem>> GetTaskListAsync(bool includeArchived = false, CancellationToken cancellationToken = default);
    Task<TaskItem?> GetTaskByIdAsync(Guid taskId, CancellationToken cancellationToken = default);
    Task<TaskItem?> GetCurrentTaskAsync(CancellationToken cancellationToken = default);
    Task ArchiveTaskAsync(Guid taskId, CancellationToken cancellationToken = default);
}
