using StayOnTarget.Models;
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

    public DateTime TransactionDate { get => _transactionDate; set => SetProperty(ref _transactionDate, value); }
    public string Description { get => _description; set => SetProperty(ref _description, value); }
    public int? PaycheckId { get => _paycheckId; set => SetProperty(ref _paycheckId, value); }
    public int? ToAccountId { get => _toAccountId; set => SetProperty(ref _toAccountId, value); }
    public int? FromAccountId { get => _fromAccountId; set => SetProperty(ref _fromAccountId, value); }
    public int? BillId { get => _billId; set => SetProperty(ref _billId, value); }
    public int? BucketId { get => _bucketId; set => SetProperty(ref _bucketId, value); }
    public int? SubCategoryId { get => _subCategoryId; set => SetProperty(ref _subCategoryId, value); }
    public bool InOrOutOfMoneyAccount { get; set; }
    public bool InMoneyAccount { get; set; }
    public bool OutOfMoneyAccount { get; set; }
    public bool InternalTransfer { get; set; }
    
    public bool NeedsAttention { get => _paycheckId.HasValue;  }
    
    // Helper property for XAML DataTriggers
    public bool IsBucket => Type == ProjectionEngine.ProjectionEventType.Bucket || Type == ProjectionEngine.ProjectionEventType.AccumulatingDrawdown;
    
    public bool IsSweep => Type == ProjectionEngine.ProjectionEventType.Sweep || Type == ProjectionEngine.ProjectionEventType.Snowball || Type == ProjectionEngine.ProjectionEventType.Roth;
    
    private ProjectionEngine.ProjectionEventType _type;
    public ProjectionEngine.ProjectionEventType Type
    {
        get { return _type; }
        set
        {
            if (_type != value) {
                _type = value;
                OnPropertyChanged("Type");
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
        get { return _isReconciled; }
        set
        {
            if (_isReconciled != value) {
                _isReconciled = value;
                OnPropertyChanged("IsReconciled");
            }
        }
    }
    
    public bool CanPayIt
    {
        get
        {
            // Must be a bill
            if (!BillId.HasValue || BillId.Value == 0) return false;

            // Determine active period start (matches your VM logic)
            DateTime periodStart = MainViewModel.Instance?.CurrentPeriodDate == DateTime.MinValue 
                ? DateTime.Today 
                : (MainViewModel.Instance?.CurrentPeriodDate ?? DateTime.Today);

            if (MainViewModel.Instance?.ProjectionStartDate.HasValue == true)
            {
                periodStart = MainViewModel.Instance.ProjectionStartDate.Value;
            }

            // Only allow payment if transaction falls within 31 days from the period start date
            return TransactionDate >= periodStart && TransactionDate <= periodStart.AddDays(31);
        }
    }
    
    public bool CanFundDrawdown
    {
        get
        {
            // Must be a bill
            if (!BucketId.HasValue || BucketId.Value == 0) return false;

            var bucket = MainViewModel.Instance?.Buckets?.FirstOrDefault(x => x.Id == BucketId);
            //if (bucket == null || bucket.Type != BucketType.AccumulatingDrawdown) return false;
            if (Type != ProjectionEngine.ProjectionEventType.AccumulatingDrawdown) return false;
            return true;
        }
    }
}