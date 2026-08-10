using StayOnTarget.ViewModels;

namespace StayOnTarget.Models;

public class BudgetBucket : ViewModelBase
{
    private int _id;
    private string _name = string.Empty;
    private decimal _expectedAmount;
    private int? _accountId;
   // private int? _paycheckId;
    private bool _isArchived;
    
    // New Bucket Type Properties
    private BucketType _type = BucketType.Standard;
    private decimal _targetBalance;
    private decimal _currentBalance;
    private decimal _initialBalance;
    
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

    /// <summary>
    /// For Standard buckets: The pay-period allowance.
    /// For Accumulating/Upfront floors: The pay-period allocation contribution towards the target floor.
    /// </summary>
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
    
    // public int? PaycheckId
    // {
    //     get => _paycheckId;
    //     set => SetProperty(ref _paycheckId, value);
    // }
    
    public bool IsArchived
    {
        get => _isArchived;
        set => SetProperty(ref _isArchived, value);
    }
    
    private bool _isActive;
    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
    }

    public BucketType Type
    {
        get => _type;
        set => SetProperty(ref _type, value);
    }

    /// <summary>
    /// Target total balance for Upfront and AccumulatingDrawdown floors.
    /// </summary>
    public decimal TargetBalance
    {
        get => _targetBalance;
        set => SetProperty(ref _targetBalance, value);
    }

    /// <summary>
    /// Persistent dynamic balance for AccumulatingDrawdown floors.
    /// Updated when contributions or drawdown transactions hit this bucket.
    /// </summary>
    public decimal CurrentBalance
    {
        get => _currentBalance;
        set => SetProperty(ref _currentBalance, value);
    }
    
    public decimal InitialBalance
    {
        get => _initialBalance;
        set => SetProperty(ref _initialBalance, value);
    }
    
    private TargetFrequencyType? _targetFrequency;
    public TargetFrequencyType? TargetFrequency
    {
        get => _targetFrequency;
        set => SetProperty(ref _targetFrequency, value);
    }
    
    private decimal _targetAmount;
    public decimal TargetAmount
    {
        get => _targetAmount;
        set => SetProperty(ref _targetAmount, value);
    }
    
    private DateTime? _nextDueDate;
    public DateTime? NextDueDate
    {
        get => _nextDueDate;
        set => SetProperty(ref _nextDueDate, value);
    }
    
    // Navigation collection for linked allocations
    public List<BucketPaycheckAllocation> PaycheckAllocations { get; set; } = new();
}