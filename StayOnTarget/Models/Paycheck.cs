using System.Collections;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using StayOnTarget.ViewModels;

namespace StayOnTarget.Models;

public class Paycheck : ViewModelBase, INotifyDataErrorInfo {
    private string _name = "Regular Paycheck";
    private decimal _expectedAmount;
    private Frequency _frequency = Frequency.BiWeekly;
    private DateTime _startDate = DateTime.Today;
    private DateTime? _endDate;
    private int? _accountId;
    private bool _isBalanced;

    private int _id;

    public int Id {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    [Required(ErrorMessage = "Paycheck name is required.")]
    [MinLength(1, ErrorMessage = "Paycheck name cannot be empty.")]
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

    public DateTime StartDate {
        get => _startDate;
        set => SetProperty(ref _startDate, value);
    }

    public DateTime? EndDate {
        get => _endDate;
        set => SetProperty(ref _endDate, value);
    }

    public int? AccountId {
        get => _accountId;
        set => SetProperty(ref _accountId, value);
    }

    public bool IsBalanced {
        get => _isBalanced;
        set => SetProperty(ref _isBalanced, value);
    }

    public Paycheck Clone() {
        return (Paycheck)this.MemberwiseClone();
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