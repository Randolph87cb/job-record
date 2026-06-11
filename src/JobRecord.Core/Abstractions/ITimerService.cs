using JobRecord.Core.Models;

namespace JobRecord.Core.Abstractions;

public interface ITimerService
{
    Task<TaskItem> StartTaskAsync(Guid taskId, CancellationToken cancellationToken = default);
    Task<TaskItem> PauseTaskAsync(Guid taskId, CancellationToken cancellationToken = default);
    Task<TaskItem> ResumeTaskAsync(Guid taskId, CancellationToken cancellationToken = default);
    Task<TaskItem> CompleteTaskAsync(Guid taskId, CancellationToken cancellationToken = default);
    Task<TaskItem> SwitchTaskAsync(Guid taskId, CancellationToken cancellationToken = default);
    Task<SubTaskItem> StartSubTaskAsync(Guid taskId, Guid subTaskId, CancellationToken cancellationToken = default);
    Task<SubTaskItem> PauseSubTaskAsync(Guid subTaskId, CancellationToken cancellationToken = default);
    Task<SubTaskItem> ResumeSubTaskAsync(Guid subTaskId, CancellationToken cancellationToken = default);
    Task<SubTaskItem> CompleteSubTaskAsync(Guid subTaskId, CancellationToken cancellationToken = default);
}
