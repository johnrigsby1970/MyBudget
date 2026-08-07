using StayOnTarget.ViewModels;

namespace StayOnTarget.Models;

public class SubCategory : ViewModelBase
{
    private bool _isArchived;
    private int _sortOrder;
    
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

    // The key link:


    private int? _defaultBucketId;
    public int?  DefaultBucketId  
    {
        get => _defaultBucketId;
        set => SetProperty(ref _defaultBucketId, value);
    }
    
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
    
    private string _categoryName;
    public string CategoryName
    {
        get => _categoryName;
        set => SetProperty(ref _categoryName, value);
    }

}