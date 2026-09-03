using System.Collections;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using StayOnTarget.ViewModels;

namespace StayOnTarget.Models;

public class BudgetBucket : ViewModelBase, INotifyDataErrorInfo {
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

    public int Id {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    [Required(ErrorMessage = "Envelope name is required.")]
    [MinLength(1, ErrorMessage = "Envelope name cannot be empty.")]
    public string Name {
        get => _name;
        set {
            if (SetProperty(ref _name, value)) {
                ValidateProperty(nameof(Name), value);
            }
        }
    }

    /// <summary>
    /// For Standard buckets: The pay-period allowance.
    /// For Accumulating/Upfront floors: The pay-period allocation contribution towards the target floor.
    /// </summary>
    public decimal ExpectedAmount {
        get => _expectedAmount;
        set => SetProperty(ref _expectedAmount, value);
    }

    public int? AccountId {
        get => _accountId;
        set => SetProperty(ref _accountId, value);
    }

    // public int? PaycheckId
    // {
    //     get => _paycheckId;
    //     set => SetProperty(ref _paycheckId, value);
    // }

    public bool IsArchived {
        get => _isArchived;
        set => SetProperty(ref _isArchived, value);
    }

    private bool _isActive;

    public bool IsActive {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
    }

    public BucketType Type {
        get => _type;
        set => SetProperty(ref _type, value);
    }

    /// <summary>
    /// Target total balance for Upfront and AccumulatingDrawdown floors.
    /// </summary>
    public decimal TargetBalance {
        get => _targetBalance;
        set => SetProperty(ref _targetBalance, value);
    }

    /// <summary>
    /// Persistent dynamic balance for AccumulatingDrawdown floors.
    /// Updated when contributions or drawdown transactions hit this bucket.
    /// </summary>
    public decimal CurrentBalance {
        get => _currentBalance;
        set => SetProperty(ref _currentBalance, value);
    }

    public decimal InitialBalance {
        get => _initialBalance;
        set => SetProperty(ref _initialBalance, value);
    }

    private TargetFrequencyType? _targetFrequency;

    public TargetFrequencyType? TargetFrequency {
        get => _targetFrequency;
        set => SetProperty(ref _targetFrequency, value);
    }

    private decimal _targetAmount;

    public decimal TargetAmount {
        get => _targetAmount;
        set => SetProperty(ref _targetAmount, value);
    }

    private DateTime? _nextDueDate;

    public DateTime? NextDueDate {
        get => _nextDueDate;
        set => SetProperty(ref _nextDueDate, value);
    }

    // Navigation collection for linked allocations
    public List<BucketPaycheckAllocation> PaycheckAllocations { get; set; } = new();

    // Key: "YYYY-MM", Value: explicit overridden amount
    public Dictionary<string, decimal> Overrides { get; set; } = new();

    /// <summary>
    /// Calculates the projection amount for any future date.
    /// Priority: 1) Specific "YYYY-MM" -> 2) Seasonal "MM" -> 3) Base ExpectedAmount
    /// </summary>
    public decimal GetEffectiveAmount(int year, int month) {
        string specificKey = $"{year:D4}-{month:D2}";
        string seasonalKey = $"{month:D2}";

        if (Overrides.TryGetValue(specificKey, out var specificAmount))
            return specificAmount;

        if (Overrides.TryGetValue(seasonalKey, out var seasonalAmount))
            return seasonalAmount;

        return ExpectedAmount;
    }

    public decimal GetEffectiveAmount(DateTime date)
        => GetEffectiveAmount(date.Year, date.Month);

    public BudgetBucket Clone() {
        return (BudgetBucket)this.MemberwiseClone();
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