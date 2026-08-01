using System.IO;
using CommunityToolkit.Mvvm.Input;
using StayOnTarget.Data;
using StayOnTarget.Services;
using StayOnTarget.Views.Wizard;

namespace StayOnTarget.ViewModels.Wizard;

public partial class DatabaseNameViewModel : ViewModelBase, IWizardStepViewModel
{
    public string StepTitle { get; }
    public int StepIndex { get; }

    public bool IsValid => !string.IsNullOrWhiteSpace(DatabaseName) &&
                           !string.IsNullOrWhiteSpace(Password) &&
                           Password == ConfirmPassword &&
                           DatabaseInitialized;

    private DatabaseInitializationContext DatabaseInitializationContext { get; }

    private string _databaseName = "budget.db";

    public string DatabaseName
    {
        get => _databaseName;
        set
        {
            if (SetProperty(ref _databaseName, value))
            {
                OnPropertyChanged(nameof(IsValid));
                InitializeDatabaseCommand.NotifyCanExecuteChanged();
            }
        }
    }

    private string _password = string.Empty;

    public string Password
    {
        get => _password;
        set
        {
            if (SetProperty(ref _password, value))
            {
                OnPropertyChanged(nameof(IsValid));
                InitializeDatabaseCommand.NotifyCanExecuteChanged();
            }
        }
    }

    private string _confirmPassword = string.Empty;

    public string ConfirmPassword
    {
        get => _confirmPassword;
        set
        {
            if (SetProperty(ref _confirmPassword, value))
            {
                OnPropertyChanged(nameof(IsValid));
                InitializeDatabaseCommand.NotifyCanExecuteChanged();
            }
        }
    }

    private bool _useWindowsHello = true;

    public bool UseWindowsHello
    {
        get => _useWindowsHello;
        set => SetProperty(ref _useWindowsHello, value);
    }

    private bool _databaseInitialized;

    public bool DatabaseInitialized
    {
        get => _databaseInitialized;
        set
        {
            if (SetProperty(ref _databaseInitialized, value))
            {
                OnPropertyChanged(nameof(IsValid));
            }
        }
    }

    public DatabaseNameViewModel(DatabaseInitializationContext ctx)
    {
        DatabaseInitializationContext = ctx;
        StepTitle = "Database";
        StepIndex = 0;
    }

    public void OnStepNavigatedTo()
    {
    }

    [RelayCommand(CanExecute = nameof(CanInitialize))]
    private void InitializeDatabase()
    {
        try
        {
            string dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                @"AppData\Local\StayOnTarget", DatabaseName);

            // In a real app, we might want to let the user pick the path, 
            // but for now we follow the pattern in DatabaseContext.
            
            var budgetService = new BudgetService(dbPath, Password);
            DatabaseInitializationContext.BudgetService = budgetService;
            
            // Save settings
            StayOnTarget.Properties.Settings.Default.UseWindowsHello = UseWindowsHello;
            StayOnTarget.Properties.Settings.Default.Save();
            
            DatabaseInitialized = true;
        }
        catch (Exception ex)
        {
            // Log error, maybe show message
            Serilog.Log.Error(ex, "Failed to initialize database in wizard.");
        }
    }

    private bool CanInitialize() => 
        !string.IsNullOrWhiteSpace(DatabaseName) && 
        !string.IsNullOrWhiteSpace(Password) && 
        Password == ConfirmPassword;
}