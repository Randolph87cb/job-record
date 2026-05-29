using JobRecord.Core.Enums;

namespace JobRecord.Core.Dtos;

public sealed class TaskUpdateRequest
{
    public string Title { get; init; } = string.Empty;
    public TaskPriority Priority { get; init; } = TaskPriority.P2;
    public int? EstimateMinutes { get; init; }
    public string? Notes { get; init; }
    public int SortOrder { get; init; }
}
