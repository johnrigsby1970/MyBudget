using StayOnTarget.ViewModels;

namespace StayOnTarget.Models;

public class AccountReconciliation : ViewModelBase
{
    private int _accountId;
    private DateTime _reconciledAsOfDate = DateTime.Today;
    private decimal _reconciledBalance;
    private DateTime _reconciledOnDate = DateTime.Today;
    private bool _isInvalidated;

    private int _id;
    public int Id 
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public int AccountId
    {
        get => _accountId;
        set => SetProperty(ref _accountId, value);
    }

    public DateTime ReconciledAsOfDate
    {
        get => _reconciledAsOfDate;
        set => SetProperty(ref _reconciledAsOfDate, value);
    }

    public decimal ReconciledBalance
    {
        get => _reconciledBalance;
        set => SetProperty(ref _reconciledBalance, value);
    }

    public DateTime ReconciledOnDate
    {
        get => _reconciledOnDate;
        set => SetProperty(ref _reconciledOnDate, value);
    }

    public bool IsInvalidated
    {
        get => _isInvalidated;
        set => SetProperty(ref _isInvalidated, value);
    }

    // Helper for UI
    private string? _accountName;
    public string? AccountName 
    {
        get => _accountName;
        set => SetProperty(ref _accountName, value);
    }
}
