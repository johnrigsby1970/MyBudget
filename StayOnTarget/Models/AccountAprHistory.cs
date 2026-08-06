using StayOnTarget.ViewModels;

namespace StayOnTarget.Models;
    
public class AccountAprHistory : ViewModelBase
{
    private decimal _annualPercentageRate;
    private decimal _cashAdvanceRate;
    private decimal _balanceTransferRate;
    private DateTime _asOfDate;

    private int _id;
    public int Id 
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }
    
    private int _accountId;
    public int AccountId 
    {
        get => _accountId;
        set => SetProperty(ref _accountId, value);
    }
    
    public decimal AnnualPercentageRate
    {
        get => _annualPercentageRate;
        set => SetProperty(ref _annualPercentageRate, value);
    }
    
    public decimal CashAdvanceRate
    {
        get => _cashAdvanceRate;
        set => SetProperty(ref _cashAdvanceRate, value);
    }
        
    public decimal BalanceTransferRate
    {
        get => _balanceTransferRate;
        set => SetProperty(ref _balanceTransferRate, value);
    }

    public DateTime AsOfDate
    {
        get => _asOfDate;
        set => SetProperty(ref _asOfDate, value);
    }
}
