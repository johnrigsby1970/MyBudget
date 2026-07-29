using Serilog;
using StayOnTarget.Models;
using StayOnTarget.Services;
using StayOnTarget.Services.Projections;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json;
using StayOnTarget.Views;

namespace StayOnTarget.ViewModels;

public class MainViewModel : ViewModelBase {
    private readonly BudgetService _budgetService;
    private readonly ReconciliationService _reconciliationService;
    private readonly IProjectionEngine _projectionEngine;
    private ObservableCollection<Account> _accounts = new();
    private ObservableCollection<Account> _accountsWithNone = new();
    private ObservableCollection<Bill> _bills = new();
    private ObservableCollection<Bill> _billsWithNone = new();
    private ObservableCollection<Paycheck> _paychecks = new();
    private ObservableCollection<Paycheck> _paychecksWithNone = new();
    private ObservableCollection<ProjectionItem> _projections = new();
    private ObservableCollection<PeriodBill> _currentPeriodBills = new();
    private ObservableCollection<BudgetBucket> _buckets = new();
    private ObservableCollection<BudgetBucket> _bucketsWithNone = new();
    private ObservableCollection<PeriodBucket> _currentPeriodBuckets = new();
    private ObservableCollection<Transaction> _currentPeriodTransactions = new();
    private int _pastDueCount;
    private int _upcomingCount;
    private int _budgetExceededCount;
    private int _envelopeNearingFullCount;
    private ObservableCollection<PeriodBill> _unpaidPastDueBills = new();
    private ObservableCollection<PeriodBucket> _budgetBustedBuckets = new();
    private Bill? _selectedBill;
    private BudgetBucket? _selectedBucket;
    private PeriodBill? _selectedPeriodBill;
    private PeriodBucket? _selectedPeriodBucket;
    private Account? _selectedAccount;
    private Transaction? _selectedTransaction;
    private bool _isEditingBill;
    private bool _isEditingBucket;
    private bool _isEditingPeriodBucket;
    private bool _isEditingPeriodBill;
    private bool _isEditingAccount;
    private bool _isEditingTransaction;
    private bool _isCalculatingProjections;
    private bool _isBillDescriptionExpanded;
    private bool _isBucketDescriptionExpanded;
    private Bill? _editingBillClone;
    private PeriodBill? _editingPeriodBillClone;
    private BudgetBucket? _editingBucketClone;
    private PeriodBucket? _editingPeriodBucketClone;
    private Account? _editingAccountClone;
    private Transaction? _editingTransactionClone;
    private Paycheck? _editingPaycheckClone;
    private DateTime _currentPeriodDate = DateTime.MinValue;
    private bool _showByMonth;
    private int _selectedPeriodPaycheckId;
    private ObservableCollection<Paycheck> _periodPaychecks = new();
    private ObservableCollection<ToastViewModel> _toasts = new();
    private bool _isEditingPaycheck;
    private Paycheck? _selectedPaycheck;
    private bool _showReconciled = true;
    private string _toggleReconciliationText = "Show Reconciled";
    private DateTime _projectionEndDate = DateTime.Today.AddYears(1);
    private DateTime? _projectionStartDate;
    private int _selectedOuterTabIndex;
    private int _selectedInnerTabIndex;
    private int _selectedProjectionTabIndex;
    private SnowballStrategyOptions _snowballOptions = new();
    private ObservableCollection<ProjectionItem> _snowballProjections = new();

    #region Properties

    public SnowballStrategyOptions SnowballOptions {
        get => _snowballOptions;
        set => SetProperty(ref _snowballOptions, value);
    }

    public ObservableCollection<ProjectionItem> SnowballProjections {
        get => _snowballProjections;
        set => SetProperty(ref _snowballProjections, value);
    }

    public bool IsCalculatingProjections => _isCalculatingProjections;

    public bool IsBucketDescriptionExpanded {
        get => _isBucketDescriptionExpanded;
        set => SetProperty(ref _isBucketDescriptionExpanded, value);
    }

    public bool IsBillDescriptionExpanded {
        get => _isBillDescriptionExpanded;
        set => SetProperty(ref _isBillDescriptionExpanded, value);
    }

    public static MainViewModel? Instance { get; private set; }

    public MainViewModel(
        BudgetService budgetService,
        ReconciliationService reconciliationService) {
        Instance = this;
        _budgetService = budgetService;
        _reconciliationService = reconciliationService;
        _projectionEngine = new ProjectionEngine();
        _snowballOptions.PropertyChanged += (s, e) => {
            _ = CalculateProjectionsAsync();
        };

        ImportAccountCommand = new AsyncRelayCommand(ImportAccountAsync, () => IsEditingAccount);
        ReconcileAccountCommand =
            new AsyncRelayCommand(ReconcileAccountAsync, () => IsEditingAccount);
        SaveBillCommand = new AsyncRelayCommand(SaveBillAsync, () => IsEditingBill);
        DeleteBillCommand = new AsyncRelayCommand(DeleteBillAsync, () => IsEditingBill);
        SavePeriodBillCommand =
            new AsyncRelayCommand(SavePeriodBillAsync, () => IsEditingPeriodBill);
        DeletePeriodBillCommand =
            new AsyncRelayCommand(DeletePeriodBillAsync, () => IsEditingPeriodBill);
        SaveBucketCommand = new AsyncRelayCommand(SaveBucketAsync, () => IsEditingBucket);
        DeleteBucketCommand = new AsyncRelayCommand(DeleteBucketAsync);
        SavePeriodBucketCommand =
            new AsyncRelayCommand(SavePeriodBucketAsync, () => IsEditingPeriodBucket);
        DeletePeriodBucketCommand =
            new AsyncRelayCommand(DeletePeriodBucketAsync, () => IsEditingPeriodBucket);
        SaveTransactionCommand =
            new AsyncRelayCommand(_ => SaveTransactionAsync(), () => IsEditingTransaction);
        DeleteTransactionCommand =
            new AsyncRelayCommand(DeleteTransactionAsync, () => IsEditingTransaction);
        SavePaycheckCommand = new AsyncRelayCommand(SavePaycheckAsync, () => IsEditingPaycheck);
        DeletePaycheckCommand =
            new AsyncRelayCommand(DeletePaycheckAsync, () => IsEditingPaycheck);
        SetAccountAprRatesCommand =
            new AsyncRelayCommand(SetAccountAprRatesAsync, () => IsEditingAccount);
        SaveAccountCommand = new AsyncRelayCommand(SaveAccountAsync, () => IsEditingAccount);
        DeleteAccountCommand = new AsyncRelayCommand(DeleteAccountAsync, () => IsEditingAccount);
        NextPeriodCommand = new AsyncRelayCommand(() => NavigatePeriodAsync(1));
        PrevPeriodCommand = new AsyncRelayCommand(() => NavigatePeriodAsync(-1));


        AddBillCommand = new RelayCommand(AddBill, () => IsNotEditingBill);
        EditBillCommand = new RelayCommand(EditBill, () => CanEditBill);


        CancelBillCommand = new RelayCommand(CancelBill, () => IsEditingBill);


        EditPeriodBillCommand = new RelayCommand(EditPeriodBill, () => CanEditPeriodBill);


        CancelPeriodBillCommand = new RelayCommand(CancelPeriodBill, () => IsEditingPeriodBill);


        AddBucketCommand = new RelayCommand(AddBucket, () => IsNotEditingBucket);
        EditBucketCommand = new RelayCommand(EditBucket, () => CanEditBucket);


        CancelBucketCommand = new RelayCommand(CancelBucket, () => IsEditingBucket);


        EditPeriodBucketCommand = new RelayCommand(EditPeriodBucket, () => CanEditPeriodBucket);


        CancelPeriodBucketCommand =
            new RelayCommand(CancelPeriodBucket, () => IsEditingPeriodBucket);
        AddTransactionCommand = new RelayCommand(AddTransaction, () => IsNotEditingTransaction);
        EditTransactionCommand = new RelayCommand(EditTransaction, () => CanEditTransaction);
        CancelTransactionCommand = new RelayCommand(CancelTransaction, () => IsEditingTransaction);
        AddPaycheckCommand = new RelayCommand(AddPaycheck);
        EditPaycheckCommand = new RelayCommand(EditPaycheck, () => CanEditPaycheck);
        CancelPaycheckCommand = new RelayCommand(CancelPaycheck, () => IsEditingPaycheck);
        AddAccountCommand = new RelayCommand(AddAccount, () => IsNotEditingAccount);
        EditAccountCommand = new RelayCommand(EditAccount, () => CanEditAccount);
        CancelAccountCommand = new RelayCommand(CancelAccount, () => IsEditingAccount);
        ShowAmortizationCommand =
            new RelayCommand<Account>(a => ShowAmortization(a as Account ?? throw new InvalidOperationException()));
        ShowAboutCommand = new RelayCommand(ShowAbout);
        ExitCommand = new RelayCommand(Exit);
        BackupCommand = new RelayCommand(Backup);
        SetOneYearCommand = new RelayCommand(() => SetProjectionEndDate(1));
        SetFiveYearCommand = new RelayCommand(() => SetProjectionEndDate(5));
        SetTenYearCommand = new RelayCommand(() => SetProjectionEndDate(10));
        SetThirtyYearCommand = new RelayCommand(() => SetProjectionEndDate(30));
        MapsToBillCommand = new RelayCommand<PeriodBill>(pb => {
            if (pb is null) return;
            SelectedPeriodBill = pb;
            SelectedOuterTabIndex = 0;
            SelectedInnerTabIndex = 1;
        });
        MapsToBucketCommand = new RelayCommand<PeriodBucket>(pb => {
            if (pb is null) return;
            SelectedPeriodBucket = pb;
            SelectedOuterTabIndex = 0;
            SelectedInnerTabIndex = 2;
        });
        ToggleBucketDescriptionCommand =
            new RelayCommand(() => IsBucketDescriptionExpanded = !IsBucketDescriptionExpanded);
        ToggleBillDescriptionCommand =
            new RelayCommand(() => IsBillDescriptionExpanded = !IsBillDescriptionExpanded);

        InitializeDataCommand = new AsyncRelayCommand(InitializeDataAsync);
    }

    public IAsyncRelayCommand InitializeDataCommand { get; }

    private async Task InitializeDataAsync() {
        // Force the dispatcher to render the empty screen/loading state first

        await Task.Yield();

        IsLoading = true;
        IsGatheringData = true;
        IsProjecting = true;
        await Task.Yield();

        try {
            await LoadDataAsync();
            
            await Task.Yield();

            InitializePeriod();

            await Task.Yield();
            
            await LoadPeriodDataAsync();
            
            await Task.Yield();

            IsGatheringData = false;

            await CalculateProjectionsAsync();
        }
        catch (Exception ex) {
            Log.Error(ex, "Error initializing data.");
        }
        finally {
            IsLoading = false;
            IsProjecting = false;
        }
    }

    private bool _useAutoSweep;

    public bool UseAutoSweep {
        get => _useAutoSweep;
        set {
            if (SetProperty(ref _useAutoSweep, value)) {
                // 1. Immediately toggle the flag on the UI thread
                IsProjecting = true; 
            
                // 2. Schedule the calculation for the next UI tick
                OnCalculateProjections();
            }
        }
    }

    private int _yearsProjecting = 1;

    public int YearsProjecting {
        get => _yearsProjecting;
        set => SetProperty(ref _yearsProjecting, value);
    }
    
    private bool _isGatheringData;

    public bool IsGatheringData {
        get => _isGatheringData;
        set => SetProperty(ref _isGatheringData, value);
    }

    private bool _isLoading;

