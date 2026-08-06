using StayOnTarget.ViewModels;

namespace StayOnTarget.Models;

public class SubCategory : ViewModelBase
{
    private bool _isArchived;
    private int _sortOrder;
    
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public string Name { get; set; } = string.Empty; // e.g., "Groceries"
    
    // The key link:
    public int? DefaultBucketId { get; set; } 
   // public BudgetBucket DefaultBucket { get; set; } = null!;
    
   public int SortOrder
   {
       get => _sortOrder;
       set => SetProperty(ref _sortOrder, value);
   }
   
    public bool IsArchived
    {
        get => _isArchived;
        set => SetProperty(ref _isArchived, value);
    }
    
}