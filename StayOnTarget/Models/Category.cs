using StayOnTarget.ViewModels;

namespace StayOnTarget.Models;

public class Category: ViewModelBase
{
    private bool _isArchived;
    private int _sortOrder;
    private string _hexColor = "#FF0000FF"; // Default to Blue
    
    private int _id;
    public int Id 
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }
    
    private int _categoryId;
    public int CategoryId 
    {
        get => _id;
        set => SetProperty(ref _categoryId, value);
    }
    
    private string _name
        = string.Empty; // e.g., "Groceries"
    public string Name  
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }
    
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
    
    public string HexColor
    {
        get => _hexColor;
        set => SetProperty(ref _hexColor, value);
    }
    
    public List<SubCategory> Subcategories { get; set; } = new();
}