    public bool IsLoading {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    private bool _isProjecting;

    public bool IsProjecting {
        get => _isProjecting;
        set => SetProperty(ref _isProjecting, value);
    }

    private bool _isSnowballProjecting;

    public bool IsSnowballProjecting {
        get => _isSnowballProjecting;
        set => SetProperty(ref _isSnowballProjecting, value);
    }
    
    public ObservableCollection<Bill> Bills {
        get => _bills;
        set => SetProperty(ref _bills, value);
    }

    public ObservableCollection<Paycheck> Paychecks {
        get => _paychecks;
        set => SetProperty(ref _paychecks, value);
    }

    public ObservableCollection<Paycheck> PaychecksWithNone {
        get => _paychecksWithNone;
        set => SetProperty(ref _paychecksWithNone, value);
    }

    public ObservableCollection<Account> Accounts {
        get => _accounts;
        set => SetProperty(ref _accounts, value); 
    }

    public AccountType[] AccountTypes => (AccountType[])Enum.GetValues(typeof(AccountType));

    public ObservableCollection<Account> AccountsWithNone {
        get => _accountsWithNone;
        set => SetProperty(ref _accountsWithNone, value);
    }

    public ObservableCollection<Bill> BillsWithNone {
        get => _billsWithNone;
        set => SetProperty(ref _billsWithNone, value);
    }

    public ObservableCollection<BudgetBucket> BucketsWithNone {
        get => _bucketsWithNone;
        set => SetProperty(ref _bucketsWithNone, value);
    }

    public ObservableCollection<ProjectionItem> Projections {
        get => _projections;
        set => SetProperty(ref _projections, value);
    }
    
    public ObservableCollection<PeriodBill> CurrentPeriodBills {
        get => _currentPeriodBills;
        set {
            if (SetProperty(ref _currentPeriodBills, value)) {
                UpdateWarningMetrics();
            }
        }
    }

    public int PastDueCount {
        get => _pastDueCount;
        set => SetProperty(ref _pastDueCount, value);
    }

    public int UpcomingCount {
        get => _upcomingCount;
        set => SetProperty(ref _upcomingCount, value);
    }

    public ObservableCollection<PeriodBill> UnpaidPastDueBills {
        get => _unpaidPastDueBills;
        set => SetProperty(ref _unpaidPastDueBills, value);
    }

    private void UpdateWarningMetrics() {
        var today = DateTime.Today;
        var upcomingLimit = today.AddDays(2);

        var pastDue = CurrentPeriodBills.Where(pb => !pb.HasActualAmount && pb.DueDate < today && pb.ActualAmount != 0)
            .ToList();
        var upcoming = CurrentPeriodBills.Where(pb =>
            !pb.HasActualAmount && pb.DueDate >= today && pb.DueDate <= upcomingLimit && pb.ActualAmount != 0).ToList();

        PastDueCount = pastDue.Count;
        UpcomingCount = upcoming.Count;
        UnpaidPastDueBills = new ObservableCollection<PeriodBill>(pastDue);
        OnPropertyChanged(nameof(ShowWarningWidget));
    }

    public bool ShowWarningWidget => PastDueCount > 0 || UpcomingCount > 0;

    #region Warning Envelope

    public int BudgetExceededCount {
        get => _budgetExceededCount;
        set => SetProperty(ref _budgetExceededCount, value);
    }

    public int EnvelopeNearingFullCount {
        get => _envelopeNearingFullCount;
        set => SetProperty(ref _envelopeNearingFullCount, value);
    }

    public ObservableCollection<PeriodBucket> BudgetBustedBuckets {
        get => _budgetBustedBuckets;
        set => SetProperty(ref _budgetBustedBuckets, value);
    }

    private void UpdateBucketWarningMetrics() {
        var exceeded = CurrentPeriodBuckets.Where(pb =>
                pb.HasActualAmount && pb.ActualAmount != 0 && pb.BudgetExceeded && pb.TransactionAmount != 0)
            .ToList();
        var nearingfull = CurrentPeriodBuckets.Where(pb =>
            pb.HasActualAmount && pb.ActualAmount != 0 && !pb.BudgetExceeded && pb.TransactionAmount != 0 &&
            Math.Abs((double)pb.TransactionAmount / (double)pb.ActualAmount) > .80).ToList();

        BudgetExceededCount = exceeded.Count;
        EnvelopeNearingFullCount = nearingfull.Count;
        if (nearingfull.Count > 0) {
            var myList = exceeded;
            myList.AddRange(nearingfull);
            BudgetBustedBuckets = new ObservableCollection<PeriodBucket>(myList);
        }
        else {
            BudgetBustedBuckets = new ObservableCollection<PeriodBucket>(exceeded);
        }

        OnPropertyChanged(nameof(ShowEnvelopeWarningWidget));
    }

    public bool ShowEnvelopeWarningWidget => BudgetExceededCount > 0 || EnvelopeNearingFullCount > 0;

    #endregion

    public ObservableCollection<BudgetBucket> Buckets {
        get => _buckets;
        set => SetProperty(ref _buckets, value);
    }

    public ObservableCollection<PeriodBucket> CurrentPeriodBuckets {
        get => _currentPeriodBuckets;
        set {
            if (SetProperty(ref _currentPeriodBuckets, value)) {
                UpdateBucketWarningMetrics();
            }
        }
    }

    public ObservableCollection<Transaction> CurrentPeriodTransactions {
        get => _currentPeriodTransactions;
        set => SetProperty(ref _currentPeriodTransactions, value);
    }

    public string ToggleReconciliationText {
        get => _toggleReconciliationText;
        set => SetProperty(ref _toggleReconciliationText, value);
    }

    public bool ShowReconciled {
        get => _showReconciled;
        set {
            if (SetProperty(ref _showReconciled, value)) {
                // 1. Immediately toggle the flag on the UI thread
                IsProjecting = true; 
            
                // 2. Schedule the calculation for the next UI tick
                OnCalculateProjections();
            }
        }
    }

    private CancellationTokenSource? _cts;

    private async void OnCalculateProjections() {
        // 1. Immediately turn on the spinner state
        IsProjecting = true;

        // Cancel any pending calculation from a previous rapid date change
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        try {
            // Force WPF to paint the UI (spinner shows instantly!)
            await Application.Current.Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Render);

            // Wait 300ms — if the user changes the date again, this task gets cancelled
            await Task.Delay(300, token);

            await CalculateProjectionsAsync();
        }
        catch (OperationCanceledException) {
            // Ignored: User changed date again before 300ms passed
        }
        catch (Exception ex) {
            Log.Error(ex, "Failed to calculate projections.");
        }
    }
    
    // private async void OnCalculateProjections() {
    //     try {
    //         // Ensure IsProjecting is set
    //         IsProjecting = true;
    //         
    //         // Force WPF to process the visual rendering (hide chart, show spinner) BEFORE running the task setup
    //         await Application.Current.Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Render);
    //         
    //         await CalculateProjectionsAsync();
    //     }
    //     catch (Exception ex) {
    //         Log.Error(ex, "Failed to calculate projections for {Date}", _currentPeriodDate);
    //     }
    // }

    public bool ShowByMonth {
        get => _showByMonth;
        set {
            if (SetProperty(ref _showByMonth, value)) {
                InitializePeriod();
                OnShowByMonthChanged();
            }
        }
    }

    private async void OnShowByMonthChanged() {
        try {
            await LoadPeriodDataAsync();
        }
        catch (Exception ex) {
            Log.Error(ex, "Failed to load period data for month");
        }
    }

    public int SelectedPeriodPaycheckId {
        get => _selectedPeriodPaycheckId;
        set {
            if (SetProperty(ref _selectedPeriodPaycheckId, value)) {
                SetCurrentPeriodDate(value);
                // 1. Immediately toggle the flag on the UI thread
                IsProjecting = true; 
            
                // 2. Schedule the calculation for the next UI tick
                OnCalculateProjections();
            }
        }
    }

    public ObservableCollection<Paycheck> PeriodPaychecks {
        get => _periodPaychecks;
        set => SetProperty(ref _periodPaychecks, value);
    }

    public ObservableCollection<ToastViewModel> Toasts {
        get => _toasts;
        set => SetProperty(ref _toasts, value);
    }

    public string PeriodDisplay {
        get {
            if (ShowByMonth) return _currentPeriodDate.ToString("MMMM yyyy");
            return $"Period: {_currentPeriodDate:d}";
        }
    }

    public DateTime ProjectionEndDate {
        get => _projectionEndDate;
        set {
            if (SetProperty(ref _projectionEndDate, value)) {
                // 1. Immediately toggle the flag on the UI thread
                IsProjecting = true; 
            
                // 2. Schedule the calculation for the next UI tick
                OnCalculateProjections();
            }
        }
    }

    public DateTime? ProjectionStartDate {
        get => _projectionStartDate;
        set {
            if (SetProperty(ref _projectionStartDate, value)) {
                // 1. Immediately toggle the flag on the UI thread
                IsProjecting = true; 
            
                // 2. Schedule the calculation for the next UI tick
                OnCalculateProjections();
            }
        }
    }

    public int SelectedOuterTabIndex {
        get => _selectedOuterTabIndex;
        set => SetProperty(ref _selectedOuterTabIndex, value);
    }

    public int SelectedInnerTabIndex {
        get => _selectedInnerTabIndex;
        set => SetProperty(ref _selectedInnerTabIndex, value);
    }

    public int SelectedProjectionTabIndex {
        get => _selectedProjectionTabIndex;
        set => SetProperty(ref _selectedProjectionTabIndex, value);
    }
    
    public DateTime CurrentPeriodDate {
        get => _currentPeriodDate;
        set {
            if (SetProperty(ref _currentPeriodDate, value)) {
                OnPropertyChanged(nameof(PeriodDisplay));
                OnCurrentPeriodDateChanged();
            }
        }
    }

    private async void OnCurrentPeriodDateChanged() {
        try {
            await LoadPeriodDataAsync();
        }
        catch (Exception ex) {
            Log.Error(ex, "Failed to load period data for current period {Date}", _currentPeriodDate);
        }
    }

