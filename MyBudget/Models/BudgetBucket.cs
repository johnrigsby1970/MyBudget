using MyBudget.ViewModels;

namespace MyBudget.Models;

public class BudgetBucket : ViewModelBase
{
    private string _name = string.Empty;
    private decimal _expectedAmount;
    private int? _accountId;

    public int Id { get; set; }

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
}