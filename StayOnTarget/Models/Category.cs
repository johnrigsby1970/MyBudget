using StayOnTarget.ViewModels;

namespace StayOnTarget.Models;

public class Category: ViewModelBase
{
    private bool _isArchived;
    private int _sortOrder;
    
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public string Name { get; set; } = string.Empty; // e.g., "Groceries"
    
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
    
    
    public List<SubCategory> Subcategories { get; set; } = new();
}