using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json;
using Serilog;
using StayOnTarget.Models;
using StayOnTarget.Views.Wizard;

namespace StayOnTarget.ViewModels.Wizard;

public partial class AccountSetupViewModel : ViewModelBase, IWizardStepViewModel {
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
        BalanceAsOf = DateTime.Today,
        IncludeInTotal = true,
        HexColor = "#FF007ACC"
    };

    public Account EditingAccount {
        get => _editingAccount;
        set => SetProperty(ref _editingAccount, value);
    }

    public AccountSetupViewModel(DatabaseInitializationContext ctx) {
        DatabaseInitializationContext = ctx;
        StepTitle = "Account Setup";
        StepIndex = 1;

        _editingAccount.IsPrimary = !Accounts.Any(a => (_editingAccount.Type== AccountType.Checking || _editingAccount.Type== AccountType.Savings) && a.IsPrimary);
    }

    public void OnStepNavigatedTo() {
        OnPropertyChanged(nameof(Accounts));
        
        if (ActiveAccountsWithNone.Count == 0) {
            ActiveAccountsWithNone.Add(new Account { Id = 0, Name = "(None)" });
        }

        OnPropertyChanged(nameof(ActiveAccountsWithNone));
        OnPropertyChanged(nameof(IsValid));
    }

    [RelayCommand]
    private async Task AddAccountAsync() {
        if (string.IsNullOrWhiteSpace(EditingAccount.Name)) return;

        if (DatabaseInitializationContext.BudgetService == null) return;

        try {
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
                CreditCardDetails = new CreditCardDetails()
            };

            account.Id = await DatabaseInitializationContext.BudgetService.UpsertAccountAsync(account);

            var debtAccountTypes = new List<AccountType>()
                { AccountType.Auto, AccountType.CreditCard, AccountType.Mortgage, AccountType.PersonalLoan };

            var openingBalance = new Transaction() {
                AccountId = debtAccountTypes.Contains(account.Type) ? account.Id : null,
                ToAccountId = debtAccountTypes.Contains(account.Type)
                    ? null
                    : account.Id,
                AccountName = debtAccountTypes.Contains(account.Type)
                    ? account.Name
                    : null,
                ToAccountName = debtAccountTypes.Contains(account.Type)
                    ? null
                    : account.Name,
                Amount = account.Balance,
                TransactionDate = account.BalanceAsOf,
                TransactionId = Guid.NewGuid(),
                FitId = Guid.NewGuid().ToString(),
                Description = "Opening Balance",
                Memo = "Opening Balance"
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

                // string json = JsonConvert.SerializeObject(transactions.ToList());
                // var reconciliationTransactions =
                //     JsonConvert.DeserializeObject<List<ReconciliationTransaction>>(json);
                // if (reconciliationTransactions != null) {
                //     if (openingBalance.AccountId.HasValue) {
                //         await _reconciliationService.ReconcileAccountAsync(
                //             openingBalance.AccountId.Value,
                //             reconciliationTransactions,
                //             openingBalance.Amount,
                //             openingBalance.TransactionDate);
                //     }
                //
                //     if (openingBalance.ToAccountId.HasValue) {
                //         await _reconciliationService.ReconcileAccountAsync(
                //             openingBalance.ToAccountId.Value,
                //             reconciliationTransactions,
                //             openingBalance.Amount,
                //             openingBalance.TransactionDate);
                //     }
                //
                // }
            }

            Accounts.Add(account);

            ActiveAccountsWithNone.Add(JsonConvert.DeserializeObject<Account>(JsonConvert.SerializeObject(account)) ??
                                       throw new InvalidOperationException());
            
            // Reset for next
            EditingAccount = new Account {
                Name = "",
                Type = AccountType.Checking,
                Balance = 0,
                BalanceAsOf = DateTime.Today,
                IncludeInTotal = true,
                IsPrimary = !Accounts.Any(a => (_editingAccount.Type== AccountType.Checking || _editingAccount.Type== AccountType.Savings) && a.IsPrimary),
                HexColor = "#FF808080"
            };

            OnPropertyChanged(nameof(IsValid));
        }
        catch (Exception ex) {
            Log.Error(ex, "Error adding account");
        }
    }

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
            window.ShowDialog();
        }
        catch (Exception ex) {
            Log.Error(ex, "Error showing APR history window.");
            MessageBox.Show("Failed to open interest rate window. See log for details.", "Error", MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}