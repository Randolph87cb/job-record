namespace JobRecord.Core.Models;

public sealed class RuntimeState
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;
    public Guid? CurrentTaskId { get; set; }
    public bool IsBarVisible { get; set; } = true;
    public bool IsExpanded { get; set; }
    public DateTimeOffset LastActiveAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
