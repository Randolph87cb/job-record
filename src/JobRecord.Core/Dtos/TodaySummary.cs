namespace JobRecord.Core.Dtos;

public sealed class TodaySummary
{
    public TimeSpan TotalDuration { get; init; }
    public int CompletedTaskCount { get; init; }
    public IReadOnlyList<PrioritySummaryItem> PriorityBreakdown { get; init; } = [];
}
