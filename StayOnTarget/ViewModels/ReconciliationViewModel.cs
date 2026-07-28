using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json;
using Serilog;
using StayOnTarget.Models;
using StayOnTarget.Services;
using StayOnTarget.Views;

namespace StayOnTarget.ViewModels;

public class ReconciliationViewModel : ViewModelBase {
    private readonly BudgetService _budgetService;
    private readonly ReconciliationService _reconciliationService;
    private Account _account;
    
    public ReconciliationViewModel(Account account, BudgetService budgetService) {
        _account = account;
        _budgetService = budgetService;
        _reconciliationService = new ReconciliationService(_budgetService);
        CancelAdjustmentCommand = new RelayCommand(() => {
            CurrentAssetValue = EndingBalance;
            AdjustmentTransactionAmount = null;
            OnPropertyChanged(nameof(AdjustmentTransactionAmount));
            AdjustmentTransactionMemo = "Balance adjustment transaction";
            OnPropertyChanged(nameof(AdjustmentTransactionMemo));
            IsBalanceAdjustmentVisible = false;
            OnPropertyChanged(nameof(IsBalanceAdjustmentVisible));
            
            
        });
        ShowAdjustmentCommand = new RelayCommand(() => IsBalanceAdjustmentVisible = true, () => CanShowAdjustBalance);

        AdjustBalanceCommand = new AsyncRelayCommand(AdjustBalanceAsync, () => CanExecuteAdjustBalance);
        
        InitializeDataCommand  = new AsyncRelayCommand( LoadDataAsync); 
    }
    
    public IAsyncRelayCommand InitializeDataCommand { get; }
    
    private ObservableCollection<ReconciliationTransaction> _reconciliationTransactions = new();

    public ObservableCollection<ReconciliationTransaction> ReconciliationTransactions {
        get => _reconciliationTransactions;
        set => SetProperty(ref _reconciliationTransactions, value);
    }

    public Account Account {
        get => _account;
        set => SetProperty(ref _account, value);
    }

    private decimal _beginningBalance;

    public decimal BeginningBalance {
        get => _beginningBalance;
        set => SetProperty(ref _beginningBalance, value);
    }
    
    private decimal _endingBalance;

    public decimal EndingBalance {
        get => _endingBalance;
        set => SetProperty(ref _endingBalance, value);
    }

    private decimal _currentAssetValue;

    public decimal CurrentAssetValue {
        get => _currentAssetValue;
        set {
            SetProperty(ref _currentAssetValue, value);
            CalculateAdjustmentTransactionAmount();
        }
    }

    private DateTime? _lastReconciledDate;

    public DateTime? LastReconciledDate {
        get => _lastReconciledDate;
        set => SetProperty(ref _lastReconciledDate, value);
    }
    
    private decimal? _newReconciledBalance = 0;

    public decimal? NewReconciledBalance {
        get => _newReconciledBalance;
        set => SetProperty(ref _newReconciledBalance, value);
    }

    private decimal? _adjustmentTransactionAmount = 0;

    public decimal? AdjustmentTransactionAmount {
        get => _adjustmentTransactionAmount;
        set {
            if (SetProperty(ref _adjustmentTransactionAmount, value)) {
                OnPropertyChanged(nameof(AdjustmentTransactionDescription));
                OnPropertyChanged(nameof(CanExecuteAdjustBalance));
                AdjustBalanceCommand.NotifyCanExecuteChanged();
            }
        }
    }

    private string? _adjustmentTransactionMemo = "Balance adjustment transaction";

    public string? AdjustmentTransactionMemo {
        get => _adjustmentTransactionMemo;
        set => SetProperty(ref _adjustmentTransactionMemo, value);
    }

    public string AdjustmentTransactionDescription => AdjustmentTransactionAmount.HasValue ?  AdjustmentTransactionAmount.Value > 0 ? "[Value Increase]" : "[Value Decrease]" : "";

    private DateTime? _newReconciledDate;

    public DateTime? NewReconciledDate {
        get => _newReconciledDate;
        set => SetProperty(ref _newReconciledDate, value);
    }
    
    
    private bool _isBalanceAdjustmentVisible;

    public bool IsBalanceAdjustmentVisible {
        get => _isBalanceAdjustmentVisible;
        set {
            if (SetProperty(ref _isBalanceAdjustmentVisible, value)) {
                OnPropertyChanged(nameof(CanShowAdjustBalance));
                ShowAdjustmentCommand.NotifyCanExecuteChanged();
            }
        }
    }

    
    public bool CanExecuteAdjustBalance => AdjustmentTransactionAmount != null;
    public bool CanShowAdjustBalance => IsBalanceAdjustmentVisible == false;
    public IAsyncRelayCommand AdjustBalanceCommand { get; }

    public IRelayCommand CancelAdjustmentCommand { get; private set; }
    public IRelayCommand ShowAdjustmentCommand { get; private set; }
    
    public  async Task ImportAccount() {
        var window = new ImportReconciliationWindow(_account, _budgetService) {
            Owner = Application.Current.MainWindow
        };
        window.ShowDialog();
        await LoadDataAsync();
    }
    
    private async Task AdjustBalanceAsync() {
        decimal currentRunningBalance = BeginningBalance + ReconciliationTransactions.Sum(t => t.Amount);
        decimal delta = CurrentAssetValue - currentRunningBalance;

        if (delta == 0) return;

        var adjustmentTransaction = new Transaction {
            AccountId = delta > 0 ? null : _account.Id,
            ToAccountId = delta > 0 ? _account.Id : null,
            TransactionDate = DateTime.Today,
            Description = delta > 0 ? "Value Increase" : "Value Decrease",
            Memo = AdjustmentTransactionMemo,
            Amount = Math.Abs(delta)
        };

        await _budgetService.UpsertTransactionAsync(adjustmentTransaction);
        CurrentAssetValue = 0; // Reset so it gets recalculated in LoadData
        IsBalanceAdjustmentVisible = false;
        OnPropertyChanged(nameof(CanExecuteAdjustBalance));
        
        
        await LoadDataAsync();
    }
    
