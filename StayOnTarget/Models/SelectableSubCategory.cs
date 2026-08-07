using StayOnTarget.ViewModels;

namespace StayOnTarget.Models;

public class SelectableSubCategory : ViewModelBase
{
    private bool _isSelected;
    
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;

    // Track original assignment
    public int? CurrentBucketId { get; set; }
    public string? CurrentBucketName { get; set; }
    
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
            {
                // Notify UI that the status message/warning state changed
                OnPropertyChanged(nameof(IsReassigned));
                OnPropertyChanged(nameof(AssignmentStatusText));
            }
        }
    }
    
    // Indicates if checking this box will move the subcategory from another envelope
    public bool IsAssignedToOtherBucket => 
        CurrentBucketId.HasValue && CurrentBucketId.Value != EditingBucketId;

    // ID of the envelope currently being edited in the form
    public int EditingBucketId { get; set; }

    // Shows a warning state only if it belongs elsewhere AND the user has checked it
    public bool IsReassigned => IsSelected && IsAssignedToOtherBucket;

    // Text status display logic
    public string AssignmentStatusText
    {
        get
        {
            if (IsAssignedToOtherBucket)
            {
                return IsSelected 
                    ? $"(Will move from '{CurrentBucketName}')" 
                    : $"(Currently in '{CurrentBucketName}')";
            }
            return string.Empty;
        }
    }
}