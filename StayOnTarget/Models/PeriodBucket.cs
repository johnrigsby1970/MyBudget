using StayOnTarget.ViewModels;

namespace StayOnTarget.Models;

public class PeriodBucket : ViewModelBase
{
    private int _bucketId;
    private DateTime _periodDate;
    private decimal _actualAmount;
    private decimal _transactionAmount;
    private bool _isPaid;

    private int _id;
    public int Id 
    {
        get => _id;
        set => SetProperty(ref _id, value);
        
    }
    private Guid _fitId = Guid.NewGuid();
    public Guid FitId 
    {
        get => _fitId;
        set => SetProperty(ref _fitId, value);
    }

    public int BucketId
    {
        get => _bucketId;
        set => SetProperty(ref _bucketId, value);
    }

    public DateTime PeriodDate
    {
        get => _periodDate;
        set => SetProperty(ref _periodDate, value);
    }

    public decimal ActualAmount {
        get => _actualAmount;
        set {
            // SetProperty returns true ONLY if the value actually changed
            if (SetProperty(ref _actualAmount, value)) 
            {
                OnPropertyChanged(nameof(HasActualAmount));
                OnPropertyChanged(nameof(BudgetExceeded));
                
                
            }
        }
    }

    public decimal TransactionAmount
    {
        get => _transactionAmount;
        set
        {
            if (SetProperty(ref _transactionAmount, value))
            {
                OnPropertyChanged(nameof(HasActualAmount));
                OnPropertyChanged(nameof(BudgetExceeded));
                
                
            }
        }
    }

    public bool HasActualAmount => _transactionAmount != 0;
    public bool BudgetExceeded => Math.Abs(_transactionAmount) > Math.Abs(_actualAmount);
    
    public bool IsPaid
    {
        get => _isPaid;
        set => SetProperty(ref _isPaid, value);
    }

    // Helper for UI
    private string? _bucketName;
    public string? BucketName 
    {
        get => _bucketName;
        set => SetProperty(ref _bucketName, value);
    }
}