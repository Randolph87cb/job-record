using JobRecord.Core.Dtos;

namespace JobRecord.Core.Abstractions;

public interface IStatisticsService
{
    Task<TodaySummary> GetTodaySummaryAsync(DateOnly? day = null, CancellationToken cancellationToken = default);
    Task<TimeSpan> GetTaskDurationAsync(Guid taskId, CancellationToken cancellationToken = default);
    Task<TimeSpan> GetSubTaskDurationAsync(Guid subTaskId, CancellationToken cancellationToken = default);
    Task<int> GetTodayCompletedTasksAsync(DateOnly? day = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PrioritySummaryItem>> GetPrioritySummaryAsync(DateOnly? day = null, CancellationToken cancellationToken = default);
    Task<TimeSpan> GetCurrentTaskDisplayDurationAsync(Guid taskId, CancellationToken cancellationToken = default);
}
