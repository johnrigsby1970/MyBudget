using StayOnTarget.ViewModels;

namespace StayOnTarget.Models;

public class BudgetBucket : ViewModelBase
{
    private string _name = string.Empty;
    private decimal _expectedAmount;
    private int? _accountId;
    private int? _paycheckId;
    private bool _isArchived;
    
    private int _id;
    public int Id 
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public decimal ExpectedAmount
    {
        get => _expectedAmount;
        set => SetProperty(ref _expectedAmount, value);
    }

    public int? AccountId
    {
        get => _accountId;
        set => SetProperty(ref _accountId, value);
    }
    
    public int? PaycheckId
    {
        get => _paycheckId;
        set => SetProperty(ref _paycheckId, value);
    }
    
    public bool IsArchived
    {
        get => _isArchived;
        set => SetProperty(ref _isArchived, value);
    }
}