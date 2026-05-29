using JobRecord.Core.Enums;

namespace JobRecord.Core.Models;

public sealed class TaskItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public TaskPriority Priority { get; set; } = TaskPriority.P2;
    public TaskStatus Status { get; set; } = TaskStatus.Pending;
    public int? EstimateMinutes { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public int SortOrder { get; set; }
    public bool IsArchived { get; set; }
    public List<TimeEntry> TimeEntries { get; set; } = [];
}