    private void CalculateAdjustmentTransactionAmount() {
        decimal currentRunningBalance = BeginningBalance + ReconciliationTransactions.Sum(t => t.Amount);
        decimal delta = CurrentAssetValue - currentRunningBalance;
        
        if (delta == 0) {
            AdjustmentTransactionAmount = null;
        }
        else {
            AdjustmentTransactionAmount = delta;
        }

        OnPropertyChanged(nameof(AdjustmentTransactionAmount));
        OnPropertyChanged(nameof(CanExecuteAdjustBalance));
        
        // Force WPF to evaluate CanExecute immediately
        CommandManager.InvalidateRequerySuggested();
    }

    private async Task LoadDataAsync() {
        try{

        decimal beginningBalance = _account.Balance;
        DateTime? lastReconciledDate = _account.BalanceAsOf;
        var accountReconciliation = await _budgetService.GetLatestValidReconciliationAsync(_account.Id);

        if (accountReconciliation != null) {
            beginningBalance = accountReconciliation.ReconciledBalance;
            lastReconciledDate = accountReconciliation.ReconciledAsOfDate;
        }

        var transactions = await _budgetService.GetAllUnreconciledTransactionsSinceLastReconciliationAsync(_account.Id);
        transactions = transactions.OrderBy(b => b.TransactionDate).ToList();
        string json = JsonConvert.SerializeObject(transactions.ToList());
        var reconciliationTransactions = JsonConvert.DeserializeObject<List<ReconciliationTransaction>>(json);
        (EndingBalance, lastReconciledDate, beginningBalance)  = await _reconciliationService.CalculateRunningBalanceAsync(_account.Id, reconciliationTransactions!);
        BeginningBalance = beginningBalance;
        LastReconciledDate = lastReconciledDate ?? DateTime.MinValue;
        if (CurrentAssetValue == 0) CurrentAssetValue = EndingBalance;

        decimal? newReconciledBalance = null;
        DateTime? newReconciledDate = null;
        bool hasNewReconciled = false;
        bool notReconciledAfterLastReconciled = true;
        foreach (var t in reconciliationTransactions!.OrderBy(b => b.TransactionDate)) {
            if (t.IsReconciled) {
                if (!notReconciledAfterLastReconciled) {
                    newReconciledBalance = t.Amount;
                    newReconciledDate = t.TransactionDate;
                    hasNewReconciled = true;
                }
            }
            else {
                if (hasNewReconciled) {
                    notReconciledAfterLastReconciled = true;
                }
            }
        }

        NewReconciledBalance = newReconciledBalance;
        NewReconciledDate = newReconciledDate;
        ReconciliationTransactions = new ObservableCollection<ReconciliationTransaction>(reconciliationTransactions!);
        }
        catch (Exception ex)
        {
            // Must catch exceptions locally to prevent app crash!
            Log.Error("ReconciliationViewModel failed to load data: " + ex.Message);
        }
        // finally
        // {
        //     IsLoading = false;
        // }
    }

    public async Task UpdateReconciliationTransactionsAsync() {
        decimal? newReconciledBalance = null;
        DateTime? newReconciledDate = null;
        bool hasNewReconciled = false;
        bool notReconciledAfterLastReconciled = false;
        bool changed = false;
        foreach (var t in ReconciliationTransactions.OrderBy(b => b.TransactionDate)) {
            if (t.IsReconciled) {
                if (!notReconciledAfterLastReconciled) {
                    hasNewReconciled = true;
                }
                else {
                    if (t.IsReconciled) {
                        t.IsReconciled = false;
                        changed = true;
                    }
                }
            }
            else {
                if (hasNewReconciled) {
                    notReconciledAfterLastReconciled = true;
                }
            }
        }

        foreach (var t in ReconciliationTransactions.OrderBy(b => b.TransactionDate)) {
            if (t.IsReconciled) {
                newReconciledBalance = t.RunningBalance;
                newReconciledDate = t.TransactionDate;
            }
        }

        if (changed) {
            OnPropertyChanged(nameof(ReconciliationTransactions));
            
            
        }

        if ((NewReconciledBalance.HasValue || newReconciledBalance.HasValue) && (NewReconciledDate.HasValue || newReconciledDate.HasValue)) {
            await _reconciliationService.ReconcileAccountAsync(
                _account.Id,
                ReconciliationTransactions,
                NewReconciledBalance ?? newReconciledBalance ?? 0,
                NewReconciledDate ?? newReconciledDate ?? DateTime.MinValue);
        }
    }

    public void Reconcile() {
        decimal? newReconciledBalance = null;
        DateTime? newReconciledDate = null;
        bool hasNewReconciled = false;
        bool notReconciledAfterLastReconciled = false;
        foreach (var t in ReconciliationTransactions.OrderBy(b => b.TransactionDate)) {
            if (t.IsReconciled) {
                if (!notReconciledAfterLastReconciled) {
                    newReconciledBalance = t.RunningBalance;
                    newReconciledDate = t.TransactionDate;
                    hasNewReconciled = true;
                }
            }
            else {
                if (hasNewReconciled) {
                    notReconciledAfterLastReconciled = true;
                }
            }
        }

        NewReconciledBalance = newReconciledBalance;
        NewReconciledDate = newReconciledDate;
    }
}