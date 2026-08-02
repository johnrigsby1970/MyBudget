using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using StayOnTarget.Models;
using StayOnTarget.Services;

namespace StayOnTarget.ViewModels;

public class AccountAprHistoryViewModel: ViewModelBase {
    private readonly BudgetService _budgetService;
    private readonly Account _account;
    
    public AccountAprHistoryViewModel(Account account, BudgetService budgetService) {
        _account = account;
        _budgetService = budgetService;
    AddCommand = new RelayCommand(Add);
    RemoveCommand = new AsyncRelayCommand<AccountAprHistory>(RemoveAsync);
        
        InitializeDataCommand = new AsyncRelayCommand(LoadDataAsync);
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
            SetProperty(ref _selectedItem, value);
        }
    }
    
    private ObservableCollection<AccountAprHistory> _accountAprHistories = new();
    public ObservableCollection<AccountAprHistory> AccountAprHistories {
        get => _accountAprHistories;
        set => SetProperty(ref _accountAprHistories, value);
    }
    
    public IRelayCommand AddCommand { get; }
    public IAsyncRelayCommand RemoveCommand { get; }
    
    private void Add()
    {
        // A new row is added to the DataGrid automatically
        if (AccountAprHistories.Count == 0) {
            AccountAprHistories.Add(new AccountAprHistory() { AccountId = _account.Id, AsOfDate=DateTime.MinValue, AnnualPercentageRate = 0, CashAdvanceRate = 0, BalanceTransferRate = 0 });
        }
        else {
            if(AccountAprHistories.Any(a => a.AsOfDate ==new DateTime(DateTime.Today.Year, DateTime.Today.Month, DateTime.Today.Day))) return;
            var latestRecords = AccountAprHistories.Last(a => a.AsOfDate == AccountAprHistories.Max(b => b.AsOfDate));
            AccountAprHistories.Add(new AccountAprHistory() { AccountId = _account.Id, AsOfDate=new DateTime(DateTime.Today.Year, DateTime.Today.Month, DateTime.Today.Day), AnnualPercentageRate = latestRecords.AnnualPercentageRate, CashAdvanceRate = latestRecords.CashAdvanceRate, BalanceTransferRate = latestRecords.BalanceTransferRate });
        }
        OnPropertyChanged(nameof(AccountAprHistories));
    }
    
    private async Task RemoveAsync(AccountAprHistory? aah)
    {
        if (aah is { Id: > 0 }) {
            MessageBoxResult messageBoxResult = MessageBox.Show(
                "Are you sure you want to delete the interest rate record?", // Message
                "Delete Confirmation", // Title
                MessageBoxButton.YesNo, // Buttons
                MessageBoxImage.Warning // Icon
            );
        
            // Check the user's response
            if (messageBoxResult == MessageBoxResult.Yes) {
                // User confirmed deletion, proceed with your delete logic here
                await _budgetService.DeleteAccountAprHistoryAsync(aah.Id);
            }
        }
        
        // A new row is added to the DataGrid automatically
        if (AccountAprHistories.Count == 0) {
            AccountAprHistories.Add(new AccountAprHistory() { AccountId = _account.Id,AsOfDate=DateTime.MinValue, AnnualPercentageRate = 0, CashAdvanceRate = 0, BalanceTransferRate = 0 });
        }
        else {
            var latestRecords = AccountAprHistories.Last(a => a.AsOfDate == AccountAprHistories.Max(b => b.AsOfDate));
            AccountAprHistories.Add(new AccountAprHistory() { AccountId = _account.Id, AsOfDate=DateTime.Now, AnnualPercentageRate = latestRecords.AnnualPercentageRate, CashAdvanceRate = latestRecords.CashAdvanceRate, BalanceTransferRate = latestRecords.BalanceTransferRate });
        }
    }
    
    private async Task LoadDataAsync() {
        // Force the dispatcher to render the empty screen/loading state first
        IsLoading = true;
        await Task.Yield();

        try {
            if (_account.Id > 0) {
                var histories = await _budgetService.GetAccountAprHistoriesAsync(_account.Id);
                histories = histories.OrderBy(b => b.AsOfDate).ToList();

                AccountAprHistories = new ObservableCollection<AccountAprHistory>(histories);
            }
            else {
                //editing a pending account
                if (_account.AccountAprHistory != null) {
                    AccountAprHistories = new ObservableCollection<AccountAprHistory>(_account.AccountAprHistory);
                }
            }
        }
        finally {
            IsLoading = false;
        }
    }
    
    public async Task UpdateAccountAprHistoriesAsync() {
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
}