using System.Collections;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using CommunityToolkit.Mvvm.Input;
using StayOnTarget.DataAnnotation;
using StayOnTarget.Helpers;
using StayOnTarget.Services;
using StayOnTarget.Views.Wizard;

namespace StayOnTarget.ViewModels.Wizard;

public partial class DatabaseNameViewModel : ViewModelBase, IWizardStepViewModel, INotifyDataErrorInfo {
    public string StepTitle { get; }
    public int StepIndex { get; }

    public bool IsValid => !string.IsNullOrWhiteSpace(DatabaseName) &&
                           !string.IsNullOrWhiteSpace(Password) &&
                           Password == ConfirmPassword &&
                           DatabaseInitialized;

    private DatabaseInitializationContext DatabaseInitializationContext { get; }

    private string _databaseName = "budget.db";

    [Required(ErrorMessage = "Database name is required.")]
    [MinLength(1, ErrorMessage = "Database name must be at least 1 characters.")]
    [MaxLength(64, ErrorMessage = "Database name can be no longer than 64 characters.")]
    [DatabaseNameValidation]
    public string DatabaseName {
        get => _databaseName;
        set {
            if (SetProperty(ref _databaseName, value)) {
                OnPropertyChanged(nameof(IsValid));
                ValidateProperty(nameof(DatabaseName), value);
                InitializeDatabaseCommand.NotifyCanExecuteChanged();
            }
        }
    }

    private string _password = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [MinLength(12, ErrorMessage = "Password must be at least 12 characters and cannot contain tabs.")]
    public string Password {
        get => _password;
        set {
            if (SetProperty(ref _password, value)) {
                OnPropertyChanged(nameof(IsValid));
                ValidateProperty(nameof(Password), value);
                InitializeDatabaseCommand.NotifyCanExecuteChanged();
            }
        }
    }

    private string _confirmPassword = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [MinLength(12, ErrorMessage = "Password must be at least 12 characters and cannot contain tabs.")]
    public string ConfirmPassword {
        get => _confirmPassword;
        set {
            if (SetProperty(ref _confirmPassword, value)) {
                OnPropertyChanged(nameof(IsValid));
                ValidateProperty(nameof(ConfirmPassword), value);
                InitializeDatabaseCommand.NotifyCanExecuteChanged();
            }
        }
    }

    private bool _useWindowsHello = true;

    public bool UseWindowsHello {
        get => _useWindowsHello;
        set => SetProperty(ref _useWindowsHello, value);
    }

    private bool _databaseInitialized;

    public bool DatabaseInitialized {
        get => _databaseInitialized;
        set {
            if (SetProperty(ref _databaseInitialized, value)) {
                OnPropertyChanged(nameof(IsValid));
            }
        }
    }

    public DatabaseNameViewModel(DatabaseInitializationContext ctx) {
        DatabaseInitializationContext = ctx;
        StepTitle = "Database";
        StepIndex = 0;
    }

    public void OnStepNavigatedTo() { }

    [RelayCommand(CanExecute = nameof(CanInitialize))]
    private void InitializeDatabase() {
        try {
            List<string> errors = GetValidationErrors();

            if (errors.Any())
            {
                // Show only the first error found
                ErrorMessage = errors.First();
                return;
            }
            
            // Success path
            ErrorMessage = string.Empty;

            StayOnTarget.Properties.Settings.Default.DatabaseName = DatabaseName;
            string dbPath = StayOnTarget.Properties.Settings.Default.DatabasePath();
            // string dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            //     @"AppData\Local\StayOnTarget", DatabaseName);

            // In a real app, we might want to let the user pick the path, 
            // but for now we follow the pattern in DatabaseContext.

            var budgetService = new BudgetService(dbPath, Password);
            DatabaseInitializationContext.BudgetService = budgetService;
            
            try {
                // Save settings
                StayOnTarget.Properties.Settings.Default.UseWindowsHello = UseWindowsHello;
                StayOnTarget.Properties.Settings.Default.Save();
            }
            catch (Exception ex) {
                Serilog.Log.Error(ex, "Error during database name save attempt.");
            }

            if (UseWindowsHello) {
                VaultManager.SaveDatabaseKey(Password, "MasterKey"); //DatabaseName);
            }
            else {
                VaultManager.RemoveDatabaseKey( "MasterKey"); //DatabaseName);
            }
            
            DatabaseInitialized = true;
        }
        catch (Exception ex) {
            // Log error, maybe show message
            Serilog.Log.Error(ex, "Failed to initialize database in wizard.");
        }
    }

