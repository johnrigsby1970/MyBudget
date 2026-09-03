using System.Collections;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using StayOnTarget.ViewModels;

namespace StayOnTarget.Models;

public class Category : ViewModelBase, INotifyDataErrorInfo {
    private bool _isArchived;
    private int _sortOrder;
    private string _hexColor = "#FF0000FF"; // Default to Blue

    private int _id;

    public int Id {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    private int _categoryId;

    public int CategoryId {
        get => _id;
        set => SetProperty(ref _categoryId, value);
    }

    [Required(ErrorMessage = "Category name is required.")]
    [MinLength(1, ErrorMessage = "Category name cannot be empty.")]
    private string _name
        = string.Empty; // e.g., "Groceries"

    public string Name {
        get => _name;
        set {
            if (SetProperty(ref _name, value)) {
                ValidateProperty(nameof(Name), value);
            }
        }
    }

    public int SortOrder {
        get => _sortOrder;
        set => SetProperty(ref _sortOrder, value);
    }

    public bool IsArchived {
        get => _isArchived;
        set => SetProperty(ref _isArchived, value);
    }

    public string HexColor {
        get => _hexColor;
        set => SetProperty(ref _hexColor, value);
    }

    public List<SubCategory> Subcategories { get; set; } = new();

    public Category Clone() {
        return (Category)this.MemberwiseClone();
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