using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using StayOnTarget.Models;
using StayOnTarget.Services;
using Serilog;

namespace StayOnTarget.ViewModels;

public class AccountAprHistoryViewModel: ViewModelBase {
    private readonly BudgetService _budgetService;
    private readonly Account _account;
    
    public AccountAprHistoryViewModel(Account account, BudgetService budgetService) {
        try {
            _account = account;
            _budgetService = budgetService;
            AddCommand = new RelayCommand(Add);
            RemoveCommand = new AsyncRelayCommand<AccountAprHistory>(RemoveAsync);
                
            InitializeDataCommand = new AsyncRelayCommand(LoadDataAsync);
        }
        catch (Exception ex) {
            Log.Fatal(ex, "Critical error initializing AccountAprHistoryViewModel[cite: 20].");
            
        }
    }   
    
    public IAsyncRelayCommand InitializeDataCommand { get; }
    
    private bool _isLoading;

    public bool IsLoading {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }
    
    private AccountAprHistory? _selectedItem;
    public AccountAprHistory? SelectedItem {
        get => _selectedItem;
        set {
            try {
                SetProperty(ref _selectedItem, value);
            }
            catch (Exception ex) {
                Log.Error(ex, "Error setting SelectedItem in AccountAprHistoryViewModel[cite: 20].");
                
            }
        }
    }
    
    private ObservableCollection<AccountAprHistory> _accountAprHistories = new();
    public ObservableCollection<AccountAprHistory> AccountAprHistories {
        get => _accountAprHistories;
        set {
            try {
                SetProperty(ref _accountAprHistories, value);
            }
            catch (Exception ex) {
                Log.Error(ex, "Error setting AccountAprHistories collection in AccountAprHistoryViewModel[cite: 20].");
                
            }
        }
    }
    
    public IRelayCommand AddCommand { get; }
    public IAsyncRelayCommand RemoveCommand { get; }
    
    private void Add()
    {
        try {
            if (AccountAprHistories.Count == 0) {
                AccountAprHistories.Add(new AccountAprHistory() { AccountId = _account.Id, AsOfDate=DateTime.MinValue, AnnualPercentageRate = 0, CashAdvanceRate = 0, BalanceTransferRate = 0 });
            }
            else {
                if(AccountAprHistories.Any(a => a.AsOfDate == new DateTime(DateTime.Today.Year, DateTime.Today.Month, DateTime.Today.Day))) return;
                var latestRecords = AccountAprHistories.Last(a => a.AsOfDate == AccountAprHistories.Max(b => b.AsOfDate));
                AccountAprHistories.Add(new AccountAprHistory() { AccountId = _account.Id, AsOfDate=new DateTime(DateTime.Today.Year, DateTime.Today.Month, DateTime.Today.Day), AnnualPercentageRate = latestRecords.AnnualPercentageRate, CashAdvanceRate = latestRecords.CashAdvanceRate, BalanceTransferRate = latestRecords.BalanceTransferRate });
            }
            OnPropertyChanged(nameof(AccountAprHistories));
        }
        catch (Exception ex) {
            Log.Error(ex, "Error adding APR history record[cite: 20].");
            
        }
    }

    private async Task RemoveAsync(AccountAprHistory? aah) {
        try {
            if (AccountAprHistories.Count == 1) {
                MessageBoxResult messageBoxResult = MessageBox.Show(
                    $"You need at least one interest rate record for an account of this type. Instead of deleting it, change its properties.",
                    "Delete Cancelled",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                return;
            }
            if (aah is { Id: > 0 }) {
                MessageBoxResult messageBoxResult = MessageBox.Show(
                    $"Are you sure you want to delete the {aah.AnnualPercentageRate:P2} interest rate effective {aah.AsOfDate:d}?\n\n" +
                    "Deleting this rate may change interest calculations and projections associated with this account.",
                    "Confirm Delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning
                );
            
                if (messageBoxResult == MessageBoxResult.Yes) {
                    await _budgetService.DeleteAccountAprHistoryAsync(aah.Id);
                }
            }
            
            if (AccountAprHistories.Count == 0) {
                AccountAprHistories.Add(new AccountAprHistory() { AccountId = _account.Id, AsOfDate=DateTime.MinValue, AnnualPercentageRate = 0, CashAdvanceRate = 0, BalanceTransferRate = 0 });
            }
            else {
                var latestRecords = AccountAprHistories.Last(a => a.AsOfDate == AccountAprHistories.Max(b => b.AsOfDate));
                AccountAprHistories.Add(new AccountAprHistory() { AccountId = _account.Id, AsOfDate=DateTime.Now, AnnualPercentageRate = latestRecords.AnnualPercentageRate, CashAdvanceRate = latestRecords.CashAdvanceRate, BalanceTransferRate = latestRecords.BalanceTransferRate });
            }
        }
        catch (Exception ex) {
            Log.Error(ex, "Error removing APR history record[cite: 20].");
            
        }
    }
    
    private async Task LoadDataAsync() {
        IsLoading = true;
        await Task.Yield();

        try {
            if (_account.Id > 0) {
                var histories = await _budgetService.GetAccountAprHistoriesAsync(_account.Id);
                histories = histories.OrderBy(b => b.AsOfDate).ToList();

                AccountAprHistories = new ObservableCollection<AccountAprHistory>(histories);
            }
            else {
                if (_account.AccountAprHistory != null) {
                    AccountAprHistories = new ObservableCollection<AccountAprHistory>(_account.AccountAprHistory);
                }
            }
        }
        catch (Exception ex) {
            Log.Error(ex, "Error loading account APR histories[cite: 20].");
            
        }
        finally {
            IsLoading = false;
        }
    }
    
    public async Task UpdateAccountAprHistoriesAsync() {
        try {
            if (_account.Id > 0) {
                foreach (var aah in AccountAprHistories) {
                    if(aah.AccountId==0) aah.AccountId = _account.Id;
                    await _budgetService.UpsertAccountAprHistoryAsync(aah);
                }
                
                var histories = await _budgetService.GetAccountAprHistoriesAsync(_account.Id);
                histories = histories.OrderBy(b => b.AsOfDate).ToList();
                _account.AccountAprHistory = histories.ToList();
                AccountAprHistories = new ObservableCollection<AccountAprHistory>(histories);
            }
            else {
                _account.AccountAprHistory = AccountAprHistories.ToList();
            }
        }
        catch (Exception ex) {
            Log.Error(ex, "Error updating account APR histories[cite: 20].");
            
        }
    }
}