using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json;
using Serilog;
using StayOnTarget.Models;
using StayOnTarget.Services;
using StayOnTarget.Views;

namespace StayOnTarget.ViewModels;

public partial class ReconciliationViewModel : ViewModelBase {
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
        CorrectOpeningBalanceCommand =
            new AsyncRelayCommand(CorrectOpeningBalanceAsync, () => CanExecuteCorrectOpeningBalance);
        CancelCorrectOpeningBalanceCommand = new AsyncRelayCommand(CancelCorrectOpeningBalanceAsync);

        InitializeDataCommand = new AsyncRelayCommand(LoadDataAsync);

        ToggleSelectionCommand = new RelayCommand(() => {
            bool allSelected = ReconciliationTransactions.All(x => x.IsCleared);
            bool allUnselected = ReconciliationTransactions.All(x => !x.IsCleared);

            if (allSelected) {
                foreach (var t in ReconciliationTransactions) t.IsCleared = false;
            }
            else if (allUnselected) {
                foreach (var t in ReconciliationTransactions) t.IsCleared = true;
            }
            else {
                foreach (var t in ReconciliationTransactions) {
                    if (!t.IsCleared) t.IsCleared = true;
                }
            }

            Reconcile();
        });
    }

    private bool? _isAllSelected;
    public bool? IsAllSelected
    {
        get => _isAllSelected;
        set
        {
            if (_isAllSelected != value)
            {
                _isAllSelected = value;
                OnPropertyChanged(nameof(IsAllSelected));

                // If user explicitly clicked header (value will be true or false, not null)
                if (value.HasValue)
                {
                    SelectAllRows(value.Value);
                }
            }
        }
    }
    
    private void UpdateIsAllSelectedState()
    {
        if (ReconciliationTransactions == null || !ReconciliationTransactions.Any())
        {
            _isAllSelected = false;
        }
        else if (ReconciliationTransactions.All(x => x.IsCleared))
        {
            _isAllSelected = true; // All checked -> Header checked
        }
        else if (ReconciliationTransactions.All(x => !x.IsCleared))
        {
            _isAllSelected = false; // None checked -> Header unchecked
        }
        else
        {
            _isAllSelected = null; // Mix -> Header indeterminate (dash)
        }

        // Raise property change without re-triggering SelectAllRows loop
        OnPropertyChanged(nameof(IsAllSelected));
    }

    private void SelectAllRows(bool select)
    {
        foreach (var item in ReconciliationTransactions)
        {
            // Temporarily unhook event listener to prevent infinite loops during bulk check
            item.PropertyChanged -= Item_PropertyChanged;
            item.IsCleared = select;
            item.PropertyChanged += Item_PropertyChanged;
        }
    }
    
    private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TransactionViewModel.IsCleared))
        {
            // Re-evaluate header state whenever a single row checkbox toggles!
            UpdateIsAllSelectedState();
        }
    }
    
    public IAsyncRelayCommand InitializeDataCommand { get; }

    [ObservableProperty] private bool _canReconcile = true;

    [ObservableProperty] private string _blockingWarningMessage = string.Empty;

    private ObservableCollection<TransactionViewModel> _reconciliationTransactions = new();

    public ObservableCollection<TransactionViewModel> ReconciliationTransactions {
        get => _reconciliationTransactions;
        set => SetProperty(ref _reconciliationTransactions, value);
    }

    public Account Account {
        get => _account;
        set => SetProperty(ref _account, value);
    }

    private string _spinnerMessage = "Loading...";

    public string SpinnerMessage {
        get => _spinnerMessage;
        set => SetProperty(ref _spinnerMessage, value);
    }

    private bool _isOpeningBalanceCreated;

    public bool IsOpeningBalanceCreated {
        get => _isOpeningBalanceCreated;
        set => SetProperty(ref _isOpeningBalanceCreated, value);
    }

    private bool _isBusy;

    public bool IsBusy {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    private decimal _beginningBalance;

    public decimal BeginningBalance {
        get => _beginningBalance;
        set => SetProperty(ref _beginningBalance, value);
    }

    private decimal _endingBalance;

    public decimal EndingBalance {
        get => _endingBalance;

        set {
            if (SetProperty(ref _endingBalance, value)) {
                OnPropertyChanged(nameof(ReconcileButtonText));
                OnPropertyChanged(nameof(CanExecuteReconcile));
                OnPropertyChanged(nameof(Difference));
            }
        }
    }

    private decimal? _openingBalance;

    public decimal? OpeningBalance {
        get => _openingBalance;
        set {
            if (SetProperty(ref _openingBalance, value)) {
                OnPropertyChanged(nameof(CanExecuteCorrectOpeningBalance));
                CorrectOpeningBalanceCommand.NotifyCanExecuteChanged();
            }
        }
    }

    private DateTime? _openingBalanceAsOf;

    public DateTime? OpeningBalanceAsOf {
        get => _openingBalanceAsOf;
        set {
            if (SetProperty(ref _openingBalanceAsOf, value)) {
                OnPropertyChanged(nameof(CanExecuteCorrectOpeningBalance));
                CorrectOpeningBalanceCommand.NotifyCanExecuteChanged();
            }
        }
    }


    private DateTime? _openingBalanceMaximumAsOf;

    public DateTime? OpeningBalanceMaximumAsOf {
        get => _openingBalanceMaximumAsOf;
        set => SetProperty(ref _openingBalanceMaximumAsOf, value);
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
        set {
            if (SetProperty(ref _newReconciledBalance, value)) {
                OnPropertyChanged(nameof(CanExecuteReconcile));
                OnPropertyChanged(nameof(ReconcileButtonText));
                OnPropertyChanged(nameof(Difference));
            }
        }
    }

    private DateTime? _newReconciledDate;

    public DateTime? NewReconciledDate {
        get => _newReconciledDate;
        set {
            if (SetProperty(ref _newReconciledDate, value)) {
                OnPropertyChanged(nameof(CanExecuteReconcile));
                OnPropertyChanged(nameof(ReconcileButtonText));
            }
        }
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

    public string AdjustmentTransactionDescription => AdjustmentTransactionAmount.HasValue
        ? AdjustmentTransactionAmount.Value > 0 ? "[Value Increase]" : "[Value Decrease]"
        : "";

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

    private bool _isOpeningBalanceCorrectionVisible;

    public bool IsOpeningBalanceCorrectionVisible {
        get => _isOpeningBalanceCorrectionVisible;
        set {
            if (SetProperty(ref _isOpeningBalanceCorrectionVisible, value)) {
                OnPropertyChanged(nameof(CanShowOpeningBalanceCorrection));
            }
        }
    }

    public decimal Difference => !NewReconciledBalance.HasValue ? 0 : EndingBalance - NewReconciledBalance.Value;

    public bool CanExecuteAdjustBalance => AdjustmentTransactionAmount != null;

    public bool CanExecuteReconcile =>
        true; //ReconciliationTransactions.Any();// && NewReconciledBalance != null && NewReconciledDate != null &&
    // NewReconciledDate >= OpeningBalanceAsOf;


    public bool CanExecuteCorrectOpeningBalance => OpeningBalance != null && OpeningBalanceAsOf != null &&
                                                   OpeningBalanceAsOf <= OpeningBalanceMaximumAsOf;

    public bool CanShowAdjustBalance => IsBalanceAdjustmentVisible == false;
    public bool CanShowOpeningBalanceCorrection => IsOpeningBalanceCorrectionVisible == false;

    public IAsyncRelayCommand AdjustBalanceCommand { get; }

    public IRelayCommand CancelAdjustmentCommand { get; private set; }
    public IRelayCommand ShowAdjustmentCommand { get; private set; }

    public IAsyncRelayCommand CorrectOpeningBalanceCommand { get; }
    public IAsyncRelayCommand CancelCorrectOpeningBalanceCommand { get; }

    public IRelayCommand ToggleSelectionCommand { get; }

    public string ReconcileButtonText {
        get {
            // If cleared balance matches register balance (everything is caught up)
            if (ReconciliationTransactions.All(x => x.IsCleared) && NewReconciledBalance == EndingBalance) {
                return "Reconcile";
            }

            // If there are still uncleared items remaining
            return "Save Cleared";
        }
    }

    public async Task ImportAccount() {
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
            FromAccountIsCleared = delta > 0 ? null : true,
            ToAccountIsCleared = delta > 0 ? true : null,
            Amount = Math.Abs(delta)
        };

        await _budgetService.UpsertTransactionAsync(adjustmentTransaction);
        CurrentAssetValue = 0; // Reset so it gets recalculated in LoadData
        IsBalanceAdjustmentVisible = false;
        OnPropertyChanged(nameof(CanExecuteAdjustBalance));
        
        await LoadDataAsync();
    }

    private async Task CancelCorrectOpeningBalanceAsync() {
        CanReconcile = false;
        if (OpeningBalanceMaximumAsOf.HasValue) {
            BlockingWarningMessage =
                $"Transactions predate your Opening Balance date, or you have yet to set one. Reconciliation is not possible until this is addressed. Please provide a new opening balance as of or prior to {OpeningBalanceMaximumAsOf.Value:MM/dd/yyyy}. Return to Reconciliation once you know this value, and it will prompt you for this entry.";
        }
        else {
            BlockingWarningMessage =
                "Transactions predate your Opening Balance date, or you have yet to set one. Reconciliation is not possible until this is addressed. Please provide a new opening balance older than the first transaction. Return to Reconciliation once you know this value, and it will prompt you for this entry.";
        }

        IsOpeningBalanceCorrectionVisible = false;
        OnPropertyChanged(nameof(CanExecuteCorrectOpeningBalance));
        CorrectOpeningBalanceCommand.NotifyCanExecuteChanged();
        CancelCorrectOpeningBalanceCommand.NotifyCanExecuteChanged();
    }

    private async Task CorrectOpeningBalanceAsync() {
        if (OpeningBalance == null) return;
        if (OpeningBalanceAsOf == null) return;

        (bool hasTransactions, decimal? openingBalance, DateTime? openingBalanceDate) openingRecord =
            await _budgetService.GetAccountBalanceOpeningStateAsync(_account.Id);

        var isDebtAccount = _account.IsLiability; //debtAccountTypes.Contains(_account.Type);

        if (openingRecord.openingBalance == null) {
            var openingBalanceTransaction = new Transaction() {
                AccountId = isDebtAccount ? _account.Id : null,
                ToAccountId = isDebtAccount
                    ? null
                    : _account.Id,
                AccountName = isDebtAccount
                    ? _account.Name
                    : null,
                ToAccountName = isDebtAccount
                    ? null
                    : _account.Name,
                Amount = isDebtAccount
                    ? -1 * OpeningBalance.Value
                    : OpeningBalance
                        .Value, //the balance is entered as a positive number, but we want to record it as a negative number
                TransactionDate = OpeningBalanceAsOf.Value,
                TransactionId = Guid.NewGuid(),
                FitId = Guid.NewGuid().ToString(),
                Description = Constants.OpeningBalance,
                Memo = Constants.OpeningBalance
            };

            await _budgetService.UpsertTransactionAsync(openingBalanceTransaction);
        }
        else {
            var allTransactions = await _budgetService.GetAccountTransactionsAsync(_account.Id);
            var openingBalanceTransaction =
                allTransactions.FirstOrDefault(t => t.Description == Constants.OpeningBalance);
            openingBalanceTransaction!.Amount = isDebtAccount ? -1 * OpeningBalance.Value : OpeningBalance.Value;
            openingBalanceTransaction!.TransactionDate = OpeningBalanceAsOf.Value;

            await _budgetService.UpsertTransactionAsync(openingBalanceTransaction);
        }

        IsOpeningBalanceCorrectionVisible = false;
        OnPropertyChanged(nameof(CanExecuteCorrectOpeningBalance));
        CorrectOpeningBalanceCommand.NotifyCanExecuteChanged();

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

    private async Task PromptForOpeningBalance(DateTime beforeDate) {
        IsOpeningBalanceCorrectionVisible = true;
    }


    private async Task LoadDataAsync() {
        try {
            SpinnerMessage = "Loading...";
            IsBusy = true;
            await Task.Delay(10);
            decimal beginningBalance = _account.IsLiability ? -_account.Balance : _account.Balance;
            DateTime? lastReconciledDate = _account.BalanceAsOf;
            (bool hasTransactions, decimal? openingBalance, DateTime? openingBalanceDate) openingRecord =
                await _budgetService.GetAccountBalanceOpeningStateAsync(_account.Id);
            IsOpeningBalanceCreated = openingRecord.openingBalance != null;
            OpeningBalance = openingRecord.openingBalance;
            OpeningBalanceAsOf = openingRecord.openingBalanceDate;
            OpeningBalanceMaximumAsOf = openingRecord.openingBalanceDate;

            var accountReconciliation = await _budgetService.GetLatestValidReconciliationAsync(_account.Id);

            if (accountReconciliation != null) {
                beginningBalance = _account.IsLiability
                    ? -accountReconciliation.ReconciledBalance
                    : accountReconciliation.ReconciledBalance;
                lastReconciledDate = accountReconciliation.ReconciledAsOfDate;
            }

            var transactions =
                await _budgetService.GetAllUnreconciledTransactionsSinceLastReconciliationAsync(_account.Id);
            transactions = transactions.Where(x=>x.AccountId == _account.Id || x.ToAccountId == _account.Id).OrderBy(b => b.TransactionDate).ToList();


            // 1. Fetch and order transactions at once
            var allTransactions = (await _budgetService.GetUnreconciledAccountLedgerAsync(_account.Id))
                .OrderBy(x => (DateTime)x.TransactionDate)
                .ToList();

            bool hasOpeningBalance = openingRecord.openingBalance != null;
            var earliestTransaction = allTransactions.FirstOrDefault(x => x.Description != Constants.OpeningBalance);

// ---------------------------------------------------------------------
// CASE 1: No Opening Balance Record Exists
// ---------------------------------------------------------------------
            if (!hasOpeningBalance) {
                if (earliestTransaction != null) {
                    // PROMPT: Set opening balance dated before earliest transaction
                    OpeningBalanceMaximumAsOf = (DateTime)earliestTransaction!.TransactionDate.AddDays(-1);
                    OpeningBalanceAsOf = OpeningBalanceMaximumAsOf;
                    // CanReconcile = false;
                    // BlockingWarningMessage = $"Transactions predate your Opening Balance date. Please provide a new opening balance as of or prior to {OpeningBalanceMaximumAsOf.Value:MM/dd/yyyy}.";

                    await PromptForOpeningBalance(OpeningBalanceMaximumAsOf.Value);

                    return; // Stops execution
                }
                else {
                    // PROMPT: Set opening balance during file import (it will go through this code again
                    //PromptForOpeningBalanceWithImport();
                }
            }

// ---------------------------------------------------------------------
// CASE 2: Opening Balance Exists, but Unreconciled Transactions Predate It
// ---------------------------------------------------------------------
            bool hasTransactionsBeforeOpening = earliestTransaction != null
                                                && (DateTime)earliestTransaction.TransactionDate <=
                                                openingRecord.openingBalanceDate;
            IsBusy = false;
            await Task.Delay(10);

            if (hasTransactionsBeforeOpening) {
                bool isOpeningBalanceReconciled = allTransactions.Any(x =>
                    x.Description == Constants.OpeningBalance && x.ReconciliationId != null);
                bool hasAnyReconciledRecords = allTransactions.Any(x =>
                    x.Description != Constants.OpeningBalance && x.ReconciliationId != null);

                if (isOpeningBalanceReconciled && hasAnyReconciledRecords) {
                    // PROMPT: Conflict with reconciled history — user must fix via Ledger


                    OpeningBalanceMaximumAsOf = (DateTime)earliestTransaction!.TransactionDate.AddDays(-1);
                    OpeningBalanceAsOf = OpeningBalanceMaximumAsOf;
                    //  CanReconcile = false;
                    // BlockingWarningMessage = $"Transactions predate your Opening Balance date. Please provide a new opening balance as of or prior to {OpeningBalanceMaximumAsOf.Value:MM/dd/yyyy}.";

                    await PromptForOpeningBalance(OpeningBalanceMaximumAsOf.Value);

                    // // 1. Alert the user
                    // MessageBox.Show(
                    //     "The opening balance for this account should be older than any other transactions. There are reconciled transactions older than the opening balance. You will need to fix the opening balance to proceed..",
                    //     "Opening Balance Invalid",
                    //     MessageBoxButton.OK,
                    //     MessageBoxImage.Warning);

                    // 2. Set dialog result (if opened via ShowDialog) and close
                    // CanReconcile = false;
                    // BlockingWarningMessage = "Reconciled transactions predate your Opening Balance date. Please correct your opening balance in the Ledger before reconciling.";
                    //
                    return; // Stops execution
                }
                else {
                    // PROMPT: Re-assign opening balance date prior to earliest transaction
                    OpeningBalanceMaximumAsOf = (DateTime)earliestTransaction!.TransactionDate.AddDays(-1);
                    OpeningBalanceAsOf = OpeningBalanceMaximumAsOf;
                    //  CanReconcile = false;
                    // BlockingWarningMessage = $"Transactions predate your Opening Balance date. Please provide a new opening balance as of or prior to {OpeningBalanceMaximumAsOf.Value:MM/dd/yyyy}.";

                    await PromptForOpeningBalance(OpeningBalanceMaximumAsOf.Value);

                    return; // Stops execution
                }
            }

            IsBusy = true;

            await Task.Delay(10);

            string json = JsonConvert.SerializeObject(transactions.ToList());
            var clonedTransactions = JsonConvert.DeserializeObject<List<Transaction>>(json) ?? new();

            // 2. Map the cloned models directly into ViewModels
            var reconciliationTransactions = clonedTransactions
                .Select(x => new TransactionViewModel(x, Account))
                .ToList();
            
            // ReSharper disable once NotAccessedVariable
            bool hasTransactionPriorToLastReconcile = false;
            // ReSharper disable once RedundantAssignment
            (EndingBalance, lastReconciledDate, beginningBalance, hasTransactionPriorToLastReconcile) =
                await _reconciliationService.CalculateRunningBalanceAsync(_account.Id, reconciliationTransactions!);
            BeginningBalance = beginningBalance;
            LastReconciledDate = lastReconciledDate ?? DateTime.MinValue;
            if (CurrentAssetValue == 0) CurrentAssetValue = EndingBalance;

            decimal? newReconciledBalance = null;
            DateTime? newReconciledDate = null;
            
            newReconciledBalance = beginningBalance;
            
            foreach (var t in reconciliationTransactions!.OrderBy(b => b.TransactionDate)) {
                if (t.AccountId == _account.Id) {
                    if (t.FromAccountIsCleared ?? false) {
                        t.IsCleared = true;
                    }
                }
                else if (t.ToAccountId == _account.Id) {
                    if (t.ToAccountIsCleared ?? false) {
                        t.IsCleared = true;
                    }
                }

                if (t.IsCleared) {
                 
                        if (t.AccountId == _account.Id) {
                            if (_account.IsLiability) {
                                newReconciledBalance += t.Amount;
                            }
                            else {
                                newReconciledBalance -= t.Amount;
                            }
                        }
                        else if (t.ToAccountId == _account.Id) {
                            if (_account.IsLiability) {
                                newReconciledBalance -= t.Amount;
                            }
                            else {
                                newReconciledBalance += t.Amount;
                            }
                        }
                    
                }
            }

            NewReconciledBalance = newReconciledBalance;
            NewReconciledDate = newReconciledDate;
            ReconciliationTransactions =
                new ObservableCollection<TransactionViewModel>(reconciliationTransactions!);
        }
        catch (Exception ex) {
            // Must catch exceptions locally to prevent app crash!
            Log.Error("ReconciliationViewModel failed to load data: " + ex.Message);
        }
        finally {
            IsBusy = false;
        }
    }

    public async Task UpdateReconciliationTransactionsAsync() {
        decimal? newReconciledBalance = null;
        DateTime? newReconciledDate = null;
        newReconciledBalance = BeginningBalance;
        foreach (var t in ReconciliationTransactions.OrderBy(b => b.TransactionDate)) {
            if (t.IsCleared) {
                if (t.AccountId == _account.Id) {
                    if (_account.IsLiability) {
                        newReconciledBalance += t.Amount;
                    }
                    else {
                        newReconciledBalance -= t.Amount;
                    }
                }
                else if (t.ToAccountId == _account.Id) {
                    if (_account.IsLiability) {
                        newReconciledBalance -= t.Amount;
                    }
                    else {
                        newReconciledBalance += t.Amount;
                    }
                }

                //newReconciledBalance = t.RunningBalance;
                newReconciledDate = t.TransactionDate;
            }
        }
        var recordedBalance = NewReconciledBalance ?? newReconciledBalance ?? 0;
        recordedBalance = _account.IsLiability ? -recordedBalance : recordedBalance;
        if (ReconciliationTransactions.All(x => x.IsCleared) &&
            (NewReconciledBalance.HasValue || newReconciledBalance.HasValue) &&
            (NewReconciledDate.HasValue || newReconciledDate.HasValue) &&
            ((NewReconciledBalance.HasValue && NewReconciledBalance.Value == EndingBalance) ||
             (newReconciledBalance.HasValue && newReconciledBalance.Value == EndingBalance))) {
            foreach (var tx in ReconciliationTransactions) {
                if (tx.IsCleared) {
                    tx.IsReconciled = true;
                }
            }

            await _reconciliationService.ReconcileAccountAsync(
                _account.Id,
                ReconciliationTransactions.ToList(),
                recordedBalance,
                NewReconciledDate ?? newReconciledDate ?? DateTime.MinValue);
        }
        else {
            //nothing flagged as reconciled, but there are bank cleared transactions
            await _reconciliationService.ReconcileAccountAsync(
                _account.Id,
                ReconciliationTransactions.ToList(),
                recordedBalance,
                NewReconciledDate ?? newReconciledDate ?? DateTime.MinValue);
        }
    }

    public void Reconcile() {
        decimal? newReconciledBalance = null;
        DateTime? newReconciledDate = null;
        
        newReconciledBalance = BeginningBalance;
        UpdateIsAllSelectedState();
        foreach (var t in ReconciliationTransactions.OrderBy(b => b.TransactionDate)) {
            if (t.IsCleared) {
                if (t.AccountId == _account.Id) {
                    if (_account.IsLiability) {
                        newReconciledBalance += t.Amount;
                    }
                    else {
                        newReconciledBalance -= t.Amount;
                    }
                }
                else if (t.ToAccountId == _account.Id) {
                    if (_account.IsLiability) {
                        newReconciledBalance -= t.Amount;
                    }
                    else {
                        newReconciledBalance += t.Amount;
                    }
                }
                
                newReconciledDate = t.TransactionDate;
            }
        }

        NewReconciledBalance = newReconciledBalance;
        NewReconciledDate = newReconciledDate;
    }
}