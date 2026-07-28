using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using StayOnTarget.Models;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using StayOnTarget.Services;

namespace StayOnTarget.ViewModels;

public class NewTransactionViewModel : ViewModelBase {
    private readonly BudgetService _budgetService;
    private Account _account;
    private readonly Action<NewTransactionViewModel, bool> _closeCallback;

    private ImportedTransactionViewModel? _selectedImported;

    public ImportedTransactionViewModel? SelectedImported {
        get => _selectedImported;
        set { SetProperty(ref _selectedImported, value); }
    }

    public NewTransactionViewModel(Account account, BudgetService budgetService,
        ImportedTransactionViewModel selectedImported, Action<NewTransactionViewModel, bool> closeCallback) {

        
        _account = account;
        _budgetService = budgetService;
        _closeCallback = closeCallback;
        
        CancelNewTransactionCommand = new RelayCommand(OnCancel);
        SaveNewTransactionCommand =
            new AsyncRelayCommand(OnSave, () => EditingTransactionClone != null);
        
        // 1. Validate data state
        bool isValid = 
                       !string.IsNullOrWhiteSpace(selectedImported.Payee) && 
                       selectedImported.Date != null && 
                       !string.IsNullOrWhiteSpace(selectedImported.BankId);

        if (!isValid)
        {
            // Queue the message box and close action onto the WPF Dispatcher AFTER constructor & render finishes
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() => 
            {
                MessageBox.Show("The transaction lacks required fields of payee, transaction date, and a bank transaction id.");
                _closeCallback?.Invoke(this, false); // Safely close the window!
            }, System.Windows.Threading.DispatcherPriority.Loaded);

            return; // Abort remaining initialization
        }
        
        EditingTransactionClone = new Transaction {
            Description = selectedImported.Payee??"",
            Memo = "",
            Amount = Math.Abs(selectedImported.Amount),
            TransactionDate = selectedImported.Date!.Value,
            FitId = selectedImported.BankId??"",
            AccountId = selectedImported.Amount > 0 ? null : _account.Id,
            AccountName = selectedImported.Amount > 0 ? null : _account.Name,
            ToAccountId = selectedImported.Amount > 0 ? _account.Id : null,
            ToAccountName = selectedImported.Amount > 0 ? _account.Name : null
        };

        _ =  LoadPaychecksAsync();

