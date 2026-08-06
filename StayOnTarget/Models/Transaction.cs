using StayOnTarget.Helpers;
using StayOnTarget.ViewModels;

namespace StayOnTarget.Models;

public class Transaction : ViewModelBase {
    private string _description = string.Empty;
    private string _normalizedDescription = string.Empty;
    private string? _memo = string.Empty;
    private decimal _amount;
    private DateTime _transactionDate = DateTime.Today;
    private int? _accountId;
    private int? _toAccountId; // For transfers
    private int? _paycheckId; // For associating with a projected paycheck
    private DateTime? _paycheckOccurrenceDate; // The date of the projected paycheck occurrence being replaced
    private int? _billId; // For bill association
    private int? _bucketId; // For bucket association
    private int? _subCategoryId; //For category association
    private DateTime _periodDate;
    private bool _isPrincipalOnly;

    private bool _isRebalance;

    // private bool _isReconciled;
    private bool _isCashAdvance;
    private bool _isBalanceTransfer;
    private bool _isInterestOnly;
    private int? _fromAccountReconciledId;
    private int? _toAccountReconciledId;
    private bool? _fromAccountIsCleared;
    private bool? _toAccountIsCleared;

    public int Id { get; set; }
    public long? FromRecordId { get; set; }
    public long? ToRecordId { get; set; }
    public string FitId { get; set; } = Guid.NewGuid().ToString();
    public Guid TransactionId { get; set; } = Guid.NewGuid();

    public string Description {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    public string NormalizedDescription {
        get => _normalizedDescription;
        set => SetProperty(ref _normalizedDescription, value);
    }

    public decimal Amount {
        get => _amount;
        set {
            if (SetProperty(ref _amount, value)) {
                //OnPropertyChanged(nameof(SignedAmount));
            }
        }
    }

    // public decimal? SignedAmount(int accountId) => accountId switch {
    //     var id when id == AccountId => -Amount,
    //     var id when id == ToAccountId => Amount,
    //     _ => null
    // };
    
    public decimal? SignedAmount(Account account)
    {
        if (AccountId == account.Id)
        {
            // Outflow: Normal accounts decrease (-), Liability accounts increase debt (+)
            return account.IsLiability ? Amount : -Amount;
        }
        else if (ToAccountId == account.Id)
        {
            // Inflow: Normal accounts increase (+), Liability accounts decrease debt (-)
            return account.IsLiability ? -Amount : Amount;
        }

        return null;
    }

    public DateTime TransactionDate {
        get => _transactionDate;
        set => SetProperty(ref _transactionDate, value);
    }

    public int? AccountId {
        get => _accountId;
        set => SetProperty(ref _accountId, value);
    }

    public string? Memo {
        get => _memo;
        set => SetProperty(ref _memo, value);
    }

    public int? ToAccountId {
        get => _toAccountId;
        set => SetProperty(ref _toAccountId, value);
    }

    public int? PaycheckId {
        get => _paycheckId;
        set => SetProperty(ref _paycheckId, value);
    }

    public DateTime? PaycheckOccurrenceDate {
        get => _paycheckOccurrenceDate;
        set => SetProperty(ref _paycheckOccurrenceDate, value);
    }

    public int? BillId {
        get => _billId;
        set => SetProperty(ref _billId, value);
    }

    public int? BucketId {
        get => _bucketId;
        set => SetProperty(ref _bucketId, value);
    }

    public DateTime PeriodDate {
        get => _periodDate;
        set => SetProperty(ref _periodDate, value);
    }
    
    public int? SubCategoryId {
        get => _subCategoryId;
        set => SetProperty(ref _subCategoryId, value);
    }


    public bool IsPrincipalOnly {
        get => _isPrincipalOnly;
        set => SetProperty(ref _isPrincipalOnly, value);
    }

    public bool IsRebalance {
        get => _isRebalance;
        set => SetProperty(ref _isRebalance, value);
    }

    public bool IsCashAdvance {
        get => _isCashAdvance;
        set => SetProperty(ref _isCashAdvance, value);
    }

    public bool IsBalanceTransfer {
        get => _isBalanceTransfer;
        set => SetProperty(ref _isBalanceTransfer, value);
    }

    public bool IsInterestOnly {
        get => _isInterestOnly;
        set => SetProperty(ref _isInterestOnly, value);
    }

    public int? FromAccountReconciledId {
        get => _fromAccountReconciledId;
        set => SetProperty(ref _fromAccountReconciledId, value);
    }

    public int? ToAccountReconciledId {
        get => _toAccountReconciledId;
        set => SetProperty(ref _toAccountReconciledId, value);
    }

    public bool? FromAccountIsCleared {
        get => _fromAccountIsCleared;
        set => SetProperty(ref _fromAccountIsCleared, value);
    }

    public bool? ToAccountIsCleared {
        get => _toAccountIsCleared;
        set => SetProperty(ref _toAccountIsCleared, value);
    }

    // Helper for UI
    public string? AccountName { get; set; }
    public string? ToAccountName { get; set; }
    public string? BillName { get; set; }
    public string? BucketName { get; set; }
}

public class TransactionViewModel : Transaction {
    private readonly Account _viewingAccount;
    private readonly bool _isLiability;
    
