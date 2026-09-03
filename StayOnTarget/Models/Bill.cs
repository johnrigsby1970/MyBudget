using System.Collections;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using StayOnTarget.ViewModels;

namespace StayOnTarget.Models;

public class Bill : ViewModelBase, INotifyDataErrorInfo {
    private string _name = string.Empty;
    private decimal _expectedAmount;
    private Frequency _frequency = Frequency.Monthly;
    private int _dueDay;
    private int? _accountId;
    private int? _toAccountId;
    private DateTime? _nextDueDate;
    private string _category = string.Empty;
    private bool _isActive = true;
    private bool _isPrincipalOnly;
    private bool _isArchived;

    private int _id;

    public int Id {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    [Required(ErrorMessage = "Bill name is required.")]
    [MinLength(1, ErrorMessage = "Bill name cannot be empty.")]
    public string Name {
        get => _name;
        set {
            if (SetProperty(ref _name, value)) {
                ValidateProperty(nameof(Name), value);
            }
        }
    }

    public decimal ExpectedAmount {
        get => _expectedAmount;
        set => SetProperty(ref _expectedAmount, value);
    }

    public Frequency Frequency {
        get => _frequency;
        set => SetProperty(ref _frequency, value);
    }

    public int DueDay {
        get => _dueDay;
        set => SetProperty(ref _dueDay, value);
    }

    public int? AccountId {
        get => _accountId;
        set => SetProperty(ref _accountId, value);
    }

    public int? ToAccountId {
        get => _toAccountId;
        set => SetProperty(ref _toAccountId, value);
    }

    public DateTime? NextDueDate {
        get => _nextDueDate;
        set => SetProperty(ref _nextDueDate, value);
    }

    public string Category {
        get => _category;
        set => SetProperty(ref _category, value);
    }

    public bool IsActive {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
    }

    public bool IsArchived {
        get => _isArchived;
        set => SetProperty(ref _isArchived, value);
    }

    public bool IsPrincipalOnly {
        get => _isPrincipalOnly;
        set => SetProperty(ref _isPrincipalOnly, value);
    }

    private int? _bucketId;

    public int? BucketId {
        get => _bucketId;
        set => SetProperty(ref _bucketId, value);
    }

    private int? _subCategoryId;

    public int? SubCategoryId {
        get => _subCategoryId;
        set => SetProperty(ref _subCategoryId, value);
    }

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

    public Bill Clone() {
        return (Bill)this.MemberwiseClone();
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