        Loaded = true;
    }

    private bool _loaded;

    public bool Loaded {
        get => _loaded;
        set => SetProperty(ref _loaded, value);
    }

    public IRelayCommand CancelNewTransactionCommand { get; }
    public IAsyncRelayCommand SaveNewTransactionCommand { get; }

    private ObservableCollection<Account> _accounts = new();

    private ObservableCollection<Paycheck> _paychecks = new();
    
    private ObservableCollection<Paycheck> _periodPayChecks = new();

    private ObservableCollection<BudgetBucket> _buckets = new();
    private DateTime _currentPeriodDate = DateTime.MinValue;

    private ObservableCollection<Account> _accountsWithNone = new();

    public ObservableCollection<Account> AccountsWithNone {
        get => _accountsWithNone;
        set => SetProperty(ref _accountsWithNone, value);
    }

    private ObservableCollection<Bill> _billsWithNone = new();

    public ObservableCollection<Bill> BillsWithNone {
        get => _billsWithNone;
        set => SetProperty(ref _billsWithNone, value);
    }

    private ObservableCollection<BudgetBucket> _bucketsWithNone = new();

    public ObservableCollection<BudgetBucket> BucketsWithNone {
        get => _bucketsWithNone;
        set => SetProperty(ref _bucketsWithNone, value);
    }

    private ObservableCollection<Bill> _bills = new();

    public ObservableCollection<Bill> Bills {
        get => _bills;
        set => SetProperty(ref _bills, value);
    }

    private Transaction? _editingTransactionClone;

    public Transaction? EditingTransactionClone {
        get => _editingTransactionClone;
        set {
            if (SetProperty(ref _editingTransactionClone, value)) {
                SaveNewTransactionCommand.NotifyCanExecuteChanged();
            }
        }
    }

    private ObservableCollection<PeriodBill> _currentPeriodBills = new();

    public ObservableCollection<PeriodBill> CurrentPeriodBills {
        get => _currentPeriodBills;
        set => SetProperty(ref _currentPeriodBills, value);
    }

    private ObservableCollection<PeriodBucket> _currentPeriodBuckets = new();

    public ObservableCollection<PeriodBucket> CurrentPeriodBuckets {
        get => _currentPeriodBuckets;
        set => SetProperty(ref _currentPeriodBuckets, value);
    }

    public DateTime CurrentPeriodDate {
        get => _currentPeriodDate;
        set {
            if (SetProperty(ref _currentPeriodDate, value)) {
                OnCurrentPeriodDateChanged();
            }
        }
    }

    private async void OnCurrentPeriodDateChanged()
    {
        try 
        {
            await LoadPeriodDataAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load period data for date {Date}", _currentPeriodDate);
        }
    }
    
    private ObservableCollection<Transaction> _currentPeriodTransactions = new();

    public ObservableCollection<Transaction> CurrentPeriodTransactions {
        get => _currentPeriodTransactions;
        set => SetProperty(ref _currentPeriodTransactions, value);
    }

    private ObservableCollection<Paycheck> _paychecksWithNone = new();

    public ObservableCollection<Paycheck> PaychecksWithNone {
        get => _paychecksWithNone;
        set => SetProperty(ref _paychecksWithNone, value);
    }

    private async Task LoadPeriodDataAsync() {
        try {
            var accounts = (await _budgetService.GetAllAccountsAsync()).ToList();
            if (accounts.All(a => a.Name != "Household Cash" && a.Type != AccountType.Cash)) {
                var cashAccount = new Account {
                    Name = "Household Cash",
                    Type = AccountType.Cash,
                    Balance = 0,
                    IncludeInTotal = true
                };
                await _budgetService.UpsertAccountAsync(cashAccount);
                accounts = (await _budgetService.GetAllAccountsAsync()).ToList();
            }

            accounts = accounts.OrderBy(b => b.Name).ToList();

            var accountsWithNone = new List<Account> { new Account { Id = 0, Name = "(None)" } };
            accountsWithNone.AddRange(accounts);
            AccountsWithNone = new ObservableCollection<Account>(accountsWithNone);

            var bills = await _budgetService.GetAllBillsAsync();
            bills = bills.OrderBy(b => b.DueDay).ThenBy(b => b.Name).ToList();

            var billsWithNone = new List<Bill> { new Bill { Id = 0, Name = "(None)" } };
            billsWithNone.AddRange(bills);
            BillsWithNone = new ObservableCollection<Bill>(billsWithNone);

            var paychecks = await _budgetService.GetAllPaychecksAsync();
            paychecks = paychecks.OrderBy(b => b.Name).ToList();

            var paychecksWithNone = new List<Paycheck> { new Paycheck { Id = 0, Name = "(None)" } };
            paychecksWithNone.AddRange(paychecks);
            PaychecksWithNone = new ObservableCollection<Paycheck>(paychecksWithNone);

            var buckets = await _budgetService.GetAllBucketsAsync();
            buckets = buckets.OrderBy(b => b.Name).ToList();

            var bucketsWithNone = new List<BudgetBucket> { new BudgetBucket { Id = 0, Name = "(None)" } };
            bucketsWithNone.AddRange(buckets);
            BucketsWithNone = new ObservableCollection<BudgetBucket>(bucketsWithNone);
        }
        catch (Exception ex) {
            Debug.WriteLine("Failure while loading data: " + ex.Message);
        }

        await LoadPeriodBillsAsync();
        await LoadPeriodBucketsAsync();
        await LoadPeriodTransactionsAsync();
    }

    private async Task LoadPeriodBillsAsync() {
        var pBills = (await _budgetService.GetPeriodBillsAsync(CurrentPeriodDate)).ToList();
        pBills = pBills.OrderBy(pb => pb.DueDate).ToList();

        CurrentPeriodBills = new ObservableCollection<PeriodBill>(pBills);
        OnPropertyChanged(nameof(CurrentPeriodBills));
        
        
    }

    private async Task LoadPeriodBucketsAsync() {
        var pBuckets = (await _budgetService.GetPeriodBucketsIncludingMonthlyAsync(CurrentPeriodDate)).ToList();
        CurrentPeriodBuckets = new ObservableCollection<PeriodBucket>(pBuckets);
        OnPropertyChanged(nameof(CurrentPeriodBuckets));
        
        
    }

    private DateTime GetNextPeriodDate(DateTime currentPeriodStart) {
        var allPaycheckDates = new List<DateTime>();
        var end = currentPeriodStart.AddYears(1);
        foreach (var pay in Paychecks.Where(p => p.Id != 0)) {
            var nextPay = pay.StartDate;
            while (nextPay < end) {
                allPaycheckDates.Add(nextPay);
                nextPay = pay.Frequency switch {
                    Frequency.Weekly => nextPay.AddDays(7),
                    Frequency.BiWeekly => nextPay.AddDays(14),
                    Frequency.Monthly => nextPay.AddMonths(1),
                    _ => nextPay.AddYears(100)
                };
            }
        }

        var sortedDates = allPaycheckDates.Distinct().OrderBy(d => d).ToList();
        var nextDate = sortedDates.FirstOrDefault(d => d > currentPeriodStart);

        return nextDate == DateTime.MinValue ? currentPeriodStart.AddDays(14) : nextDate;
    }

    private async Task LoadPeriodTransactionsAsync() {
        var nextPeriodDate = GetNextPeriodDate(CurrentPeriodDate);
        var transactions = (await _budgetService.GetTransactionsAsync(CurrentPeriodDate, nextPeriodDate)).ToList();
        transactions = transactions.OrderBy(pb => pb.TransactionDate).ToList();
        CurrentPeriodTransactions = new ObservableCollection<Transaction>(transactions);
        OnPropertyChanged(nameof(CurrentPeriodTransactions));
        
        
    }

    
    public ObservableCollection<Paycheck> PeriodPaychecks {
        get => _periodPayChecks;
        set => SetProperty(ref _periodPayChecks, value);
    }
    
    public ObservableCollection<Paycheck> Paychecks {
        get => _paychecks;
        set => SetProperty(ref _paychecks, value);
    }

    private async Task LoadPaychecksAsync() {
        var paychecks = await _budgetService.GetAllPaychecksAsync();
        paychecks = paychecks.OrderBy(b => b.Name).ToList();
        Paychecks = new ObservableCollection<Paycheck>(paychecks);

        var allPaychecks = Paychecks.ToList();
        if (allPaychecks.Count == 0) {
            CurrentPeriodDate = DateTime.Today;
            return;
        }

        PeriodPaychecks = new ObservableCollection<Paycheck>(allPaychecks);

        var paychecksWithNone = new List<Paycheck> { new Paycheck { Id = 0, Name = "(None)" } };
        paychecksWithNone.AddRange(paychecks);
        PaychecksWithNone = new ObservableCollection<Paycheck>(paychecksWithNone);

        SetCurrentPeriodDate();
    }

    private void SetCurrentPeriodDate(int? id = null) {
        if (EditingTransactionClone == null) return;

        var allPaychecks = Paychecks.ToList();
        if (allPaychecks.Count == 0) {
            CurrentPeriodDate = DateTime.Today;
            return;
        }

        DateTime latestPayBeforeToday = DateTime.MinValue;
        foreach (var pay in allPaychecks.Where(p => id == null || p.Id == id)) {
            var nextPay = pay.StartDate;
            while (nextPay <= EditingTransactionClone.TransactionDate) {
                if (nextPay <= EditingTransactionClone.TransactionDate && nextPay > latestPayBeforeToday)
                    latestPayBeforeToday = nextPay;

                nextPay = pay.Frequency switch {
                    Frequency.Weekly => nextPay.AddDays(7),
                    Frequency.BiWeekly => nextPay.AddDays(14),
                    Frequency.Monthly => nextPay.AddMonths(1),
                    _ => nextPay.AddYears(100)
                };
            }
        }

        if (latestPayBeforeToday != DateTime.MinValue)
            CurrentPeriodDate = latestPayBeforeToday;
        else if (allPaychecks.Any())
            CurrentPeriodDate = allPaychecks.Min(p => p.StartDate);

        var currentPeriodPaychecks = new List<Paycheck>();
        foreach (var pay in allPaychecks.Where(p => id == null || p.Id == id)) {
            var nextPay = pay.StartDate;
            var found = false;
            while (nextPay <= CurrentPeriodDate) {
                if (nextPay.Date == CurrentPeriodDate.Date) {
                    found = true;
                    break;
                }

                nextPay = pay.Frequency switch {
                    Frequency.Weekly => nextPay.AddDays(7),
                    Frequency.BiWeekly => nextPay.AddDays(14),
                    Frequency.Monthly => nextPay.AddMonths(1),
                    _ => nextPay.AddYears(100)
                };
            }

            if (found) currentPeriodPaychecks.Add(pay);
        }
    }

    private async Task OnSave() {
        if (EditingTransactionClone == null) return;

        if (EditingTransactionClone.AccountId == 0) EditingTransactionClone.AccountId = null;
        if (EditingTransactionClone.ToAccountId == 0) EditingTransactionClone.ToAccountId = null;
        if (EditingTransactionClone.BillId == 0) EditingTransactionClone.BillId = null;
        if (EditingTransactionClone.BucketId == 0) EditingTransactionClone.BucketId = null;
        if (EditingTransactionClone.PaycheckId == 0) EditingTransactionClone.PaycheckId = null;

        await _budgetService.UpsertTransactionAsync(EditingTransactionClone);

        _closeCallback?.Invoke(this, true);
  
        EditingTransactionClone = null;
    }

    private void OnCancel() {
        EditingTransactionClone = null;
        // Tell the parent to close us, passing 'false' because they cancelled
        _closeCallback?.Invoke(this, false);
    }
}