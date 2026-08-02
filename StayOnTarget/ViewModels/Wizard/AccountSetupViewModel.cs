using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json;
using Serilog;
using StayOnTarget.Models;
using StayOnTarget.Services;
using StayOnTarget.Views;
using StayOnTarget.Views.Wizard;

namespace StayOnTarget.ViewModels.Wizard;

public partial class AccountSetupViewModel : ViewModelBase, IWizardStepViewModel, INotifyDataErrorInfo {
    public string StepTitle { get; }
    public int StepIndex { get; }
    public bool IsValid => Accounts.Any();
    private DatabaseInitializationContext DatabaseInitializationContext { get; }

    public ObservableCollection<Account> Accounts => DatabaseInitializationContext.Accounts;
    public ObservableCollection<Account> ActiveAccountsWithNone => DatabaseInitializationContext.ActiveAccountsWithNone;

    public AccountType[] AccountTypes => (AccountType[])Enum.GetValues(typeof(AccountType));

    private Account _editingAccount = new() {
        Name = "Main Checking",
        Type = AccountType.Checking,
        Balance = 0,
        IsPrimary = false,
        BalanceAsOf = DateTime.Today.AddDays(-1),
        IncludeInTotal = true,
        HexColor = "#FF007ACC",
        MortgageDetails = new MortgageDetails(),
        CreditCardDetails = new CreditCardDetails()
    };

    public Account EditingAccount
    {
        get => _editingAccount;
        set
        {
            if (_editingAccount != value)
            {
                // 1. Unsubscribe from old Account AND old child objects
                UnsubscribeFromAccountEvents(_editingAccount);

                _editingAccount = value;
                OnPropertyChanged();

                // 2. Subscribe to new Account AND new child objects
                SubscribeToAccountEvents(_editingAccount);

                // 3. Refresh command state immediately
                AddAccountCommand.NotifyCanExecuteChanged();
            }
        }
    }
    
    private void SubscribeToAccountEvents(Account account)
    {
        if (account == null) return;

        // Unsubscribe FIRST to avoid duplicate handlers
        UnsubscribeFromAccountEvents(account);
        
        // Listen to top-level Account properties
        account.PropertyChanged += OnEditingAccountPropertyChanged;

        // Listen to child object properties
        if (account.CreditCardDetails != null)
            account.CreditCardDetails.PropertyChanged += OnEditingAccountPropertyChanged;

        if (account.MortgageDetails != null)
            account.MortgageDetails.PropertyChanged += OnEditingAccountPropertyChanged;
    }

    private void UnsubscribeFromAccountEvents(Account account)
    {
        if (account == null) return;

        account.PropertyChanged -= OnEditingAccountPropertyChanged;

        if (account.CreditCardDetails != null)
            account.CreditCardDetails.PropertyChanged -= OnEditingAccountPropertyChanged;

        if (account.MortgageDetails != null)
            account.MortgageDetails.PropertyChanged -= OnEditingAccountPropertyChanged;
    }
    
    private void OnEditingAccountPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        // 1. If the Account Type dropdown changed, initialize child details & wire up events
        if (e.PropertyName == nameof(Account.Type))
        {
            HandleTypeChange(_editingAccount);
        }
        
