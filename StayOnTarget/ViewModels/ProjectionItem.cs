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
    private int? _fromAccountId;
    private Dictionary<string, decimal> _accountBalances = new();
    private bool _isSynthetic;

    public DateTime TransactionDate { get => _transactionDate; set => SetProperty(ref _transactionDate, value); }
    public string Description { get => _description; set => SetProperty(ref _description, value); }
    public int? PaycheckId { get => _paycheckId; set => SetProperty(ref _paycheckId, value); }
    public int? ToAccountId { get => _toAccountId; set => SetProperty(ref _toAccountId, value); }
    public int? FromAccountId { get => _fromAccountId; set => SetProperty(ref _fromAccountId, value); }
    public bool InOrOutOfMoneyAccount { get; set; }
    public bool InMoneyAccount { get; set; }
    public bool OutOfMoneyAccount { get; set; }
    public bool InternalTransfer { get; set; }
    
    public bool NeedsAttention { get => _paycheckId.HasValue;  }
    
    // Helper property for XAML DataTriggers
    public bool IsBucket => Type == ProjectionEngine.ProjectionEventType.Bucket;
    
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
}