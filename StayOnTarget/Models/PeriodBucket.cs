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
        set
        {
            if (SetProperty(ref _id, value))
            {
                OnPropertyChanged(nameof(IsAccumulatingDrawdown));
                OnPropertyChanged(nameof(FundingStatus));
                OnPropertyChanged(nameof(IsSkipped));
                OnPropertyChanged(nameof(IsFunded));
            }
        }
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
                OnPropertyChanged(nameof(IsAccumulatingDrawdown));
                OnPropertyChanged(nameof(FundingStatus));
                OnPropertyChanged(nameof(IsSkipped));
                OnPropertyChanged(nameof(IsFunded));
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
    
    private BucketType _bucketType;
    public BucketType BucketType
    {
        get => _bucketType;
        set
        {
            if (SetProperty(ref _bucketType, value))
            {
                OnPropertyChanged(nameof(IsAccumulatingDrawdown));
                OnPropertyChanged(nameof(FundingStatus));
                OnPropertyChanged(nameof(IsSkipped));
                OnPropertyChanged(nameof(IsFunded));
            }
        }
    }

    public bool IsAccumulatingDrawdown => BucketType == BucketType.AccumulatingDrawdown;

    // Status helper for DataGrid row styling and text displays
    public string FundingStatus
    {
        get
        {
            if (!IsAccumulatingDrawdown) return "Standard";
            if (Id > 0 && ActualAmount == 0) return "Skipped";
            if (Id > 0 && ActualAmount > 0) return "Funded";
            return "Pending"; // Virtual / Draft
        }
    }

    public bool IsSkipped => IsAccumulatingDrawdown && Id > 0 && ActualAmount == 0;
    public bool IsFunded => IsAccumulatingDrawdown && Id > 0 && ActualAmount > 0;
}