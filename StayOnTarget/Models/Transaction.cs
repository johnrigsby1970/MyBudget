using System.Collections;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using StayOnTarget.Helpers;
using StayOnTarget.ViewModels;

namespace StayOnTarget.Models;

public class Transaction : ViewModelBase, INotifyDataErrorInfo {
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
    private int? _fromAccountReconciliationId;
    private int? _toAccountReconciliationId;
    private bool? _fromAccountIsCleared;
    private bool? _toAccountIsCleared;

    private int _id;

    public int Id {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    private long? _fromRecordId;

    public long? FromRecordId {
        get => _fromRecordId;
        set => SetProperty(ref _fromRecordId, value);
    }

    private long? _toRecordId;

    public long? ToRecordId {
        get => _toRecordId;
        set => SetProperty(ref _toRecordId, value);
    }


    private string _toFitId = Guid.NewGuid().ToString();

    public string ToFitId {
        get => _toFitId;
        set => SetProperty(ref _toFitId, value);
    }

    private string _fromFitId = Guid.NewGuid().ToString();

    public string FromFitId {
        get => _fromFitId;
        set => SetProperty(ref _fromFitId, value);
    }


    private Guid _transactionId = Guid.NewGuid();

    public Guid TransactionId {
        get => _transactionId;
        set => SetProperty(ref _transactionId, value);
    }

    [MinLength(1)]
    [Required]
    public string Description {
        get => _description;
        set {
            if (SetProperty(ref _description, value)) {
                ValidateProperty(nameof(Description), value);
            }
        }
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

    public decimal? SignedAmount(Account account) {
        if (AccountId == account.Id) {
            // Outflow: Normal accounts decrease (-), Liability accounts increase debt (+)
            return account.IsLiability ? Amount : -Amount;
        }
        else if (ToAccountId == account.Id) {
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

    public int? FromAccountReconciliationId {
        get => _fromAccountReconciliationId;
        set => SetProperty(ref _fromAccountReconciliationId, value);
    }

    public int? ToAccountReconciliationId {
        get => _toAccountReconciliationId;
        set => SetProperty(ref _toAccountReconciliationId, value);
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

    private string? _accountName;

    public string? AccountName {
        get => _accountName;
        set => SetProperty(ref _accountName, value);
    }

    private string? _toAccountName;

    public string? ToAccountName {
        get => _toAccountName;
        set => SetProperty(ref _toAccountName, value);
    }

    private string? _billName;

    public string? BillName {
        get => _billName;
        set => SetProperty(ref _billName, value);
    }

    private string? _bucketName;

    public string? BucketName {
        get => _bucketName;
        set => SetProperty(ref _bucketName, value);
    }

    public Transaction Clone() {
        return (Transaction)this.MemberwiseClone();
    }

    #region Error Validation

    // --- INotifyDataErrorInfo Implementation ---

    private readonly Dictionary<string, List<string>> _errors = new();
    public bool HasErrors => _errors.Any();
    public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

    public IEnumerable GetErrors(string? propertyName) {
        if (string.IsNullOrEmpty(propertyName)) {
            return _errors.Values.SelectMany(e => e);
        }

        return _errors.TryGetValue(propertyName, out var errors) ? errors : Enumerable.Empty<string>();
    }

    private void ValidateProperty(string propertyName, object value) {
        var results = new List<ValidationResult>();
        var context = new ValidationContext(this) { MemberName = propertyName };

        Validator.TryValidateProperty(value, context, results);

        if (results.Any()) {
            _errors[propertyName] = results.Where(r => !string.IsNullOrEmpty(r.ErrorMessage))
                .Select(r => r.ErrorMessage!).ToList();
        }
        else {
            _errors.Remove(propertyName);
        }

        ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
    }

    public bool ValidateAllProperties() {
        var results = new List<ValidationResult>();
        var context = new ValidationContext(this);

        // Track old properties that had errors so we can clear their visual state if now valid
        var previousPropertiesWithErrors = _errors.Keys.ToList();
        _errors.Clear();

        if (!Validator.TryValidateObject(this, context, results, validateAllProperties: true)) {
            foreach (var result in results) {
                foreach (var memberName in result.MemberNames) {
                    if (!_errors.ContainsKey(memberName)) {
                        _errors[memberName] = new List<string>();
                    }

                    if (!string.IsNullOrEmpty(result.ErrorMessage)) {
                        _errors[memberName].Add(result.ErrorMessage);
                    }
                }
            }
        }

        // Combine all modified properties (both new errors and newly cleared errors)
        var affectedProperties = previousPropertiesWithErrors.Union(_errors.Keys).Distinct();

        // Raise ErrorsChanged for each specific property so WPF updates each TextBox border
        foreach (var propertyName in affectedProperties) {
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
        }

        return !_errors.Any();
    }

    #endregion
}

public class TransactionViewModel : Transaction {
    private readonly Account? _viewingAccount;

    // Default constructor for Newtonsoft.Json / Deserialization
    public TransactionViewModel() { }

    public TransactionViewModel(Transaction source, Account account) {
        // Copy base fields from raw Transaction entity
        PropertyCopier.CopyProperties(source, this);

        _viewingAccount = account;

        // Trigger SignedAmount notification whenever core transaction amounts/accounts shift
        this.PropertyChanged += (s, e) => {
            if (e.PropertyName == nameof(Amount) ||
                e.PropertyName == nameof(AccountId) ||
                e.PropertyName == nameof(ToAccountId)) {
                OnPropertyChanged(nameof(SignedAmount));
            }
        };
    }

    // Perspective-calculated property ready for XAML binding
    public new decimal? SignedAmount => _viewingAccount != null ? SignedAmount(_viewingAccount) : Amount;

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
    
    public bool WasOriginallyCleared { get; set; }
    
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

    private RangeObservableCollection<Ledger> _details = new();
    public RangeObservableCollection<Ledger> Details {
        get => _details;
        set => SetProperty(ref _details, value);
    }

    // Helper to check if this is a split entry
    public bool IsSplit => Details.Any();
    
    private int _id;

    public int Id {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    private string _fitId = Guid.NewGuid().ToString();

    public string FitId {
        get => _fitId;
        set => SetProperty(ref _fitId, value);
    }

    private int? _subCategoryId;

    public int? SubCategoryId {
        get => _subCategoryId;
        set => SetProperty(ref _subCategoryId, value);
    }

    private Guid _transactionId = Guid.NewGuid();

    public Guid TransactionId {
        get => _transactionId;
        set => SetProperty(ref _transactionId, value);
    }

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