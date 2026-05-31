using JobRecord.Core.Enums;
using JobRecord.App.Styling;
using Brush = System.Windows.Media.Brush;

namespace JobRecord.App.ViewModels;

public sealed class TaskListItemViewModel : ObservableObject
{
    public required Guid Id { get; init; }
    public required string Title { get; init; }
    public required TaskPriority Priority { get; init; }
    public required TaskStatus Status { get; init; }
    public required string StatusText { get; init; }
    public required string DurationText { get; init; }
    public required string EstimateText { get; init; }
    public bool IsCurrent { get; init; }
    public string PriorityText => Priority.ToString();
    public Brush PriorityBackground => UiPalette.GetPriorityBackground(Priority);
    public Brush PriorityForeground => UiPalette.GetPriorityForeground(Priority);
    public Brush PrioritySoftBackground => UiPalette.GetPrioritySoftBackground(Priority);
    public Brush StatusBackground => UiPalette.GetStatusBackground(Status);
    public Brush StatusForeground => UiPalette.GetStatusForeground(Status);
    public string PrimaryActionText => Status switch
    {
        TaskStatus.Running => "暂停",
        TaskStatus.Paused => "继续",
        TaskStatus.Pending => "开始",
        _ => "已完成"
    };

    public bool CanPrimaryAction => Status != TaskStatus.Completed;
    public bool CanComplete => Status != TaskStatus.Completed;
}
