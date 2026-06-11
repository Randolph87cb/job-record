using JobRecord.Core.Enums;

namespace JobRecord.Core.Models;

public sealed class SubTaskItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TaskItemId { get; set; }
    public TaskItem? TaskItem { get; set; }
    public string Title { get; set; } = string.Empty;
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