        // Re-evaluate CanExecute when Name, BankName, etc. change inside the model
        AddAccountCommand.NotifyCanExecuteChanged();
    }

    private void HandleTypeChange(Account account)
    {
        if (account == null) return;

        // Instantiate child objects if they don't exist yet
        if (account.Type == AccountType.CreditCard && account.CreditCardDetails == null)
        {
            account.CreditCardDetails = new CreditCardDetails();
        }
        else if (account.Type == AccountType.Mortgage && account.MortgageDetails == null)
        {
            account.MortgageDetails = new MortgageDetails();
        }

        // Crucial: Re-subscribe so event listeners attach to the child object!
        SubscribeToAccountEvents(account);
    }
    
    public AccountSetupViewModel(DatabaseInitializationContext ctx) {
        DatabaseInitializationContext = ctx;
        StepTitle = "Accounts";
        StepIndex = 1;

        _editingAccount.IsPrimary = !Accounts.Any(a => (a.Type== AccountType.Checking || a.Type== AccountType.Savings) && a.IsPrimary);
        
        // Subscribe to the default initial instance
        if (_editingAccount != null)
        {
            SubscribeToAccountEvents(_editingAccount);
        }
    }

    public void OnStepNavigatedTo() {
        OnPropertyChanged(nameof(Accounts));
        
        if (ActiveAccountsWithNone.Count == 0) {
            ActiveAccountsWithNone.Add(new Account { Id = 0, Name = "(None)" });
        }

        OnPropertyChanged(nameof(ActiveAccountsWithNone));
        OnPropertyChanged(nameof(IsValid));
    }

    private bool CanAddAccount()
    {
        if (EditingAccount == null) return false;

        // Basic requirements
        if (string.IsNullOrWhiteSpace(EditingAccount.Name)) return false;
        if (string.IsNullOrWhiteSpace(EditingAccount.BankName)) return false;

        // Conditional requirements based on type
        if (EditingAccount.Type == AccountType.Mortgage)
        {
            var m = EditingAccount.MortgageDetails;
            if (m == null || m.InterestRate <= 0 || m.LoanPayment <= 0 || m.StatementDay <= 0)
                return false;
        }

        if (EditingAccount.Type == AccountType.CreditCard)
        {
            var cc = EditingAccount.CreditCardDetails;
            if (cc == null || cc.StatementDay <= 0 || EditingAccount.AccountAprHistory == null)
                return false;
        }

        return true;
    }
    
    [RelayCommand(CanExecute = nameof(CanAddAccount))]
    private async Task AddAccountAsync() {
        //if (string.IsNullOrWhiteSpace(EditingAccount.Name)) return;

        if (DatabaseInitializationContext.BudgetService == null) return;

        try {
            List<string> errors = GetValidationErrors(EditingAccount);

            if (errors.Any())
            {
                // Show only the first error found
                ErrorMessage = errors.First();
                return;
            }
            
            // Success path
            ErrorMessage = string.Empty;
            
            var account = new Account {
                Name = EditingAccount.Name,
                BankName = EditingAccount.BankName,
                Type = EditingAccount.Type,
                Balance = EditingAccount.Balance,
                BalanceAsOf = EditingAccount.BalanceAsOf,
                IncludeInTotal = EditingAccount.IncludeInTotal,
                HexColor = EditingAccount.HexColor,
                IsPrimary = EditingAccount.IsPrimary,
                MortgageDetails = new MortgageDetails(),
                CreditCardDetails = new CreditCardDetails(),
                AccountAprHistory = new List<AccountAprHistory>()
            };
            
            account.MortgageDetails = EditingAccount.MortgageDetails;
            account.CreditCardDetails = EditingAccount.CreditCardDetails;
            account.AccountAprHistory = EditingAccount.AccountAprHistory;

            account.Id = await DatabaseInitializationContext.BudgetService.UpsertAccountAsync(account);

            var debtAccountTypes = new List<AccountType>()
                { AccountType.Auto, AccountType.CreditCard, AccountType.Mortgage, AccountType.PersonalLoan };
            var isDebtAccount = debtAccountTypes.Contains(account.Type);
            // if (account.Balance > 0) {
            //     isDebtAccount = false; //for purposes of initial balance, if the balance is positive,
            //                            //it's not a debt account. It is one, but it is currently carrying
            //                            //a positive balance.
            // }
            var openingBalance = new Transaction() {
                AccountId = isDebtAccount ? account.Id : null,
                ToAccountId = isDebtAccount
                    ? null
                    : account.Id,
                AccountName = isDebtAccount
                    ? account.Name
                    : null,
                ToAccountName = isDebtAccount
                    ? null
                    : account.Name,
                Amount = isDebtAccount ? -1 * account.Balance: account.Balance, //the balance is entered as a positive number, but we want to record it as a negative number
                TransactionDate = account.BalanceAsOf,
                TransactionId = Guid.NewGuid(),
                FitId = Guid.NewGuid().ToString(),
                Description = Constants.OpeningBalance,
                Memo = Constants.OpeningBalance
            };

            if (openingBalance.Amount != 0) {
                try {
                    await DatabaseInitializationContext.BudgetService.UpsertTransactionAsync(openingBalance);
                }
                catch (Exception ex) {
                    Log.Error(ex, "Error upserting transaction in Wizard.");
                }

                // List<Transaction> transactions = new List<Transaction>();
                // if (openingBalance.AccountId.HasValue) {
                //     transactions.AddRange(
                //         await DatabaseInitializationContext.BudgetService.GetAccountTransactionsAsync(openingBalance.AccountId.Value));
                // }
                //
                // if (openingBalance.ToAccountId.HasValue) {
                //     transactions.AddRange(
                //         await DatabaseInitializationContext.BudgetService.GetAccountTransactionsAsync(openingBalance.ToAccountId.Value));
                // }

                var reconciliationService = new ReconciliationService(DatabaseInitializationContext.BudgetService);
                string json = JsonConvert.SerializeObject(new List<Transaction>(){openingBalance});
                var reconciliationTransactions =
                    JsonConvert.DeserializeObject<List<ReconciliationTransaction>>(json);
                if (reconciliationTransactions != null) {
                    foreach (var reconciliationTransaction in reconciliationTransactions) {
                        reconciliationTransaction.IsReconciled = true;
                    }
                    if (openingBalance.AccountId.HasValue) {
                        await reconciliationService.ReconcileAccountAsync(
                            openingBalance.AccountId.Value,
                            reconciliationTransactions,
                            openingBalance.Amount,
                            openingBalance.TransactionDate);
                    }
                
                    if (openingBalance.ToAccountId.HasValue) {
                        await reconciliationService.ReconcileAccountAsync(
                            openingBalance.ToAccountId.Value,
                            reconciliationTransactions,
                            openingBalance.Amount,
                            openingBalance.TransactionDate);
                    }
                }
            }

            Accounts.Add(account);

            ActiveAccountsWithNone.Add(JsonConvert.DeserializeObject<Account>(JsonConvert.SerializeObject(account)) ??
                                       throw new InvalidOperationException());
            
            // Reset for next
            EditingAccount = new Account {
                Name = "",
                Type = AccountType.Checking,
                Balance = 0,
                BalanceAsOf = DateTime.Today.AddDays(-1),
                IncludeInTotal = true,
                IsPrimary = !Accounts.Any(a => (a.Type== AccountType.Checking || _editingAccount.Type== AccountType.Savings) && a.IsPrimary),
                HexColor = "#FF808080",
                MortgageDetails = new MortgageDetails(),
                CreditCardDetails = new CreditCardDetails(),
                AccountAprHistory = new List<AccountAprHistory>()
            };

            OnPropertyChanged(nameof(IsValid));
        }
        catch (Exception ex) {
            Log.Error(ex, "Error adding account");
        }
    }

    // [RelayCommand]
    // private async Task ImportStatementAsync(Account? account) {
    //     if (account == null || DatabaseInitializationContext.BudgetService == null) return;
    //     try {
    //         var window = new ImportReconciliationWindow(account, DatabaseInitializationContext.BudgetService) {
    //             Owner = Application.Current.MainWindow
    //         };
    //         window.ShowDialog();
    //     }
    //     catch (Exception ex) {
    //         Log.Error(ex, "Error showing import window.");
    //         MessageBox.Show("Failed to open import window. See log for details.", "Error", MessageBoxButton.OK,
    //             MessageBoxImage.Error);
    //     }
    // }
    
    [RelayCommand]
    private async Task DeleteAccountAsync(Account? account) {
        if (account == null || DatabaseInitializationContext.BudgetService == null) return;

        await DatabaseInitializationContext.BudgetService.DeleteAccountAsync(account.Id);
        Accounts.Remove(account);
        OnPropertyChanged(nameof(IsValid));
    }

    [RelayCommand]
    private async Task SetAccountAprRatesAsync() {
        if (EditingAccount is not { Type: AccountType.CreditCard }) return;
        if (DatabaseInitializationContext.BudgetService == null) return;

        try {
            EditingAccount.AccountAprHistory ??= [];
            var window = new AccountAprHistoryWindow(EditingAccount, DatabaseInitializationContext.BudgetService) {
                Owner = Application.Current.MainWindow
            };
            // 1. Blocks here until the user closes the interest rate window
            window.ShowDialog();
            // 2. Re-subscribe events (to catch any new objects/collections attached inside the dialog)
            SubscribeToAccountEvents(EditingAccount);

            // 3. Notify the UI that EditingAccount state may have changed
            OnPropertyChanged(nameof(EditingAccount));

            // 4. Force the Add Account button to re-evaluate its enabled state
            AddAccountCommand.NotifyCanExecuteChanged();
        }
        catch (Exception ex) {
            Log.Error(ex, "Error showing APR history window.");
            MessageBox.Show("Failed to open interest rate window. See log for details.", "Error", MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    #region Error Validation
    
    private string _errorMessage  = string.Empty;
    public string ErrorMessage {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    private readonly Dictionary<string, List<string>> _errors = new();

    public bool HasErrors => _errors.Any();
    
    public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;
    
    public IEnumerable GetErrors(string? propertyName)
    {
        if (string.IsNullOrEmpty(propertyName) || !_errors.ContainsKey(propertyName))
            return null;
        return _errors[propertyName];
    }
    
    public void AddError(string propertyName, string error)
    {
        if (!_errors.ContainsKey(propertyName))
            _errors[propertyName] = new List<string>();

        if (!_errors[propertyName].Contains(error))
        {
            _errors[propertyName].Add(error);
            OnErrorsChanged(propertyName);
        }
    }

    public void ClearErrors(string propertyName)
    {
        if (_errors.Remove(propertyName))
            OnErrorsChanged(propertyName);
    }

    private void OnErrorsChanged(string propertyName)
    {
        ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
        // Re-evaluate your AddAccountCommand.CanExecute() here
    }
    
    public List<string> GetValidationErrors(Account account)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(account.Name))
            errors.Add("Account name is required.");

        if (string.IsNullOrWhiteSpace(account.BankName))
            errors.Add("Bank name is required.");

        if (account.Type == AccountType.Mortgage)
        {
            var m = account.MortgageDetails;
            if (m == null)
            {
                errors.Add("Mortgage details must be defined.");
            }
            else
            {
                if (m.InterestRate <= 0) errors.Add("Mortgage interest rate is required.");
                if (m.LoanPayment <= 0) errors.Add("Mortgage payment is required.");
                if (m.StatementDay <= 0) errors.Add("Mortgage statement day is required.");
            }
        }

        if (account.Type == AccountType.CreditCard)
        {
            var cc = account.CreditCardDetails;
            if (cc == null || cc.StatementDay <= 0)
                errors.Add("Credit card statement day is required.");

            if (account.AccountAprHistory == null)
                errors.Add("Credit card interest rate must be set.");
        }

        return errors;
    }
    
    #endregion
}