    private static bool IsValidDatabasePassword(string password) {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 12)
            return false; // Minimum length recommendation for DB encryption

        // Ensure no control characters (null bytes, newlines, tabs)
        foreach (char c in password) {
            if (char.IsControl(c))
                return false;
        }

        return true;
    }

    private bool CanInitialize()
    {
        // Allow clicking the button as long as fields aren't completely blank,
        // so GetValidationErrors() can run and display error messages if something is wrong.
        return !string.IsNullOrWhiteSpace(DatabaseName) && 
               !string.IsNullOrWhiteSpace(Password) && 
               !string.IsNullOrWhiteSpace(ConfirmPassword);
    }

    #region Error Validation

    private string _errorMessage = string.Empty;

    public string ErrorMessage {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    private readonly Dictionary<string, List<string>> _errors = new();

    public bool HasErrors => _errors.Any();

    public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

    public IEnumerable GetErrors(string? propertyName) {
        if (string.IsNullOrEmpty(propertyName) || !_errors.TryGetValue(propertyName, out var errors))
            return Array.Empty<object>();
        return errors;
    }

    public void AddError(string propertyName, string error) {
        if (!_errors.ContainsKey(propertyName))
            _errors[propertyName] = new List<string>();

        if (!_errors[propertyName].Contains(error)) {
            _errors[propertyName].Add(error);
            OnErrorsChanged(propertyName);
        }
    }

    public void ClearErrors(string propertyName) {
        if (_errors.Remove(propertyName))
            OnErrorsChanged(propertyName);
    }

    private void OnErrorsChanged(string propertyName) {
        ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
        // Re-evaluate your AddAccountCommand.CanExecute() here
    }

    public List<string> GetValidationErrors() {
        var errors = new List<string>();

        if (!DatabaseFileNameValidator.IsValidFileName(DatabaseName, out var errorMessage)) {
            errors.Add(errorMessage);
        }
        
        if (string.IsNullOrWhiteSpace(Password))
            errors.Add("Password is required.");

        if (!IsValidDatabasePassword(Password))
            errors.Add("Password must be 12 characters and cannot contain tabs.");

        if (Password!=ConfirmPassword)
            errors.Add("Confirmation password does not match password..");
        
        return errors;
    }
    
    private void ValidateProperty(string propertyName, object value)
    {
        var results = new List<ValidationResult>();
        var context = new ValidationContext(this) { MemberName = propertyName };

        // 1. Run DataAnnotations Validation ([Required], [MinLength], etc.)
        Validator.TryValidateProperty(value, context, results);

        List<string> propertyErrors = results.Select(r => r.ErrorMessage ?? string.Empty).ToList();

        // 2. Custom validation for DatabaseName
        if (propertyName == nameof(DatabaseName) && value is string dbName)
        {
            if (!DatabaseFileNameValidator.IsValidFileName(dbName, out var fileNameError))
            {
                propertyErrors.Add(fileNameError);
            }
        }

        // 3. Update dictionary
        if (propertyErrors.Any())
        {
            _errors[propertyName] = propertyErrors;
        }
        else
        {
            _errors.Remove(propertyName);
        }

        // Update the UI callout error message with the first active error found
        ErrorMessage = _errors.Values.SelectMany(x => x).FirstOrDefault() ?? string.Empty;

        ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
    }
    
    #endregion
}