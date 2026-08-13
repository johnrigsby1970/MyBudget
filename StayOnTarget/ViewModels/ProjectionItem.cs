using StayOnTarget.Services.Projections;

namespace StayOnTarget.ViewModels;

public class ProjectionItem : ViewModelBase
{
    private DateTime _transactionDate;
    private string _description = string.Empty;
    private decimal _amount;
    private decimal _balance;
    private bool _isWarning;
    private decimal? _periodNet;
    private int? _paycheckId;
    private int? _toAccountId;
    private int? _billId;
    private int? _bucketId;
    private int? _subCategoryId;
    private int? _fromAccountId;
    private Dictionary<string, decimal> _accountBalances = new();
    private bool _isSynthetic;
    private decimal _spendableBalance;
    private bool _isBelowFloor;
    private string? _warningMessage;

    public DateTime TransactionDate { get => _transactionDate; set => SetProperty(ref _transactionDate, value); }
    public string Description { get => _description; set => SetProperty(ref _description, value); }
    
    public int? PaycheckId 
    { 
        get => _paycheckId; 
        set 
        {
            if (SetProperty(ref _paycheckId, value))
            {
                OnPropertyChanged(nameof(NeedsAttention));
            }
        } 
    }

    public int? ToAccountId { get => _toAccountId; set => SetProperty(ref _toAccountId, value); }
    public int? FromAccountId { get => _fromAccountId; set => SetProperty(ref _fromAccountId, value); }
    public int? BillId { get => _billId; set => SetProperty(ref _billId, value); }

    public int? BucketId 
    {
        get => _bucketId; 
        set
        {
            if (SetProperty(ref _bucketId, value))
            {
                OnPropertyChanged(nameof(CanFundDrawdown));
            }
        }
    }

    public int? SubCategoryId { get => _subCategoryId; set => SetProperty(ref _subCategoryId, value); }

    private bool _inOrOutOfMoneyAccount;
    public bool InOrOutOfMoneyAccount { get => _inOrOutOfMoneyAccount; set => SetProperty(ref _inOrOutOfMoneyAccount, value); }

    private bool _inMoneyAccount;
    public bool InMoneyAccount { get => _inMoneyAccount; set => SetProperty(ref _inMoneyAccount, value); }

    private bool _outOfMoneyAccount;
    public bool OutOfMoneyAccount { get => _outOfMoneyAccount; set => SetProperty(ref _outOfMoneyAccount, value); }

    private bool _internalTransfer;
    public bool InternalTransfer { get => _internalTransfer; set => SetProperty(ref _internalTransfer, value); }

    public decimal SpendableBalance 
    { 
        get => _spendableBalance; 
        set => SetProperty(ref _spendableBalance, value); 
    }

    public bool IsBelowFloor 
    { 
        get => _isBelowFloor; 
        set 
        {
            if (SetProperty(ref _isBelowFloor, value))
            {
                IsWarning = value; // Synchronizes warning state cleanly
            }
        } 
    }

    public string? WarningMessage 
    { 
        get => _warningMessage; 
        set => SetProperty(ref _warningMessage, value); 
    }

    public bool NeedsAttention => _paycheckId.HasValue;
    
    public bool IsBucket => Type == ProjectionEngine.ProjectionEventType.Bucket || Type == ProjectionEngine.ProjectionEventType.AccumulatingDrawdown;
    
    public bool IsSweep => Type == ProjectionEngine.ProjectionEventType.Sweep || Type == ProjectionEngine.ProjectionEventType.Snowball || Type == ProjectionEngine.ProjectionEventType.Roth;
    
    private ProjectionEngine.ProjectionEventType _type;
    public ProjectionEngine.ProjectionEventType Type
    {
        get => _type;
        set
        {
            if (SetProperty(ref _type, value))
            {
                OnPropertyChanged(nameof(IsSweep));
                OnPropertyChanged(nameof(IsBucket));
                OnPropertyChanged(nameof(CanFundDrawdown));
            }
        }
    }
    
    public decimal Amount 
    { 
        get => _amount; 
        set => SetProperty(ref _amount, value);
    }

    public decimal Balance { get => _balance; set => SetProperty(ref _balance, value); }
    public bool IsWarning { get => _isWarning; set => SetProperty(ref _isWarning, value); }
    public decimal? PeriodNet { get => _periodNet; set => SetProperty(ref _periodNet, value); }
    
    public Dictionary<string, decimal> AccountBalances
    {
        get => _accountBalances;
        set => SetProperty(ref _accountBalances, value);
    }

    public bool IsSynthetic { get => _isSynthetic; set => SetProperty(ref _isSynthetic, value); }

    public decimal GetAccountBalance(string accountName)
    {
        return _accountBalances.TryGetValue(accountName, out var bal) ? bal : 0;
    }

    private bool _isReconciled;
    public bool IsReconciled
    {
        get => _isReconciled;
        set => SetProperty(ref _isReconciled, value);
    }
    
    public bool CanPayIt
    {
        get
        {
            if (!BillId.HasValue || BillId.Value == 0) return false;

            DateTime periodStart = MainViewModel.Instance?.CurrentPeriodDate == DateTime.MinValue 
                ? DateTime.Today 
                : (MainViewModel.Instance?.CurrentPeriodDate ?? DateTime.Today);

            if (MainViewModel.Instance?.ProjectionStartDate.HasValue == true)
            {
                periodStart = MainViewModel.Instance.ProjectionStartDate.Value;
            }

            return TransactionDate >= periodStart && TransactionDate <= periodStart.AddDays(31);
        }
    }
    
    public bool CanFundDrawdown
    {
        get
        {
            if (!BucketId.HasValue || BucketId.Value == 0) return false;
            return Type == ProjectionEngine.ProjectionEventType.AccumulatingDrawdown;
        }
    }
}