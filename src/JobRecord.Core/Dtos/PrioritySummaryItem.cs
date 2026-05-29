using JobRecord.Core.Enums;

namespace JobRecord.Core.Dtos;

public sealed class PrioritySummaryItem
{
    public TaskPriority Priority { get; init; }
    public TimeSpan Duration { get; init; }
}