    public Bill? SelectedBill {
        get => _selectedBill;
        set {
            if (_selectedBill != value && IsEditingBill && EditingBillClone != null &&
                EditingBillClone?.Id != value?.Id) {
                CancelBill();
            }

            if (SetProperty(ref _selectedBill, value)) {
                OnPropertyChanged(nameof(CanEditBill));
                EditBillCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public PeriodBill? SelectedPeriodBill {
        get => _selectedPeriodBill;
        set {
            if (_selectedPeriodBill != value && IsEditingPeriodBill && EditingPeriodBillClone != null &&
                EditingPeriodBillClone?.Id != value?.Id) {
                CancelPeriodBill();
            }

            if (SetProperty(ref _selectedPeriodBill, value)) {
                OnPropertyChanged(nameof(CanEditPeriodBill));
                EditPeriodBillCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public BudgetBucket? SelectedBucket {
        get => _selectedBucket;
        set {
            if (_selectedBucket != value && IsEditingBucket && EditingBucketClone != null &&
                EditingBucketClone?.Id != value?.Id) {
                CancelBucket();
            }

            if (SetProperty(ref _selectedBucket, value)) {
                OnPropertyChanged(nameof(CanEditBucket));
                EditBucketCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public PeriodBucket? SelectedPeriodBucket {
        get => _selectedPeriodBucket;
        set {
            if (_selectedPeriodBucket != value && IsEditingPeriodBucket && EditingPeriodBucketClone != null &&
                EditingPeriodBucketClone?.Id != value?.Id) {
                CancelPeriodBucket();
            }

            if (SetProperty(ref _selectedPeriodBucket, value)) {
                OnPropertyChanged(nameof(CanEditPeriodBucket));
                EditPeriodBucketCommand.NotifyCanExecuteChanged();
            }
        }
    }


    public Account? SelectedAccount {
        get => _selectedAccount;
        set {
            if (_selectedAccount != value && IsEditingAccount && EditingAccountClone != null &&
                EditingAccountClone?.Id != value?.Id) {
                CancelAccount();
            }

            if (SetProperty(ref _selectedAccount, value)) {
                OnPropertyChanged(nameof(CanEditAccount));
                EditAccountCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public Transaction? SelectedTransaction {
        get => _selectedTransaction;
        set {
            if (_selectedTransaction != value && IsEditingTransaction && EditingTransactionClone != null &&
                EditingTransactionClone?.Id != value?.Id) {
                CancelTransaction();
            }

            if (SetProperty(ref _selectedTransaction, value)) {
                OnPropertyChanged(nameof(CanEditTransaction));
                EditTransactionCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public Paycheck? SelectedPaycheck {
        get => _selectedPaycheck;
        set {
            if (_selectedPaycheck != value && IsEditingPaycheck && EditingPaycheckClone != null &&
                EditingPaycheckClone?.Id != value?.Id) {
                CancelPaycheck();
            }

            if (SetProperty(ref _selectedPaycheck, value)) {
                OnPropertyChanged(nameof(CanEditPaycheck));
                EditPaycheckCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsEditingBill {
        get => _isEditingBill;
        set {
            if (SetProperty(ref _isEditingBill, value)) {
                OnPropertyChanged(nameof(IsNotEditingBill));
                OnPropertyChanged(nameof(CanEditBill));
                AddBillCommand.NotifyCanExecuteChanged();
                EditBillCommand.NotifyCanExecuteChanged();
                CancelBillCommand.NotifyCanExecuteChanged();
                SaveBillCommand.NotifyCanExecuteChanged();
                DeleteBillCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsNotEditingBill => !IsEditingBill;
    public bool CanEditBill => SelectedBill != null;

    public bool IsEditingPaycheck {
        get => _isEditingPaycheck;
        set {
            if (SetProperty(ref _isEditingPaycheck, value)) {
                OnPropertyChanged(nameof(IsNotEditingPaycheck));
                OnPropertyChanged(nameof(CanEditPaycheck));
                EditPaycheckCommand.NotifyCanExecuteChanged();
                CancelPaycheckCommand.NotifyCanExecuteChanged();
                SavePaycheckCommand.NotifyCanExecuteChanged();
                DeletePaycheckCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsNotEditingPaycheck => !IsEditingPaycheck;

    public bool CanEditPaycheck => SelectedPaycheck != null;

    public bool IsEditingPeriodBucket {
        get => _isEditingPeriodBucket;
        set {
            if (SetProperty(ref _isEditingPeriodBucket, value)) {
                OnPropertyChanged(nameof(IsNotEditingPeriodBucket));
                OnPropertyChanged(nameof(CanEditPeriodBucket));
                EditPeriodBucketCommand.NotifyCanExecuteChanged();
                CancelPeriodBucketCommand.NotifyCanExecuteChanged();
                SavePeriodBucketCommand.NotifyCanExecuteChanged();
                DeletePeriodBucketCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsEditingBucket {
        get => _isEditingBucket;
        set {
            if (SetProperty(ref _isEditingBucket, value)) {
                OnPropertyChanged(nameof(IsNotEditingBucket));
                OnPropertyChanged(nameof(CanEditBucket));
                AddBucketCommand.NotifyCanExecuteChanged();
                EditBucketCommand.NotifyCanExecuteChanged();
                CancelBucketCommand.NotifyCanExecuteChanged();
                SaveBucketCommand.NotifyCanExecuteChanged();
                DeleteBucketCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsNotEditingBucket => !IsEditingBucket;

    public bool CanEditBucket => SelectedBucket != null;

    public bool IsNotEditingPeriodBill => !IsEditingPeriodBill;

    public bool IsEditingPeriodBill {
        get => _isEditingPeriodBill;
        set {
            if (SetProperty(ref _isEditingPeriodBill, value)) {
                OnPropertyChanged(nameof(IsNotEditingPeriodBill));
                OnPropertyChanged(nameof(CanEditPeriodBill));
                EditPeriodBillCommand.NotifyCanExecuteChanged();
                CancelPeriodBillCommand.NotifyCanExecuteChanged();
                SavePeriodBillCommand.NotifyCanExecuteChanged();
                DeletePeriodBillCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool CanEditPeriodBill => SelectedPeriodBill != null;

    public bool IsNotEditingPeriodBucket => !IsEditingPeriodBucket;

    public bool CanEditPeriodBucket => SelectedPeriodBucket != null;

    public bool IsEditingAccount {
        get => _isEditingAccount;
        set {
            if (SetProperty(ref _isEditingAccount, value)) {
                OnPropertyChanged(nameof(IsNotEditingAccount));
                OnPropertyChanged(nameof(CanEditAccount));
                AddAccountCommand.NotifyCanExecuteChanged();
                EditAccountCommand.NotifyCanExecuteChanged();
                CancelAccountCommand.NotifyCanExecuteChanged();
                SaveAccountCommand.NotifyCanExecuteChanged();
                DeleteAccountCommand.NotifyCanExecuteChanged();
                ImportAccountCommand.NotifyCanExecuteChanged();
                ReconcileAccountCommand.NotifyCanExecuteChanged();
                SetAccountAprRatesCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsNotEditingAccount => !IsEditingAccount;
    public bool CanEditAccount => SelectedAccount != null;

    public bool IsEditingTransaction {
        get => _isEditingTransaction;
        set {
            if (SetProperty(ref _isEditingTransaction, value)) {
                OnPropertyChanged(nameof(IsNotEditingTransaction));
                OnPropertyChanged(nameof(CanEditTransaction));
                AddTransactionCommand.NotifyCanExecuteChanged();
                EditTransactionCommand.NotifyCanExecuteChanged();
                CancelTransactionCommand.NotifyCanExecuteChanged();
                SaveTransactionCommand.NotifyCanExecuteChanged();
                DeleteTransactionCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsNotEditingTransaction => !IsEditingTransaction;
    public bool CanEditTransaction => SelectedTransaction != null;

    public IEnumerable<Frequency> BillFrequencies { get; } = new[] { Frequency.Monthly, Frequency.Yearly };

    public Bill? EditingBillClone {
        get => _editingBillClone;
        set => SetProperty(ref _editingBillClone, value);
    }

    public PeriodBill? EditingPeriodBillClone {
        get => _editingPeriodBillClone;
        set => SetProperty(ref _editingPeriodBillClone, value);
    }

    public BudgetBucket? EditingBucketClone {
        get => _editingBucketClone;
        set => SetProperty(ref _editingBucketClone, value);
    }

    public PeriodBucket? EditingPeriodBucketClone {
        get => _editingPeriodBucketClone;
        set => SetProperty(ref _editingPeriodBucketClone, value);
    }

    public Account? EditingAccountClone {
        get => _editingAccountClone;
        set => SetProperty(ref _editingAccountClone, value);
    }

    public Transaction? EditingTransactionClone {
        get => _editingTransactionClone;
        set => SetProperty(ref _editingTransactionClone, value);
    }

    public Paycheck? EditingPaycheckClone {
        get => _editingPaycheckClone;
        set => SetProperty(ref _editingPaycheckClone, value);
    }

    #endregion

    #region Commands

    public IRelayCommand AddBillCommand { get; }

    public IRelayCommand EditBillCommand { get; }
    public IAsyncRelayCommand SaveBillCommand { get; }

    public IRelayCommand CancelBillCommand { get; }

    public IAsyncRelayCommand DeleteBillCommand { get; }

    public IRelayCommand EditPeriodBillCommand { get; }

    public IAsyncRelayCommand SavePeriodBillCommand { get; }

    public IRelayCommand CancelPeriodBillCommand { get; }

    public IAsyncRelayCommand DeletePeriodBillCommand { get; }

    public IRelayCommand AddBucketCommand { get; }

    public IRelayCommand EditBucketCommand { get; }
    public IAsyncRelayCommand SaveBucketCommand { get; }

    public IRelayCommand CancelBucketCommand { get; }

    public IAsyncRelayCommand DeleteBucketCommand { get; }

    public IRelayCommand EditPeriodBucketCommand { get; }

    public IAsyncRelayCommand SavePeriodBucketCommand { get; }

    public IRelayCommand CancelPeriodBucketCommand { get; }

    public IAsyncRelayCommand DeletePeriodBucketCommand { get; }

    public IRelayCommand AddTransactionCommand { get; }

    public IRelayCommand EditTransactionCommand { get; }

    public IAsyncRelayCommand SaveTransactionCommand { get; }

    public IRelayCommand CancelTransactionCommand { get; }

    public IAsyncRelayCommand DeleteTransactionCommand { get; }

    public IRelayCommand AddPaycheckCommand { get; }

    public IRelayCommand EditPaycheckCommand { get; }
    public IAsyncRelayCommand SavePaycheckCommand { get; }

    public IRelayCommand CancelPaycheckCommand { get; }

    public IAsyncRelayCommand DeletePaycheckCommand { get; }

    public IRelayCommand AddAccountCommand { get; }

    public IRelayCommand EditAccountCommand { get; }

    public IAsyncRelayCommand ReconcileAccountCommand { get; }

    public IAsyncRelayCommand ImportAccountCommand { get; }

    public IAsyncRelayCommand SetAccountAprRatesCommand { get; }

    public IAsyncRelayCommand SaveAccountCommand { get; }

    public IRelayCommand CancelAccountCommand { get; }

    public IAsyncRelayCommand DeleteAccountCommand { get; }

    public IAsyncRelayCommand NextPeriodCommand { get; }

    public IAsyncRelayCommand PrevPeriodCommand { get; }

    public IRelayCommand ShowAmortizationCommand { get; }

    public IRelayCommand ShowAboutCommand { get; }

    public IRelayCommand ExitCommand { get; }

    public IRelayCommand BackupCommand { get; }

    public IRelayCommand SetOneYearCommand { get; }

    public IRelayCommand SetFiveYearCommand { get; }

    public IRelayCommand SetTenYearCommand { get; }

    public IRelayCommand SetThirtyYearCommand { get; }

    public IRelayCommand MapsToBillCommand { get; }

    public IRelayCommand MapsToBucketCommand { get; }

    public IRelayCommand ToggleBucketDescriptionCommand { get; }

    public IRelayCommand ToggleBillDescriptionCommand { get; }

    private void SetProjectionEndDate(int years) {
        YearsProjecting = years;
        ProjectionEndDate = DateTime.Now.AddYears(years);
    }

    #endregion

    private bool _isLoadingData;
#pragma warning disable CS0414 // Field is assigned but its value is never used
    private bool _isLoadingAccountData;
#pragma warning restore CS0414 // Field is assigned but its value is never used
#pragma warning disable CS0414 // Field is assigned but its value is never used
    private bool _isLoadingBillData;
#pragma warning restore CS0414 // Field is assigned but its value is never used
#pragma warning disable CS0414 // Field is assigned but its value is never used
    private bool _isLoadingBucketData;
#pragma warning restore CS0414 // Field is assigned but its value is never used
#pragma warning disable CS0414 // Field is assigned but its value is never used
    private bool _isLoadingPaycheckData;
#pragma warning restore CS0414 // Field is assigned but its value is never used

    #region Events

    private async void Item_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) {
        if (_isLoadingData) return;
        try {
            switch (sender) {
                case Bill b:
                    await _budgetService.UpsertBillAsync(b);
                    break;
                case Paycheck p: {
                    await _budgetService.UpsertPaycheckAsync(p);
                    RefreshPaychecks();
                    if (p.Id == _selectedPeriodPaycheckId) {
                        OnPropertyChanged(nameof(SelectedPeriodPaycheckId));


                        await LoadPeriodDataAsync();
                    }

                    break;
                }
                case Account a:
                    await _budgetService.UpsertAccountAsync(a);
                    break;
                case BudgetBucket bb:
                    await _budgetService.UpsertBucketAsync(bb);
                    break;
            }

            await CalculateProjectionsAsync();
        }
        catch (Exception ex) {
            Log.Error(ex, "Error in Item_PropertyChanged for {SenderType}.", sender?.GetType().Name);
        }
    }

    private async void PeriodBill_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) {
        if (sender is not PeriodBill pb) return;
        try {
            if (e.PropertyName == nameof(PeriodBill.TransactionAmount)) {
                UpdateWarningMetrics();
                return;
            }

            if (e.PropertyName == nameof(PeriodBill.HasActualAmount)) {
                UpdateWarningMetrics();
                return;
            }

            if (e.PropertyName == nameof(PeriodBill.BudgetExceeded)) return;
            await _budgetService.UpsertPeriodBillAsync(pb);
            await LoadPeriodDataAsync();
            await CalculateProjectionsAsync();

            if (e.PropertyName == nameof(PeriodBill.ActualAmount)) return;
            {
                UpdateWarningMetrics();
            }
        }
        catch (Exception ex) {
            Log.Error(ex, "Error in PeriodBill_PropertyChanged.");
        }
    }

    private async void PeriodBucket_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) {
        if (sender is not PeriodBucket pb) return;
        try {
            if (e.PropertyName == nameof(PeriodBill.TransactionAmount)) {
                UpdateBucketWarningMetrics();
                return;
            }

            if (e.PropertyName == nameof(PeriodBill.HasActualAmount)) {
                UpdateBucketWarningMetrics();
                return;
            }


            if (e.PropertyName == nameof(PeriodBucket.BudgetExceeded)) return;
            await _budgetService.UpsertPeriodBucketAsync(pb);
            await LoadPeriodDataAsync();
            await CalculateProjectionsAsync();

            if (e.PropertyName == nameof(PeriodBill.ActualAmount)) return;
            {
                UpdateBucketWarningMetrics();
            }
        }
        catch (Exception ex) {
            Log.Error(ex, "Error in PeriodBucket_PropertyChanged.");
        }
    }

    private async void Transaction_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) {
        if (sender is not Transaction t) return;
        try {
            try {
                await _budgetService.UpsertTransactionAsync(t);
            }
            catch (Exception ex) {
                Log.Error(ex, "Error upserting transaction in PropertyChanged.");
            }

            await LoadPeriodDataAsync();
            await CalculateProjectionsAsync();
        }
        catch (Exception ex) {
            Log.Error(ex, "Error in Transaction_PropertyChanged.");
        }
    }

    #endregion

    #region Bill CRUD

    private void AddBill() {
        try {
            EditingBillClone = new Bill { Name = "New Bill", ExpectedAmount = 0, DueDay = 1, IsActive = true };
            SelectedBill = null;
            IsEditingBill = true;
        }
        catch (Exception ex) {
            Log.Error(ex, "Error initializing new bill.");
        }
    }

    private void EditBill() {
        try {
            CancelBill();
            if (SelectedBill == null) return;
            EditingBillClone = new Bill {
                Id = SelectedBill.Id, Name = SelectedBill.Name, ExpectedAmount = SelectedBill.ExpectedAmount,
                Frequency = SelectedBill.Frequency, DueDay = SelectedBill.DueDay, AccountId = SelectedBill.AccountId,
                ToAccountId = SelectedBill.ToAccountId, NextDueDate = SelectedBill.NextDueDate, IsPrincipalOnly = SelectedBill.IsPrincipalOnly,
                Category = SelectedBill.Category, IsActive = SelectedBill.IsActive
            };
            IsEditingBill = true;
        }
        catch (Exception ex) {
            Log.Error(ex, "Error entering edit mode for bill.");
        }
    }

    private async Task SaveBillAsync() {
        if (EditingBillClone == null) return;

        try {
            if (EditingBillClone.Frequency == Frequency.Monthly) {
                if (EditingBillClone.DueDay < 1 || EditingBillClone.DueDay > 31) {
                    MessageBox.Show("Due Day must be between 1 and 31 for Monthly bills.", "Validation Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                EditingBillClone.NextDueDate = null;
            }
            else if (EditingBillClone.Frequency == Frequency.Yearly) {
                if (EditingBillClone.NextDueDate == null) {
                    MessageBox.Show("Next Due Date is required for Yearly bills.", "Validation Error",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                EditingBillClone.DueDay = 0;
            }

            if (EditingBillClone.AccountId == 0) EditingBillClone.AccountId = null;
            if (EditingBillClone.ToAccountId == 0) EditingBillClone.ToAccountId = null;

            if (SelectedBill != null) {
                UpdateBillFromClone(SelectedBill, EditingBillClone);
                await _budgetService.UpsertBillAsync(SelectedBill);
            }
            else {
                await _budgetService.UpsertBillAsync(EditingBillClone);
            }

            await LoadBillDataAsync();
            await LoadPeriodDataAsync();
            IsEditingBill = false;
            EditingBillClone = null;
            await CalculateProjectionsAsync();
        }
        catch (Exception ex) {
            Log.Error(ex, "Error saving bill.");
            MessageBox.Show("Failed to save bill. See log for details.", "Error", MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void UpdateBillFromClone(Bill target, Bill clone) {
        target.Name = clone.Name;
        target.ExpectedAmount = clone.ExpectedAmount;
        target.Frequency = clone.Frequency;
        target.DueDay = clone.DueDay;
        target.AccountId = (clone.AccountId == 0 || clone.AccountId == null) ? null : clone.AccountId;
        target.ToAccountId = (clone.ToAccountId == 0 || clone.ToAccountId == null) ? null : clone.ToAccountId;
        target.NextDueDate = clone.NextDueDate;
        target.Category = clone.Category;
        target.IsActive = clone.IsActive;
        target.IsPrincipalOnly = clone.IsPrincipalOnly;
    }

    private void CancelBill() {
        try {
            IsEditingBill = false;
            EditingBillClone = null;
        }
        catch (Exception ex) {
            Log.Error(ex, "Error cancelling bill edit.");
        }
    }

    private async Task DeletePeriodBillAsync() {
        if (EditingPeriodBillClone == null) return;
        var messageBoxResult = MessageBox.Show(
            "Are you sure you want to delete this period's bill?", // Message
            "Delete Confirmation", // Title
            MessageBoxButton.YesNo, // Buttons
            MessageBoxImage.Warning // Icon
        );

        // Check the user's response
        if (messageBoxResult == MessageBoxResult.Yes) {
            try {
                // User confirmed deletion, proceed with your delete logic here
                await _budgetService.DeletePeriodBillAsync(EditingPeriodBillClone.Id);
                IsEditingPeriodBill = false;
                EditingPeriodBillClone = null;
                await LoadPeriodDataAsync();
                await CalculateProjectionsAsync();
            }
            catch (Exception ex) {
                Log.Error(ex, "Error deleting period bill.");
                MessageBox.Show("Failed to delete period bill. See log for details.", "Error", MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }


    private void EditPeriodBill() {
        try {
            CancelPeriodBill();
            //until a user customizes a bucket, it uses the budgeted bucket and the period bucket is a copy of that.
            if (SelectedPeriodBill == null) return;
            EditingPeriodBillClone = new PeriodBill {
                Id = SelectedPeriodBill.Id,
                BillName = SelectedPeriodBill.BillName,
                ActualAmount = SelectedPeriodBill.ActualAmount,
                BillId = SelectedPeriodBill.BillId,
                FitId = SelectedPeriodBill.FitId,
                DueDate = SelectedPeriodBill.DueDate,
                PeriodDate = SelectedPeriodBill.PeriodDate,
                IsPaid = SelectedPeriodBill.IsPaid
            };

            IsEditingPeriodBill = true;
        }
        catch (Exception ex) {
            Log.Error(ex, "Error entering edit mode for period bill.");
        }
    }

    private async Task SavePeriodBillAsync() {
        //until a user customizes a bucket, it uses the budgeted bucket and the period bucket is a copy of that.
        if (EditingPeriodBillClone == null) return;
        try {
            if (SelectedPeriodBill != null) {
                UpdatePeriodBillFromClone(SelectedPeriodBill, EditingPeriodBillClone);
            }
            else {
                await _budgetService.UpsertPeriodBillAsync(EditingPeriodBillClone);
            }

            await LoadPeriodDataAsync();
            IsEditingPeriodBill = false;
            EditingPeriodBillClone = null;
            await CalculateProjectionsAsync();
        }
        catch (Exception ex) {
            Log.Error(ex, "Error saving period bill.");
            MessageBox.Show("Failed to save period bill. See log for details.", "Error", MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }


    private void CancelPeriodBill() {
        try {
            IsEditingPeriodBill = false;
            EditingPeriodBillClone = null;
        }
        catch (Exception ex) {
            Log.Error(ex, "Error cancelling period bill edit.");
        }
    }


    private void UpdatePeriodBillFromClone(PeriodBill target, PeriodBill clone) {
        target.Id = clone.Id;
        target.ActualAmount = clone.ActualAmount;
        target.DueDate = clone.DueDate;
        target.IsPaid = clone.IsPaid;
    }

    private async Task DeleteBillAsync() {
        if (EditingBillClone == null) return;
        var messageBoxResult = MessageBox.Show(
            "Are you sure you want to delete this bill?", // Message
            "Delete Confirmation", // Title
            MessageBoxButton.YesNo, // Buttons
            MessageBoxImage.Warning // Icon
        );

        // Check the user's response
        if (messageBoxResult == MessageBoxResult.Yes) {
            try {
                // User confirmed deletion, proceed with your delete logic here
                await _budgetService.DeleteBillAsync(EditingBillClone.Id);
                IsEditingBill = false;
                EditingBillClone = null;
                await LoadBillDataAsync();
                await LoadPeriodDataAsync();
                await CalculateProjectionsAsync();
            }
            catch (Exception ex) {
                Log.Error(ex, "Error deleting bill.");
                MessageBox.Show("Failed to delete bill. See log for details.", "Error", MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }

    #endregion

    #region Bucket CRUD

    private void AddBucket() {
        try {
            EditingBucketClone = new BudgetBucket { Name = "New Bucket", ExpectedAmount = 0 };
            SelectedBucket = null;
            IsEditingBucket = true;
        }
        catch (Exception ex) {
            Log.Error(ex, "Error initializing new bucket.");
        }
    }

    private void EditBucket() {
        try {
            CancelBucket();
            if (SelectedBucket == null) return;
            EditingBucketClone = new BudgetBucket {
                Id = SelectedBucket.Id,
                Name = SelectedBucket.Name,
                ExpectedAmount = SelectedBucket.ExpectedAmount,
                AccountId = SelectedBucket.AccountId,
                PaycheckId = SelectedBucket.PaycheckId
            };
            IsEditingBucket = true;
        }
        catch (Exception ex) {
            Log.Error(ex, "Error entering edit mode for bucket.");
        }
    }

    private async Task SaveBucketAsync() {
        if (EditingBucketClone == null) return;

        try {
            if (EditingBucketClone.AccountId == 0) EditingBucketClone.AccountId = null;
            if (EditingBucketClone.PaycheckId == 0) EditingBucketClone.PaycheckId = null;

            if (SelectedBucket != null) {
                UpdateBucketFromClone(SelectedBucket, EditingBucketClone);
                await _budgetService.UpsertBucketAsync(SelectedBucket);
            }
            else {
                await _budgetService.UpsertBucketAsync(EditingBucketClone);
            }

            await LoadBucketDataAsync();
            await LoadPeriodDataAsync();
            IsEditingBucket = false;
            EditingBucketClone = null;
            await CalculateProjectionsAsync();
        }
        catch (Exception ex) {
            Log.Error(ex, "Error saving bucket.");
            MessageBox.Show("Failed to save bucket. See log for details.", "Error", MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void UpdateBucketFromClone(BudgetBucket target, BudgetBucket clone) {
        target.Name = clone.Name;
        target.ExpectedAmount = clone.ExpectedAmount;
        target.AccountId = clone.AccountId == 0 ? null : clone.AccountId;
        target.PaycheckId = clone.PaycheckId == 0 ? null : clone.PaycheckId;
    }

    private void CancelBucket() {
        try {
            IsEditingBucket = false;
            EditingBucketClone = null;
        }
        catch (Exception ex) {
            Log.Error(ex, "Error cancelling bucket edit.");
        }
    }

    private async Task DeleteBucketAsync() {
        if (EditingBucketClone == null) return;
        var messageBoxResult = MessageBox.Show(
            "Are you sure you want to delete this bucket?", // Message
            "Delete Confirmation", // Title
            MessageBoxButton.YesNo, // Buttons
            MessageBoxImage.Warning // Icon
        );

        // Check the user's response
        if (messageBoxResult == MessageBoxResult.Yes) {
            try {
                // User confirmed deletion, proceed with your delete logic here
                await _budgetService.DeleteBucketAsync(EditingBucketClone.Id);
                IsEditingBucket = false;
                EditingBucketClone = null;
                await LoadBucketDataAsync();
                await LoadPeriodDataAsync();
                await CalculateProjectionsAsync();
            }
            catch (Exception ex) {
                Log.Error(ex, "Error deleting bucket.");
                MessageBox.Show("Failed to delete bucket. See log for details.", "Error", MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }

    private void EditPeriodBucket() {
        try {
            CancelPeriodBucket();
            //until a user customizes a bucket, it uses the budgeted bucket and the period bucket is a copy of that.
            if (SelectedPeriodBucket == null) return;
            EditingPeriodBucketClone = new PeriodBucket {
                Id = SelectedPeriodBucket.Id,
                BucketName = SelectedPeriodBucket.BucketName,
                ActualAmount = SelectedPeriodBucket.ActualAmount,
                BucketId = SelectedPeriodBucket.BucketId,
                FitId = SelectedPeriodBucket.FitId,
                PeriodDate = SelectedPeriodBucket.PeriodDate,
                IsPaid = SelectedPeriodBucket.IsPaid
            };
            IsEditingPeriodBucket = true;
        }
        catch (Exception ex) {
            Log.Error(ex, "Error entering edit mode for period bucket.");
        }
    }

    private async Task SavePeriodBucketAsync() {
        //until a user customizes a bucket, it uses the budgeted bucket and the period bucket is a copy of that.
        if (EditingPeriodBucketClone == null) return;
        try {
            if (SelectedPeriodBucket != null) {
                UpdatePeriodBucketFromClone(SelectedPeriodBucket, EditingPeriodBucketClone);
            }
            else {
                await _budgetService.UpsertPeriodBucketAsync(EditingPeriodBucketClone);
            }

            await LoadPeriodDataAsync();
            IsEditingPeriodBucket = false;
            EditingPeriodBucketClone = null;
            await CalculateProjectionsAsync();
        }
        catch (Exception ex) {
            Log.Error(ex, "Error saving period bucket.");
            MessageBox.Show("Failed to save period bucket. See log for details.", "Error", MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void UpdatePeriodBucketFromClone(PeriodBucket target, PeriodBucket clone) {
        target.Id = clone.Id;
        target.BucketName = clone.BucketName;
        target.ActualAmount = clone.ActualAmount;
        target.BucketId = clone.BucketId;
        target.FitId = clone.FitId;
        target.PeriodDate = clone.PeriodDate;
        target.IsPaid = clone.IsPaid;
    }

    private void CancelPeriodBucket() {
        try {
            IsEditingPeriodBucket = false;
            EditingPeriodBucketClone = null;
        }
        catch (Exception ex) {
            Log.Error(ex, "Error cancelling period bucket edit.");
        }
    }

    private async Task DeletePeriodBucketAsync() {
        if (EditingPeriodBucketClone == null) return;
        var messageBoxResult = MessageBox.Show(
            "Are you sure you want to delete this period's bucket?\r\n\r\nIt will use the budgetted amount for the bucket instead. Save a $0 amount if you do not want to budget for this bucket for this period.", // Message
            "Delete Confirmation", // Title
            MessageBoxButton.YesNo, // Buttons
            MessageBoxImage.Warning // Icon
        );

        // Check the user's response
        if (messageBoxResult == MessageBoxResult.Yes) {
            try {
                // User confirmed deletion, proceed with your delete logic here
                await _budgetService.DeletePeriodBucketAsync(EditingPeriodBucketClone.Id);
                IsEditingPeriodBucket = false;
                EditingPeriodBucketClone = null;
                await LoadPeriodDataAsync();
                await CalculateProjectionsAsync();
            }
            catch (Exception ex) {
                Log.Error(ex, "Error deleting period bucket.");
                MessageBox.Show("Failed to delete period bucket. See log for details.", "Error", MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }

    #endregion

    #region Transaction CRUD

    private void AddTransaction() {
        try {
            var guid = Guid.NewGuid().ToString();
            EditingTransactionClone = new Transaction {
                Description = "", Memo = "", Amount = 0, TransactionDate = DateTime.Today,
                FitId = guid
            };
            SelectedTransaction = null;
            IsEditingTransaction = true;
        }
        catch (Exception ex) {
            Log.Error(ex, "Error initializing new transaction.");
        }
    }

    private void EditTransaction() {
        try {
            CancelTransaction();
            if (SelectedTransaction == null) return;
            EditingTransactionClone = new Transaction {
                Id = SelectedTransaction.Id,
                Description = SelectedTransaction.Description,
                Memo = SelectedTransaction.Memo,
                Amount = SelectedTransaction.Amount,
                TransactionDate = SelectedTransaction.TransactionDate,
                AccountId = SelectedTransaction.AccountId,
                ToAccountId = SelectedTransaction.ToAccountId,
                BucketId = SelectedTransaction.BucketId,
                IsPrincipalOnly = SelectedTransaction.IsPrincipalOnly,
                IsRebalance = SelectedTransaction.IsRebalance,
                PaycheckId = SelectedTransaction.PaycheckId,
                BillId = SelectedTransaction.BillId,
                BillName = SelectedTransaction.BillName,
                PaycheckOccurrenceDate = SelectedTransaction.PaycheckOccurrenceDate,
                FitId = SelectedTransaction.FitId,
                TransactionId = SelectedTransaction.TransactionId,
                FromAccountReconciledId = SelectedTransaction.FromAccountReconciledId,
                ToAccountReconciledId = SelectedTransaction.ToAccountReconciledId
            };
            IsEditingTransaction = true;
        }
        catch (Exception ex) {
            Log.Error(ex, "Error entering edit mode for transaction.");
        }
    }

    private async Task SaveTransactionAsync() {
        if (EditingTransactionClone == null) return;

        try {
            if (EditingTransactionClone.AccountId == 0) EditingTransactionClone.AccountId = null;
            if (EditingTransactionClone.ToAccountId == 0) EditingTransactionClone.ToAccountId = null;
            if (EditingTransactionClone.BillId == 0) EditingTransactionClone.BillId = null;
            if (EditingTransactionClone.BucketId == 0) EditingTransactionClone.BucketId = null;

            if (SelectedTransaction != null) {
                UpdateTransactionFromClone(SelectedTransaction, EditingTransactionClone);
                await _budgetService.UpsertTransactionAsync(SelectedTransaction);
            }
            else {
                await _budgetService.UpsertTransactionAsync(EditingTransactionClone);
            }

            IsEditingTransaction = false;
            EditingTransactionClone = null;

            await LoadPeriodDataAsync();
            await CalculateProjectionsAsync();
        }
        catch (Exception ex) {
            Log.Error(ex, "Error saving transaction.");
            MessageBox.Show("Failed to save transaction. See log for details.", "Error", MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void UpdateTransactionFromClone(Transaction target, Transaction clone) {
        target.TransactionId = clone.TransactionId; // Keep the Guid chain bound
        target.FitId = clone.FitId; // Keep the Guid chain bound
        target.Description = clone.Description;
        target.Memo = clone.Memo;
        target.Amount = clone.Amount;
        target.TransactionDate = clone.TransactionDate;
        target.AccountId = clone.AccountId == 0 ? null : clone.AccountId;
        target.ToAccountId = clone.ToAccountId == 0 ? null : clone.ToAccountId;
        target.BucketId = clone.BucketId == 0 ? null : clone.BucketId;
        target.BillId = clone.BillId == 0 ? null : clone.BillId;
        target.IsPrincipalOnly = clone.IsPrincipalOnly;
        target.IsInterestOnly = clone.IsInterestOnly;
        target.IsRebalance = clone.IsRebalance;
        target.PaycheckId = clone.PaycheckId;
        target.PaycheckOccurrenceDate = clone.PaycheckOccurrenceDate;
        target.FromAccountReconciledId = clone.FromAccountReconciledId;
        target.ToAccountReconciledId = clone.ToAccountReconciledId;
    }

    private void CancelTransaction() {
        try {
            if (SelectedTransaction != null && SelectedTransaction.TransactionId == Guid.Empty) {
                CurrentPeriodTransactions.Remove(SelectedTransaction);
            }

            IsEditingTransaction = false;
            EditingTransactionClone = null;
        }
        catch (Exception ex) {
            Log.Error(ex, "Error cancelling transaction edit.");
        }
    }

    private async Task DeleteTransactionAsync() {
        if (EditingTransactionClone == null) return;
        var messageBoxResult = MessageBox.Show(
            "Are you sure you want to delete this transaction?", // Message
            "Delete Confirmation", // Title
            MessageBoxButton.YesNo, // Buttons
            MessageBoxImage.Warning // Icon
        );

        // Check the user's response
        if (messageBoxResult == MessageBoxResult.Yes) {
            try {
                // User confirmed deletion, proceed with your delete logic here
                await _budgetService.DeleteTransactionAsync(EditingTransactionClone.TransactionId);
                IsEditingTransaction = false;
                EditingTransactionClone = null;
                await LoadPeriodDataAsync();
                await CalculateProjectionsAsync();
            }
            catch (Exception ex) {
                Log.Error(ex, "Error deleting transaction.");
                MessageBox.Show("Failed to delete transaction. See log for details.", "Error", MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }

    #endregion

    #region Paycheck CRUD

    private void AddPaycheck() {
        try {
            EditingPaycheckClone = new Paycheck {
                Name = "New Paycheck", ExpectedAmount = 0, StartDate = DateTime.Today, Frequency = Frequency.BiWeekly
            };
            SelectedPaycheck = null;
            IsEditingPaycheck = true;
        }
        catch (Exception ex) {
            Log.Error(ex, "Error initializing new paycheck.");
        }
    }

    private void EditPaycheck() {
        try {
            CancelPaycheck();
            if (SelectedPaycheck == null) return;
            EditingPaycheckClone = new Paycheck {
                Id = SelectedPaycheck.Id,
                Name = SelectedPaycheck.Name,
                ExpectedAmount = SelectedPaycheck.ExpectedAmount,
                Frequency = SelectedPaycheck.Frequency,
                StartDate = SelectedPaycheck.StartDate,
                EndDate = SelectedPaycheck.EndDate,
                AccountId = SelectedPaycheck.AccountId,
                IsBalanced = SelectedPaycheck.IsBalanced
            };
            IsEditingPaycheck = true;
        }
        catch (Exception ex) {
            Log.Error(ex, "Error entering edit mode for paycheck.");
        }
    }

    private async Task SavePaycheckAsync() {
        if (EditingPaycheckClone == null) return;
        try {
            if (SelectedPaycheck != null) {
                UpdatePaycheckFromClone(SelectedPaycheck, EditingPaycheckClone);
                await _budgetService.UpsertPaycheckAsync(SelectedPaycheck);
            }
            else {
                await _budgetService.UpsertPaycheckAsync(EditingPaycheckClone);
            }

            IsEditingPaycheck = false;
            EditingPaycheckClone = null;

            await LoadPaycheckDataAsync();
            await LoadPeriodDataAsync();
            RefreshPaychecks();
            LoadPaychecks();
            await CalculateProjectionsAsync();
        }
        catch (Exception ex) {
            Log.Error(ex, "Error saving paycheck.");
            MessageBox.Show("Failed to save paycheck. See log for details.", "Error", MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void UpdatePaycheckFromClone(Paycheck target, Paycheck clone) {
        target.Name = clone.Name;
        target.ExpectedAmount = clone.ExpectedAmount;
        target.Frequency = clone.Frequency;
        target.StartDate = clone.StartDate;
        target.EndDate = clone.EndDate;
        target.AccountId = clone.AccountId;
        target.IsBalanced = clone.IsBalanced;
    }

    private void CancelPaycheck() {
        try {
            IsEditingPaycheck = false;
            EditingPaycheckClone = null;
        }
        catch (Exception ex) {
            Log.Error(ex, "Error cancelling paycheck edit.");
        }
    }

    private async Task DeletePaycheckAsync() {
        if (EditingPaycheckClone == null) return;
        var messageBoxResult = MessageBox.Show(
            "Are you sure you want to delete this paycheck?", // Message
            "Delete Confirmation", // Title
            MessageBoxButton.YesNo, // Buttons
            MessageBoxImage.Warning // Icon
        );

        // Check the user's response
        if (messageBoxResult == MessageBoxResult.Yes) {
            try {
                // User confirmed deletion, proceed with your delete logic here
                await _budgetService.DeletePaycheckAsync(EditingPaycheckClone.Id);
                IsEditingPaycheck = false;
                EditingPaycheckClone = null;
                await LoadPaycheckDataAsync();
                await LoadPeriodDataAsync();
                RefreshPaychecks();
                await CalculateProjectionsAsync();
            }
            catch (Exception ex) {
                Log.Error(ex, "Error deleting paycheck.");
                MessageBox.Show("Failed to delete paycheck. See log for details.", "Error", MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }

    #endregion

    #region Account CRUD

    private void AddAccount() {
        try {
            EditingAccountClone = new Account {
                Name = "New Account",
                Type = AccountType.Checking,
                Balance = 0,
                BalanceAsOf = DateTime.Today,
                IncludeInTotal = true,
                MortgageDetails = new MortgageDetails(),
                CreditCardDetails = new CreditCardDetails(),
                HexColor = "#FF808080"
            };
            SelectedAccount = null;
            IsEditingAccount = true;
        }
        catch (Exception ex) {
            Log.Error(ex, "Error initializing new account.");
        }
    }

    private void EditAccount() {
        try {
            CancelAccount();
            if (SelectedAccount == null) return;
            EditingAccountClone = new Account {
                Id = SelectedAccount.Id,
                Name = SelectedAccount.Name,
                BankName = SelectedAccount.BankName,
                Balance = SelectedAccount.Balance,
                BalanceAsOf = SelectedAccount.BalanceAsOf,
                AnnualGrowthRate = SelectedAccount.AnnualGrowthRate,
                IncludeInTotal = SelectedAccount.IncludeInTotal,
                Type = SelectedAccount.Type,
                HexColor = SelectedAccount.HexColor,
                IsPrimary = SelectedAccount.IsPrimary
            };
            if (SelectedAccount.MortgageDetails != null) {
                EditingAccountClone.MortgageDetails = new MortgageDetails {
                    Id = SelectedAccount.MortgageDetails.Id,
                    AccountId = SelectedAccount.MortgageDetails.AccountId,
                    InterestRate = SelectedAccount.MortgageDetails.InterestRate,
                    Escrow = SelectedAccount.MortgageDetails.Escrow,
                    MortgageInsurance = SelectedAccount.MortgageDetails.MortgageInsurance,
                    LoanPayment = SelectedAccount.MortgageDetails.LoanPayment,
                    PaymentDate = SelectedAccount.MortgageDetails.PaymentDate
                };
            }
            else {
                EditingAccountClone.MortgageDetails = new MortgageDetails();
            }

            if (SelectedAccount.CreditCardDetails != null) {
                EditingAccountClone.CreditCardDetails = new CreditCardDetails {
                    Id = SelectedAccount.CreditCardDetails.Id,
                    AccountId = SelectedAccount.CreditCardDetails.AccountId,
                    StatementDay = SelectedAccount.CreditCardDetails.StatementDay,
                    DueDateOffset = SelectedAccount.CreditCardDetails.DueDateOffset,
                    GraceActive = SelectedAccount.CreditCardDetails.GraceActive,
                    MinPayFloor = SelectedAccount.CreditCardDetails.MinPayFloor,
                    PayPreviousMonthBalanceInFull = SelectedAccount.CreditCardDetails.PayPreviousMonthBalanceInFull
                };
            }
            else {
                EditingAccountClone.CreditCardDetails = new CreditCardDetails();
            }

            IsEditingAccount = true;
        }
        catch (Exception ex) {
            Log.Error(ex, "Error entering edit mode for account.");
        }
    }

    private async Task SaveAccountAsync() {
        if (EditingAccountClone != null) {
            try {
                if (EditingAccountClone.Type == AccountType.CreditCard &&
                    (EditingAccountClone.AccountAprHistory == null ||
                     EditingAccountClone.AccountAprHistory.Count ==
                     0)) {
                    MessageBox.Show(
                        "Before you can save this credit card, you need to set up your interest rates.", // Message
                        "Incomplete Setup", // Title
                        MessageBoxButton.OK, // Buttons
                        MessageBoxImage.Warning // Icon
                    );
                    SetAccountAprRatesCommand.Execute(EditingAccountClone);
                    return;
                }

                if (SelectedAccount != null) {
                    UpdateAccountFromClone(SelectedAccount, EditingAccountClone);
                    await _budgetService.UpsertAccountAsync(SelectedAccount);
                }
                else {
                    EditingAccountClone.Id = await _budgetService.UpsertAccountAsync(EditingAccountClone);
                    var openingBalance = new Transaction() {
                        AccountId = EditingAccountClone.Id,
                        Amount = EditingAccountClone.Balance,
                        TransactionDate = EditingAccountClone.BalanceAsOf,
                        TransactionId = Guid.NewGuid(),
                        FitId = Guid.NewGuid().ToString(),
                        Description = "Opening Balance",
                        Memo = "Opening Balance"
                    };

                    if (openingBalance.Amount != 0) {
                        try {
                            await _budgetService.UpsertTransactionAsync(openingBalance);
                        }
                        catch (Exception ex) {
                            Log.Error(ex, "Error upserting transaction in PropertyChanged.");
                        }

                        var transactions =
                            await _budgetService.GetAccountTransactionsAsync(openingBalance.AccountId.Value);

                        string json = JsonConvert.SerializeObject(transactions.ToList());
                        var reconciliationTransactions =
                            JsonConvert.DeserializeObject<List<ReconciliationTransaction>>(json);
                        if (reconciliationTransactions != null) {
                            await _reconciliationService.ReconcileAccountAsync(
                                openingBalance.AccountId.Value,
                                reconciliationTransactions,
                                openingBalance.Amount,
                                openingBalance.TransactionDate);
                        }
                    }
                }

                await LoadAccountDataAsync();
                await LoadPeriodDataAsync();
                IsEditingAccount = false;
                EditingAccountClone = null;
                await CalculateProjectionsAsync();


            }
            catch (Exception ex) {
                Log.Error(ex, "Error saving account.");
                MessageBox.Show("Failed to save account. See log for details.", "Error", MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }

    private void UpdateAccountFromClone(Account target, Account clone) {
        target.Name = clone.Name;
        target.BankName = clone.BankName;
        target.Balance = clone.Balance;
        target.BalanceAsOf = clone.BalanceAsOf;
        target.AnnualGrowthRate = clone.AnnualGrowthRate;
        target.IncludeInTotal = clone.IncludeInTotal;
        target.Type = clone.Type;
        target.HexColor = clone.HexColor;
        target.IsPrimary = clone.IsPrimary;

        if (clone is { Type: AccountType.Mortgage, MortgageDetails: not null }) {
            target.MortgageDetails ??= new MortgageDetails();
            target.MortgageDetails.InterestRate = clone.MortgageDetails.InterestRate;
            target.MortgageDetails.Escrow = clone.MortgageDetails.Escrow;
            target.MortgageDetails.MortgageInsurance = clone.MortgageDetails.MortgageInsurance;
            target.MortgageDetails.LoanPayment = clone.MortgageDetails.LoanPayment;
            target.MortgageDetails.PaymentDate = clone.MortgageDetails.PaymentDate;
        }

        if (clone is { Type: AccountType.CreditCard, CreditCardDetails: not null }) {
            target.CreditCardDetails ??= new CreditCardDetails();
            target.CreditCardDetails.StatementDay = clone.CreditCardDetails.StatementDay;
            target.CreditCardDetails.DueDateOffset = clone.CreditCardDetails.DueDateOffset;
            target.CreditCardDetails.GraceActive = clone.CreditCardDetails.GraceActive;
            target.CreditCardDetails.MinPayFloor = clone.CreditCardDetails.MinPayFloor;
            target.CreditCardDetails.PayPreviousMonthBalanceInFull =
                clone.CreditCardDetails.PayPreviousMonthBalanceInFull;
        }
    }

    private void CancelAccount() {
        try {
            IsEditingAccount = false;
            EditingAccountClone = null;
        }
        catch (Exception ex) {
            Log.Error(ex, "Error cancelling account edit.");
        }
    }

    private async Task DeleteAccountAsync() {
        if (EditingAccountClone == null) return;
        var messageBoxResult = MessageBox.Show(
            "Are you sure you want to delete this account?", // Message
            "Delete Confirmation", // Title
            MessageBoxButton.YesNo, // Buttons
            MessageBoxImage.Warning // Icon
        );

        // Check the user's response
        if (messageBoxResult == MessageBoxResult.Yes) {
            try {
                // User confirmed deletion, proceed with your delete logic here
                await _budgetService.DeleteAccountAsync(EditingAccountClone.Id);
                IsEditingAccount = false;
                EditingAccountClone = null;
                await LoadAccountDataAsync();
                await LoadPeriodDataAsync();
                await CalculateProjectionsAsync();
            }
            catch (Exception ex) {
                Log.Error(ex, "Error deleting account.");
                MessageBox.Show("Failed to delete account. See log for details.", "Error", MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }

    #endregion

    #region Helpers

    // public async Task CalculateProjectionsAsync() {
    //     if (_isCalculatingProjections) return;
    //     _isCalculatingProjections = true;
    //     try {
    //         IsProjecting = true;
    //
    //         // Give WPF time to render the hourglass on screen
    //         await Task.Yield();
    //
    //         var showReconicled = ShowReconciled;
    //         var currentPeriodDate = CurrentPeriodDate;
    //         var projectionStartDate = ProjectionStartDate;
    //         var projectionEndDate = ProjectionEndDate;
    //         
    //         var paychecks = await _budgetService.GetAllPaychecksAsync();
    //         var bills = await _budgetService.GetAllBillsAsync();
    //         var buckets = await _budgetService.GetAllBucketsAsync();
    //         var periodBills = await _budgetService.GetAllPeriodBillsAsync();
    //         var periodBuckets = await _budgetService.GetAllPeriodBucketsAsync();
    //         var transactions = showReconicled
    //             ? await _budgetService.GetAllTransactionsAsync()
    //             : await _budgetService.GetAllUnreconciledTransactionsAsync();
    //         var reconciliations = !showReconicled ? await _budgetService.GetAllAccountReconciliationsAsync() : null;
    //         reconciliations = null;
    //         var start = currentPeriodDate == DateTime.MinValue ? DateTime.Today : currentPeriodDate;
    //         if (projectionStartDate.HasValue) start = projectionStartDate.Value;
    //         var accounts = (await _budgetService.GetAllAccountsAsOfAsync(start.AddDays(-1))).ToList();
    //         var end = projectionEndDate;
    //         if (end < start) end = start.AddYears(1);
    //         // start = new DateTime(2026, 2, 19);
    //         // end = new DateTime(2027, 2, 19);
    //         var allPaycheckTransactions = await _budgetService.GetAllPaycheckTransactionsAsync();
    //         var allBillTransactions = await _budgetService.GetBillTransactionsAsync();
    //         var allBucketTransactions = await _budgetService.GetBucketTransactionsAsync();
    //         var allTransactions = (await _budgetService.GetAllTransactionsAsync()).ToList();
    //         var paycheckTransactions = allPaycheckTransactions.ToList();
    //
    //         #region Massage paycheck transaction date
    //
    //         //Whatever date the paycheck may have come in on, for purposes of this projection, it came in on its expected date.
    //         //So that it can be attributed to the pay period.
    //         foreach (var allPaycheckTransaction in paycheckTransactions) {
    //             if (allPaycheckTransaction.PaycheckOccurrenceDate != null && allPaycheckTransaction.TransactionDate !=
    //                 allPaycheckTransaction.PaycheckOccurrenceDate) {
    //                 allPaycheckTransaction.TransactionDate = allPaycheckTransaction.PaycheckOccurrenceDate.Value;
    //             }
    //         }
    //
    //         allTransactions.Where(x => x.PaycheckId != null).ToList().ForEach(x => {
    //             if (x.PaycheckOccurrenceDate != null && x.TransactionDate != x.PaycheckOccurrenceDate) {
    //                 x.TransactionDate = x.PaycheckOccurrenceDate.Value;
    //             }
    //         });
    //
    //         #endregion
    //         
    //         // 2. HEAVY CPU MATH: Keep in Task.Run, but ONLY compute data (No UI updates!)
    //         var (resultList, negativeAccounts) = await Task.Run(() => {
    //             var results = _projectionEngine.CalculateProjections(
    //                 paycheckTransactions,
    //                 allBillTransactions.ToList(),
    //                 allBucketTransactions.ToList(),
    //                 allTransactions,
    //                 start, end, accounts.ToList(), paychecks.ToList(), bills.ToList(), buckets.ToList(),
    //                 periodBills.ToList(), periodBuckets.ToList(), transactions.ToList(), reconciliations?.ToList(),
    //                 ShowReconciled, true);
    //
    //             var list = results.ToList();
    //
    //             // Check for negative checking/savings accounts
    //             var negAccounts = new HashSet<string>();
    //             foreach (var item in list) {
    //                 foreach (var acc in accounts) {
    //                     if (acc.Type is not (AccountType.Checking or AccountType.Savings)) continue;
    //                     if (item.AccountBalances.TryGetValue(acc.Name, out decimal balance) && balance < 0) {
    //                         negAccounts.Add(acc.Name);
    //                     }
    //                 }
    //             }
    //
    //             // Return calculated results as a Tuple
    //             return (list, negAccounts);
    //         });
    //
    //         // 3. BACK ON UI THREAD: Safely update collections and UI toasts!
    //         Projections = new ObservableCollection<ProjectionItem>(resultList);
    //
    //         if (negativeAccounts.Any()) {
    //             string message =
    //                 $"Warning: The following accounts go negative in the projection: {string.Join(", ", negativeAccounts)}";
    //             ShowToast(message);
    //         }
    //     }
    //     catch (Exception ex) {
    //         Log.Error(ex, "Error calculating projections.");
    //         ShowToast("Failed to calculate projections. Check logs.");
    //     }
    //     finally {
    //         _isCalculatingProjections = false;
    //         IsProjecting = false;
    //     }
    // }


public async Task CalculateProjectionsAsync() {
    if (_isCalculatingProjections) return;
    _isCalculatingProjections = true;
    try {
        IsProjecting = true;
        IsSnowballProjecting = true;
        
        // Force WPF to draw the spinner on screen BEFORE background processing starts
        await Task.Yield();

        // Capture local copies of ViewModel properties on the UI thread first
        var showReconciled = ShowReconciled;
        var currentPeriodDate = CurrentPeriodDate;
        var projectionStartDate = ProjectionStartDate;
        var projectionEndDate = ProjectionEndDate;

        // 1. ALL HEAVY I/O, DATA MASSAGING, AND CALCULATIONS ON BACKGROUND THREAD
        var (resultList, snowballList, negativeAccounts) = await Task.Run(async () => {
            var paychecks = await _budgetService.GetAllPaychecksAsync();
            var bills = await _budgetService.GetAllBillsAsync();
            var buckets = await _budgetService.GetAllBucketsAsync();
            var periodBills = await _budgetService.GetAllPeriodBillsAsync();
            var periodBuckets = await _budgetService.GetAllPeriodBucketsAsync();
            var transactions = showReconciled
                ? await _budgetService.GetAllTransactionsAsync()
                : await _budgetService.GetAllUnreconciledTransactionsAsync();
            var reconciliations = !showReconciled ? await _budgetService.GetAllAccountReconciliationsAsync() : null;
            reconciliations = null;

            var start = currentPeriodDate == DateTime.MinValue ? DateTime.Today : currentPeriodDate;
            if (projectionStartDate.HasValue) start = projectionStartDate.Value;

            var accounts = (await _budgetService.GetAllAccountsAsOfAsync(start.AddDays(-1))).ToList();
            var end = projectionEndDate;
            if (end < start) end = start.AddYears(1);

            var allPaycheckTransactions = await _budgetService.GetAllPaycheckTransactionsAsync();
            var allBillTransactions = await _budgetService.GetBillTransactionsAsync();
            var allBucketTransactions = await _budgetService.GetBucketTransactionsAsync();
            var allTransactions = (await _budgetService.GetAllTransactionsAsync()).ToList();
            var paycheckTransactions = allPaycheckTransactions.ToList();

            #region Massage paycheck transaction date
            // Whatever date the paycheck may have come in on, for purposes of this projection, it came in on its expected date.
            // So that it can be attributed to the pay period.
            foreach (var allPaycheckTransaction in paycheckTransactions) {
                if (allPaycheckTransaction.PaycheckOccurrenceDate != null && allPaycheckTransaction.TransactionDate !=
                    allPaycheckTransaction.PaycheckOccurrenceDate) {
                    allPaycheckTransaction.TransactionDate = allPaycheckTransaction.PaycheckOccurrenceDate.Value;
                }
            }

            allTransactions.Where(x => x.PaycheckId != null).ToList().ForEach(x => {
                if (x.PaycheckOccurrenceDate != null && x.TransactionDate != x.PaycheckOccurrenceDate) {
                    x.TransactionDate = x.PaycheckOccurrenceDate.Value;
                }
            });
            #endregion

            // Run Projection Engine (Standard)
            var results = _projectionEngine.CalculateProjections(
                paycheckTransactions,
                allBillTransactions.ToList(),
                allBucketTransactions.ToList(),
                allTransactions,
                start, end, accounts.ToList(), paychecks.ToList(), bills.ToList(), buckets.ToList(),
                periodBills.ToList(), periodBuckets.ToList(), transactions.ToList(), reconciliations?.ToList(),
                showReconciled, true, UseAutoSweep, null);

            var list = results.ToList();

            // Run Projection Engine (Snowball)
            var snowballOptions = SnowballOptions;
            var snowballResults = _projectionEngine.CalculateProjections(
                paycheckTransactions,
                allBillTransactions.ToList(),
                allBucketTransactions.ToList(),
                allTransactions,
                start, end, accounts.ToList(), paychecks.ToList(), bills.ToList(), buckets.ToList(),
                periodBills.ToList(), periodBuckets.ToList(), transactions.ToList(), reconciliations?.ToList(),
                showReconciled, true, UseAutoSweep, snowballOptions);

            var snowballList = snowballResults.ToList();

            // Check for negative checking/savings accounts
            var negAccounts = new HashSet<string>();
            foreach (var item in list) {
                foreach (var acc in accounts) {
                    if (acc.Type is not (AccountType.Checking or AccountType.Savings)) continue;
                    if (item.AccountBalances.TryGetValue(acc.Name, out decimal balance) && balance < 0) {
                        negAccounts.Add(acc.Name);
                    }
                }
            }

            return (list, snowballList, negAccounts);
        });

        // 2. BACK ON UI THREAD: Safely update bound collections and show toasts!
        Projections = new ObservableCollection<ProjectionItem>(resultList);
        SnowballProjections = new ObservableCollection<ProjectionItem>(snowballList);

        if (negativeAccounts.Any()) {
            string message = $"Warning: The following accounts go negative in the projection: {string.Join(", ", negativeAccounts)}";
            ShowToast(message);
        }
    }
    catch (Exception ex) {
        Log.Error(ex, "Error calculating projections.");
        ShowToast("Failed to calculate projections. Check logs.");
    }
    finally {
        _isCalculatingProjections = false;
        IsProjecting = false;
        IsSnowballProjecting = false;
    }
}

    public void ShowToast(string message) {
        Application.Current.Dispatcher.Invoke(() => {
            // Avoid duplicate toasts with the same message
            if (Toasts.Any(t => t.Message == message)) return;

            var toast = new ToastViewModel(message,
                t => { Application.Current.Dispatcher.Invoke(() => Toasts.Remove(t)); });
            Toasts.Add(toast);
        });
    }

    public List<PeriodBill> GetProjectedBillsForPeriod(DateTime periodStart) {
        try {
            var periodEnd = periodStart.AddDays(14); // Default
            if (ShowByMonth) {
                periodEnd = periodStart.AddMonths(1);
            }
            else {
                var allPaycheckDates = new List<DateTime>();
                foreach (var pay in Paychecks) {
                    var nextPay = pay.StartDate;
                    while (nextPay < periodStart.AddYears(1)) {
                        if (nextPay > periodStart) {
                            allPaycheckDates.Add(nextPay);
                            break;
                        }

                        nextPay = pay.Frequency switch {
                            Frequency.Weekly => nextPay.AddDays(7),
                            Frequency.BiWeekly => nextPay.AddDays(14),
                            Frequency.Monthly => nextPay.AddMonths(1),
                            _ => nextPay.AddYears(100)
                        };
                    }
                }

                if (allPaycheckDates.Any()) periodEnd = allPaycheckDates.Min();
            }

            var result = new List<PeriodBill>();
            foreach (var bill in Bills) {
                DateTime nextDue;
                if (bill.NextDueDate.HasValue) {
                    nextDue = bill.NextDueDate.Value;
                    while (nextDue < periodStart) {
                        nextDue = bill.Frequency switch {
                            Frequency.Monthly => nextDue.AddMonths(1),
                            Frequency.Yearly => nextDue.AddYears(1),
                            Frequency.Weekly => nextDue.AddDays(7),
                            Frequency.BiWeekly => nextDue.AddDays(14),
                            _ => nextDue.AddYears(100)
                        };
                    }
                }
                else {
                    nextDue = new DateTime(periodStart.Year, periodStart.Month,
                        Math.Min(bill.DueDay, DateTime.DaysInMonth(periodStart.Year, periodStart.Month)));
                    if (nextDue < periodStart) nextDue = nextDue.AddMonths(1);
                }

                while (nextDue < periodEnd) {
                    if (nextDue >= periodStart) {
                        result.Add(new PeriodBill {
                            BillId = bill.Id,
                            BillName = bill.Name,
                            PeriodDate = periodStart,
                            DueDate = nextDue,
                            ActualAmount = bill.ExpectedAmount,
                            IsPaid = false
                        });
                    }

                    nextDue = bill.Frequency switch {
                        Frequency.Monthly => nextDue.AddMonths(1),
                        Frequency.Yearly => nextDue.AddYears(1),
                        Frequency.Weekly => nextDue.AddDays(7),
                        Frequency.BiWeekly => nextDue.AddDays(14),
                        _ => nextDue.AddYears(100)
                    };
                }
            }

            return result;
        }
        catch (Exception ex) {
            Log.Error(ex, "Error getting projected bills for period starting {PeriodStart}.", periodStart);
            return new List<PeriodBill>();
        }
    }

    private async Task LoadDataAsync() {
        Log.Information("Loading all budget data.");
        _isLoadingData = true;
        try {
            await Task.Yield();
            await LoadAccountDataAsync();
            await Task.Yield();

            await LoadBillDataAsync();
            await Task.Yield();

            await LoadPaycheckDataAsync();
            await Task.Yield();

            await LoadBucketDataAsync();

            // Re-trigger Accounts collection change to update chart
            OnPropertyChanged(nameof(Accounts));
            
            //UpdateProjectionColumns(Accounts);
            
            Log.Information("Budget data loaded successfully. Accounts: {AccountCount}, Bills: {BillCount}",
                Accounts.Count, Bills.Count);
        }
        catch (Exception ex) {
            Log.Error(ex, "Failed to load budget data.");
            MessageBox.Show("Failed to load budget data. See log for details.", "Error", MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally {
            _isLoadingData = false;
        }
    }

    private async Task LoadAccountDataAsync() {
        Log.Information("Loading account data.");
        _isLoadingAccountData = true;
        try {
            var accounts = (await _budgetService.GetAllAccountsAsync()).ToList();
            if (accounts.All(a => !(a.Name == "Household Cash" && a.Type == AccountType.Cash))) {
                Log.Information("Household Cash account not found. Creating default.");
                var cashAccount = new Account {
                    Name = "Household Cash",
                    Type = AccountType.Cash,
                    Balance = 0,
                    IncludeInTotal = true
                };
                await _budgetService.UpsertAccountAsync(cashAccount);
                accounts = (await _budgetService.GetAllAccountsAsync()).ToList();
            }

            var accountBalances = (await _budgetService.GetAllAccountsAsOfAsync(DateTime.Now)).ToList();
            accounts = accounts.OrderBy(b => b.Name).ToList();
            foreach (var a in accounts) {
                a.Balance = accountBalances.SingleOrDefault(b => b.Id == a.Id)?.Balance ?? 0;
            }

            foreach (var a in accounts) a.PropertyChanged += Item_PropertyChanged;
            Accounts = new ObservableCollection<Account>(accounts);

            var accountsWithNone = new List<Account> { new Account { Id = 0, Name = "(None)" } };
            accountsWithNone.AddRange(accounts);
            AccountsWithNone = new ObservableCollection<Account>(accountsWithNone);

            if (Accounts.Any(x => x.Type == AccountType.Checking && x.IsPrimary) && Accounts.Any(x => x.Type == AccountType.CreditCard)) {
                UseAutoSweep = true;
                OnPropertyChanged(nameof(UseAutoSweep));
            }
            Log.Information("Account data loaded successfully. Accounts: {AccountCount}",
                Accounts.Count);
        }
        catch (Exception ex) {
            Log.Error(ex, "Failed to load account data.");
            MessageBox.Show("Failed to load account data. See log for details.", "Error", MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally {
            _isLoadingAccountData = false;
        }
    }

    private async Task LoadBillDataAsync() {
        Log.Information("Loading bill data.");
        _isLoadingBillData = true;
        try {
            var bills = await _budgetService.GetAllBillsAsync();
            bills = bills.OrderBy(b => b.DueDay).ThenBy(b => b.Name).ToList();
            foreach (var b in bills) b.PropertyChanged += Item_PropertyChanged;
            Bills = new ObservableCollection<Bill>(bills);

            var billsWithNone = new List<Bill> { new Bill { Id = 0, Name = "(None)" } };
            billsWithNone.AddRange(bills);
            BillsWithNone = new ObservableCollection<Bill>(billsWithNone);

            Log.Information("Bill data loaded successfully. Bills: {BillCount}",
                Bills.Count);
        }
        catch (Exception ex) {
            Log.Error(ex, "Failed to load bill data.");
            MessageBox.Show("Failed to load bill data. See log for details.", "Error", MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally {
            _isLoadingBillData = false;
        }
    }

    private async Task LoadBucketDataAsync() {
        Log.Information("Loading all bucket data.");
        _isLoadingBucketData = true;
        try {
            var buckets = await _budgetService.GetAllBucketsAsync();
            buckets = buckets.OrderBy(b => b.Name).ToList();
            foreach (var b in buckets) b.PropertyChanged += Item_PropertyChanged;
            Buckets = new ObservableCollection<BudgetBucket>(buckets);

            var bucketsWithNone = new List<BudgetBucket> { new BudgetBucket { Id = 0, Name = "(None)" } };
            bucketsWithNone.AddRange(buckets);
            BucketsWithNone = new ObservableCollection<BudgetBucket>(bucketsWithNone);

            Log.Information("Bucket data loaded successfully. Accounts: {BucketCount}",
                Buckets.Count);
        }
        catch (Exception ex) {
            Log.Error(ex, "Failed to load bucket data.");
            MessageBox.Show("Failed to load bucket data. See log for details.", "Error", MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally {
            _isLoadingBucketData = false;
        }
    }

    private async Task LoadPaycheckDataAsync() {
        Log.Information("Loading Paycheck data.");
        _isLoadingPaycheckData = true;
        try {
            var paychecks = await _budgetService.GetAllPaychecksAsync();
            paychecks = paychecks.OrderBy(b => b.Name).ToList();
            foreach (var p in paychecks) p.PropertyChanged += Item_PropertyChanged;
            Paychecks = new ObservableCollection<Paycheck>(paychecks);

            var paychecksWithNone = new List<Paycheck> { new Paycheck { Id = 0, Name = "(None)" } };
            paychecksWithNone.AddRange(paychecks);
            PaychecksWithNone = new ObservableCollection<Paycheck>(paychecksWithNone);

            Log.Information("Paycheck data loaded successfully. Paychecks: {PaycheckCount}",
                Paychecks.Count);
        }
        catch (Exception ex) {
            Log.Error(ex, "Failed to load Paycheck data.");
            MessageBox.Show("Failed to load Paycheck data. See log for details.", "Error", MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally {
            _isLoadingPaycheckData = false;
        }
    }

    private void LoadPaychecks() {
        try {
            var allPaychecks = Paychecks.ToList();
            if (allPaychecks.Count == 0) {
                CurrentPeriodDate = DateTime.Today;
                return;
            }

            PeriodPaychecks = new ObservableCollection<Paycheck>(allPaychecks);

            SetCurrentPeriodDate();
        }
        catch (Exception ex) {
            Log.Error(ex, "Error loading paychecks into period view.");
        }
    }

    private async Task LoadPeriodDataAsync() {
        try {
            await LoadPeriodBillsAsync();
            await LoadPeriodBucketsAsync();
            await LoadPeriodTransactionsAsync();
            ApplyTransactionAmounts();
            UpdateWarningMetrics();
            UpdateBucketWarningMetrics();
        }
        catch (Exception ex) {
            Log.Error(ex, "Error loading period data.");
        }
    }

    private void ApplyTransactionAmounts() {
        try {
            foreach (var pb in CurrentPeriodBills) {
                pb.TransactionAmount = CurrentPeriodTransactions
                    .Where(t => t.BillId == pb.BillId)
                    .Sum(t => t.Amount);
            }

            foreach (var pb in CurrentPeriodBuckets) {
                pb.TransactionAmount = CurrentPeriodTransactions
                    .Where(t => t.BucketId == pb.BucketId)
                    .Sum(t => t.Amount);
            }
        }
        catch (Exception ex) {
            Log.Error(ex, "Error applying transaction amounts to period items.");
        }
    }

    private async Task LoadPeriodBillsAsync() {
        try {
            var pBills = (await _budgetService.GetPeriodBillsAsync(CurrentPeriodDate)).ToList();
            pBills = pBills.OrderBy(pb => pb.DueDate).ToList();
            // Always ensure projected bills for this period are in the database and collection
            var projectedBillsForPeriod = GetProjectedBillsForPeriod(CurrentPeriodDate);

            foreach (var pb in projectedBillsForPeriod) {
                if (!pBills.Any(existing =>
                        existing.BillId == pb.BillId && existing.PeriodDate.Date == pb.PeriodDate.Date)) { }
                else {
                    var periodBill = pBills.SingleOrDefault(existing =>
                        existing.BillId == pb.BillId && existing.PeriodDate.Date == pb.PeriodDate.Date);
                    UpdatePeriodBillFromClone(pb, periodBill!);
                }
            }

            projectedBillsForPeriod = projectedBillsForPeriod.OrderBy(pb => pb.DueDate).ToList();

            CurrentPeriodBills = new ObservableCollection<PeriodBill>(projectedBillsForPeriod);
            foreach (var pb in CurrentPeriodBills) pb.PropertyChanged += PeriodBill_PropertyChanged;
            UpdateWarningMetrics();
        }
        catch (Exception ex) {
            Log.Error(ex, "Error loading period bills.");
        }
    }

    private async Task LoadPeriodBucketsAsync() {
        try {
            var pBuckets = (await _budgetService.GetPeriodBucketsIncludingMonthlyAsync(CurrentPeriodDate)).ToList();

            foreach (var bucket in Buckets.Where(b =>
                         b.PaycheckId == null || (b.PaycheckId == SelectedPeriodPaycheckId && !ShowByMonth))) {
                if (pBuckets.All(existing => existing.BucketId != bucket.Id)) {
                    var pb = new PeriodBucket {
                        BucketId = bucket.Id,
                        BucketName = bucket.Name,
                        PeriodDate = bucket.PaycheckId == null
                            ? new DateTime(CurrentPeriodDate.Year, CurrentPeriodDate.Month, 1)
                            : CurrentPeriodDate,
                        ActualAmount = bucket.ExpectedAmount,
                        IsPaid = false,
                        FitId = Guid.NewGuid()
                    };
                    pBuckets.Add(pb);
                }
            }

            CurrentPeriodBuckets = new ObservableCollection<PeriodBucket>(pBuckets);
            foreach (var pb in CurrentPeriodBuckets) pb.PropertyChanged += PeriodBucket_PropertyChanged;
        }
        catch (Exception ex) {
            Log.Error(ex, "Error loading period buckets.");
        }
    }

    private DateTime GetNextPeriodDate(DateTime currentPeriodStart) {
        if (ShowByMonth) {
            return currentPeriodStart.AddMonths(1);
        }

        var allPaycheckDates = new List<DateTime>();
        var end = currentPeriodStart.AddYears(1);
        foreach (var pay in Paychecks.Where(p => p.Id == SelectedPeriodPaycheckId)) {
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
        try {
            var nextPeriodDate = GetNextPeriodDate(CurrentPeriodDate);
            var transactions = (await _budgetService.GetTransactionsAsync(CurrentPeriodDate, nextPeriodDate)).ToList();
            transactions = transactions.OrderBy(pb => pb.TransactionDate).ToList();
            CurrentPeriodTransactions.Clear();
            foreach (var tx in transactions) {
                CurrentPeriodTransactions.Add(tx);
            }
        }
        catch (Exception ex) {
            Log.Error(ex, "Error loading period transactions.");
        }
    }

    private void InitializePeriod() {
        try {
            if (ShowByMonth) {
                CurrentPeriodDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                return;
            }

            LoadPaychecks();
        }
        catch (Exception ex) {
            Log.Error(ex, "Error initializing period.");
        }
    }

    private async Task NavigatePeriodAsync(int direction) {
        try {
            if (ShowByMonth) {
                CurrentPeriodDate = CurrentPeriodDate.AddMonths(direction);
                await LoadPeriodDataAsync();
                return;
            }

            var allPaycheckDates = new List<DateTime>();
            var end = DateTime.Today.AddYears(1);
            foreach (var pay in Paychecks.Where(p => p.Id == SelectedPeriodPaycheckId)) {
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
            var currentIndex = sortedDates.FindIndex(d => d.Date == CurrentPeriodDate.Date);

            if (currentIndex == -1) {
                if (direction > 0)
                    CurrentPeriodDate = sortedDates.FirstOrDefault(d => d > CurrentPeriodDate);
                else
                    CurrentPeriodDate = sortedDates.LastOrDefault(d => d < CurrentPeriodDate);
            }
            else {
                int nextIndex = currentIndex + direction;
                if (nextIndex >= 0 && nextIndex < sortedDates.Count)
                    CurrentPeriodDate = sortedDates[nextIndex];
            }

            await LoadPeriodDataAsync();
        }
        catch (Exception ex) {
            Log.Error(ex, "Error navigating period.");
        }
    }

    private async Task ReconcileAccountAsync() {
        if (EditingAccountClone == null) return;
        try {
            var window = new ReconciliationWindow(EditingAccountClone, _budgetService) {
                Owner = Application.Current.MainWindow
            };
            window.ShowDialog();
            await LoadAccountDataAsync();
            await LoadPeriodDataAsync();
            await CalculateProjectionsAsync();
        }
        catch (Exception ex) {
            Log.Error(ex, "Error showing reconciliation window.");
            MessageBox.Show("Failed to open reconciliation window. See log for details.", "Error", MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async Task ImportAccountAsync() {
        if (EditingAccountClone == null) return;
        try {
            var window = new ImportReconciliationWindow(EditingAccountClone, _budgetService) {
                Owner = Application.Current.MainWindow
            };
            window.ShowDialog();
            await LoadAccountDataAsync();
            await LoadPeriodDataAsync();
            await CalculateProjectionsAsync();
        }
        catch (Exception ex) {
            Log.Error(ex, "Error showing import window.");
            MessageBox.Show("Failed to open import window. See log for details.", "Error", MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async Task SetAccountAprRatesAsync() {
        if (EditingAccountClone is not { Type: AccountType.CreditCard }) return;
        try {
            EditingAccountClone.AccountAprHistory ??= [];
            var window = new AccountAprHistoryWindow(EditingAccountClone, _budgetService) {
                Owner = Application.Current.MainWindow
            };
            window.ShowDialog();
            await CalculateProjectionsAsync();
        }
        catch (Exception ex) {
            Log.Error(ex, "Error showing APR history window.");
            MessageBox.Show("Failed to open interest rate window. See log for details.", "Error", MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }


    private void RefreshPaychecks() {
        try {
            var allPaychecks = Paychecks.ToList();
            if (allPaychecks.Count == 0) {
                CurrentPeriodDate = DateTime.Today;
                return;
            }

            PeriodPaychecks = new ObservableCollection<Paycheck>(allPaychecks);
        }
        catch (Exception ex) {
            Log.Error(ex, "Error refreshing paychecks list.");
        }
    }

    private void SetCurrentPeriodDate(int? id = null) {
        try {
            var allPaychecks = Paychecks.ToList();
            if (allPaychecks.Count == 0) {
                CurrentPeriodDate = DateTime.Today;
                return;
            }

            DateTime latestPayBeforeToday = DateTime.MinValue;
            foreach (var pay in allPaychecks.Where(p => id == null || p.Id == id)) {
                var nextPay = pay.StartDate;
                while (nextPay <= DateTime.Today.AddDays(1)) {
                    if (nextPay <= DateTime.Today && nextPay > latestPayBeforeToday)
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

            if (id == null && currentPeriodPaychecks.Any()) {
                _selectedPeriodPaycheckId = currentPeriodPaychecks.First().Id;
                OnPropertyChanged(nameof(SelectedPeriodPaycheckId));
            }
        }
        catch (Exception ex) {
            Log.Error(ex, "Error setting current period date.");
        }
    }

    private void ShowAbout() {
        try {
            var about = new AboutWindow {
                Owner = Application.Current.MainWindow
            };
            about.ShowDialog();
        }
        catch (Exception ex) {
            Log.Error(ex, "Error showing about window.");
        }
    }

    private void Exit() {
        try {
            Application.Current.Shutdown();
        }
        catch (Exception ex) {
            Log.Error(ex, "Error during exit.");
        }
    }

    private void Backup() {
        try {
            var file = _budgetService.BackupDatabase();
            MessageBox.Show($"Database backup saved successfully to {file}", "Success", MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex) {
            MessageBox.Show(ex.Message, "Backup Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ShowAmortization(Account account) {
        try {
            var amortization = new AmortizationWindow(account) {
                Owner = Application.Current.MainWindow
            };
            amortization.ShowDialog();
        }
        catch (Exception ex) {
            Log.Error(ex, "Error showing amortization window.");
        }
    }

    #endregion
}