    // Default constructor for Newtonsoft.Json / Deserialization
    public TransactionViewModel() { }

    public TransactionViewModel(Transaction source, Account account) {
        // Copy base fields from raw Transaction entity
        PropertyCopier.CopyProperties(source, this);

        _viewingAccount = account;
    }

    // Perspective-calculated property ready for XAML binding
    public decimal? SignedAmount => SignedAmount(_viewingAccount);

    private decimal _runningBalance;

    public decimal RunningBalance {
        get => _runningBalance;
        set => SetProperty(ref _runningBalance, value);
    }

    private bool _isReconciled;

    public bool IsReconciled {
        get => _isReconciled;
        set => SetProperty(ref _isReconciled, value);
    }

    private bool _isCleared;

    public bool IsCleared {
        get => _isCleared;
        set => SetProperty(ref _isCleared, value);
    }

    private bool _isEnabled = true;

    public bool IsEnabled {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }
}

public class Ledger : ViewModelBase {
    private string _description = string.Empty;
    private string? _memo = string.Empty;
    private decimal _amount;
    private DateTime _transactionDate = DateTime.Today;
    private int? _accountId;
    private int? _paycheckId; // For associating with a projected paycheck
    private DateTime? _paycheckOccurrenceDate; // The date of the projected paycheck occurrence being replaced
    private int? _billId; // For bill association
    private int? _bucketId; // For bucket association
    private DateTime _periodDate;
    private bool _isPrincipalOnly;
    private bool _isRebalance;
    private bool _isCashAdvance;
    private bool _isBalanceTransfer;
    private bool _isInterestOnly;
    private int? _reconciliationId;
    private bool _isCleared;


    public int Id { get; set; }
    public string FitId { get; set; } = Guid.NewGuid().ToString();
    public Guid TransactionId { get; set; } = Guid.NewGuid();

    public string Description {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    public decimal Amount {
        get => _amount;
        set => SetProperty(ref _amount, value);
    }

    public DateTime TransactionDate {
        get => _transactionDate;
        set => SetProperty(ref _transactionDate, value);
    }

    public int? AccountId {
        get => _accountId;
        set => SetProperty(ref _accountId, value);
    }

    public string? Memo {
        get => _memo;
        set => SetProperty(ref _memo, value);
    }

    public int? PaycheckId {
        get => _paycheckId;
        set => SetProperty(ref _paycheckId, value);
    }

    public DateTime? PaycheckOccurrenceDate {
        get => _paycheckOccurrenceDate;
        set => SetProperty(ref _paycheckOccurrenceDate, value);
    }

    public int? ReconciliationId {
        get => _reconciliationId;
        set => SetProperty(ref _reconciliationId, value);
    }

    public int? BillId {
        get => _billId;
        set => SetProperty(ref _billId, value);
    }

    public int? BucketId {
        get => _bucketId;
        set => SetProperty(ref _bucketId, value);
    }

    public DateTime PeriodDate {
        get => _periodDate;
        set => SetProperty(ref _periodDate, value);
    }

    public bool IsPrincipalOnly {
        get => _isPrincipalOnly;
        set => SetProperty(ref _isPrincipalOnly, value);
    }

    public bool IsRebalance {
        get => _isRebalance;
        set => SetProperty(ref _isRebalance, value);
    }

    public bool IsCashAdvance {
        get => _isCashAdvance;
        set => SetProperty(ref _isCashAdvance, value);
    }

    public bool IsBalanceTransfer {
        get => _isBalanceTransfer;
        set => SetProperty(ref _isBalanceTransfer, value);
    }

    public bool IsInterestOnly {
        get => _isInterestOnly;
        set => SetProperty(ref _isInterestOnly, value);
    }

    public bool IsCleared {
        get => _isCleared;
        set => SetProperty(ref _isCleared, value);
    }
}