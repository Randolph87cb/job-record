using JobRecord.Core.Enums;

namespace JobRecord.Core.Models;

public sealed class TimeEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TaskItemId { get; set; }
    public TaskItem? TaskItem { get; set; }
    public Guid? SubTaskItemId { get; set; }
    public SubTaskItem? SubTaskItem { get; set; }
    public DateTimeOffset StartAt { get; set; }
    public DateTimeOffset? EndAt { get; set; }
    public int DurationSeconds { get; set; }
    public TimeEntryType EntryType { get; set; } = TimeEntryType.Manual;
    public DateTimeOffset CreatedAt { get; set; }
}
