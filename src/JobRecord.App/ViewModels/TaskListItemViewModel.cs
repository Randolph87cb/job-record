using System.Collections.ObjectModel;
using JobRecord.App.Styling;
using JobRecord.Core.Enums;
using Brush = System.Windows.Media.Brush;

namespace JobRecord.App.ViewModels;

public sealed class TaskListItemViewModel : ObservableObject
{
    private string _editableTitle = string.Empty;
    private string _newSubTaskTitle = string.Empty;
    private bool _isEditing;

    public required Guid Id { get; init; }
    public required string Title { get; init; }
    public required TaskPriority Priority { get; init; }
    public required TaskStatus Status { get; init; }
    public required string StatusText { get; init; }
    public required string DurationText { get; init; }
    public required string EstimateText { get; init; }
    public bool IsCurrent { get; init; }
    public Action<Guid, string>? NewSubTaskTitleChanged { get; init; }
    public ObservableCollection<SubTaskListItemViewModel> SubTasks { get; } = [];

    public string EditableTitle
    {
        get => _editableTitle;
        set
        {
            if (SetProperty(ref _editableTitle, value))
            {
                RaisePropertyChanged(nameof(CanSaveEdit));
            }
        }
    }

    public bool IsEditing
    {
        get => _isEditing;
        set
        {
            if (SetProperty(ref _isEditing, value))
            {
                RaisePropertyChanged(nameof(CanSaveEdit));
            }
        }
    }

    public string NewSubTaskTitle
    {
        get => _newSubTaskTitle;
        set
        {
            if (SetProperty(ref _newSubTaskTitle, value))
            {
                NewSubTaskTitleChanged?.Invoke(Id, value);
                RaisePropertyChanged(nameof(CanCreateSubTask));
            }
        }
    }

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
    public bool CanCreateSubTask => Status != TaskStatus.Completed && !string.IsNullOrWhiteSpace(NewSubTaskTitle);
    public bool CanSaveEdit => IsEditing &&
        !string.IsNullOrWhiteSpace(EditableTitle) &&
        !string.Equals(EditableTitle.Trim(), Title, StringComparison.Ordinal);

    public void BeginEdit()
    {
        EditableTitle = Title;
        IsEditing = true;
    }

    public void CancelEdit()
    {
        EditableTitle = Title;
        IsEditing = false;
    }
}
