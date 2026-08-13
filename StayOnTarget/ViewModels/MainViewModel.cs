using Serilog;
using StayOnTarget.Models;
using StayOnTarget.Services;
using StayOnTarget.Services.Projections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Media;
using System.Windows;
using System.Windows.Data;
using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json;
using StayOnTarget.Helpers;
using StayOnTarget.Views;

namespace StayOnTarget.ViewModels;

public class MainViewModel : ViewModelBase {
    private readonly BudgetService _budgetService;
    private readonly ReconciliationService _reconciliationService;
    private readonly IProjectionEngine _projectionEngine;
    private RangeObservableCollection<PeriodBill> _currentPeriodBills = new();
    private RangeObservableCollection<PeriodBucket> _currentPeriodBuckets = new();
    private int _pastDueCount;
    private int _upcomingCount;
    private int _budgetExceededCount;
    private int _envelopeNearingFullCount;
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
    private bool _isBillDescriptionExpanded;
    private bool _isBucketDescriptionExpanded;
    private Bill? _editingBillClone;
    private PeriodBill? _editingPeriodBillClone;
    private BudgetBucket? _editingBucketClone;
    private PeriodBucket? _editingPeriodBucketClone;
    private Account? _editingAccountClone;
    private Transaction? _editingTransactionClone;
    private bool _isEditingTransactionEnabled = true;
    private Paycheck? _editingPaycheckClone;
    private DateTime _currentPeriodDate = DateTime.MinValue;
    private bool _showByMonth;
    private int _selectedPeriodPaycheckId;
    private ObservableCollection<ToastViewModel> _toasts = new();
    private bool _isEditingPaycheck;
    private Paycheck? _selectedPaycheck;
    private string _toggleReconciliationText = "Show Reconciled";
    private DateTime _projectionEndDate = DateTime.Today.AddYears(1);
    private DateTime? _projectionStartDate;
    private int _selectedOuterTabIndex;
    private int _selectedInnerTabIndex;
    private int _selectedProjectionTabIndex;
    private SnowballStrategyOptions _snowballOptions = new();
    private NavigationItemViewModel? _selectedNavigationItem;

    #region Properties

    public ObservableCollection<NavigationItemViewModel> NavigationItems { get; } = new();

    public NavigationItemViewModel? SelectedNavigationItem {
        get => _selectedNavigationItem;
        set {
            if (SetProperty(ref _selectedNavigationItem, value) && value != null) {
                SelectedOuterTabIndex = value.TabIndex;
            }
        }
    }

    public IEnumerable<TargetFrequencyType> TargetFrequencyTypes =>
        Enum.GetValues(typeof(TargetFrequencyType)).Cast<TargetFrequencyType>();

    public IEnumerable<BucketType> BucketTypes => Enum.GetValues(typeof(BucketType)).Cast<BucketType>();

    public SnowballStrategyOptions SnowballOptions {
        get => _snowballOptions;
        set {
            if (_snowballOptions != null) {
                _snowballOptions.PropertyChanged -= OnSnowballOptionsPropertyChanged;
            }

            if (SetProperty(ref _snowballOptions, value)) {
                if (_snowballOptions != null) {
                    _snowballOptions.PropertyChanged += OnSnowballOptionsPropertyChanged;
                }

                RequestProjectionRecalculation();
            }
        }
    }

    private async void OnSnowballOptionsPropertyChanged(object? sender, PropertyChangedEventArgs e) {
        if (_isLoadingData || IsLoading) return;

        // 1. Trigger debounced calculation whenever options change
        RequestProjectionRecalculation();

        // 2. Persist updated options back to the database/settings table
        try {
            var optionsToSerialize = sender as SnowballStrategyOptions ?? this.SnowballOptions;
            if (optionsToSerialize != null) {
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(optionsToSerialize);
                await _budgetService.SaveSettingAsync("SnowballStrategyOptions", json);
            }
        }
        catch (Exception ex) {
            Log.Error(ex, "Failed to save SnowballOptions setting.");
        }
    }

    public RangeObservableCollection<BucketPaycheckAllocation> EditableAllocations { get; }
        = new RangeObservableCollection<BucketPaycheckAllocation>();

    public RangeObservableCollection<SelectableSubCategory> EditableSubCategories { get; }
        = new RangeObservableCollection<SelectableSubCategory>();

    public RangeObservableCollection<ProjectionItem> SnowballProjections { get; } = new();

    public bool IsBucketDescriptionExpanded {
        get => _isBucketDescriptionExpanded;
        set => SetProperty(ref _isBucketDescriptionExpanded, value);
    }

    public bool IsBillDescriptionExpanded {
        get => _isBillDescriptionExpanded;
        set => SetProperty(ref _isBillDescriptionExpanded, value);
    }

    private bool _isDarkMode;

    public bool IsDarkMode {
        get => _isDarkMode;
        set => SetProperty(ref _isDarkMode, value);
    }

    public static MainViewModel? Instance { get; private set; }

    public MainViewModel(
        BudgetService budgetService,
        ReconciliationService reconciliationService) {
        Instance = this;
        _budgetService = budgetService;
        _reconciliationService = reconciliationService;
        _projectionEngine = new ProjectionEngine();

        // InitializeDataAsync handles loading from budgetService and attaching the listener.
        ImportAccountCommand = new AsyncRelayCommand(ImportAccountAsync, () => CanEditAccount);
        ReconcileAccountCommand =
            new AsyncRelayCommand(ReconcileAccountAsync, () => CanEditAccount);

        SetAccountAprRatesCommand =
            new AsyncRelayCommand(SetAccountAprRatesAsync, () => IsEditingAccount);

        NextPeriodCommand = new AsyncRelayCommand(() => NavigatePeriodAsync(1));
        PrevPeriodCommand = new AsyncRelayCommand(() => NavigatePeriodAsync(-1));

        AddBillCommand = new RelayCommand(AddBill, () => IsNotEditingBill);
        EditBillCommand = new RelayCommand(EditBill, () => CanEditBill);
        CancelBillCommand = new RelayCommand(CancelBill, () => IsEditingBill);
        SaveBillCommand = new AsyncRelayCommand(SaveBillAsync, () => IsEditingBill);
        DeleteBillCommand = new AsyncRelayCommand(DeleteBillAsync, () => IsEditingBill);

        EditPeriodBillCommand = new RelayCommand(EditPeriodBill, () => CanEditPeriodBill);
        CancelPeriodBillCommand = new RelayCommand(CancelPeriodBill, () => IsEditingPeriodBill);
        SavePeriodBillCommand =
            new AsyncRelayCommand(SavePeriodBillAsync, () => IsEditingPeriodBill);
        DeletePeriodBillCommand =
            new AsyncRelayCommand(DeletePeriodBillAsync, () => IsEditingPeriodBill);

        AddBucketCommand = new RelayCommand(AddBucket, () => IsNotEditingBucket);
        EditBucketCommand = new RelayCommand(EditBucket, () => CanEditBucket);
        CancelBucketCommand = new RelayCommand(CancelBucket, () => IsEditingBucket);
        SaveBucketCommand = new AsyncRelayCommand(SaveBucketAsync, () => IsEditingBucket);
        DeleteBucketCommand = new AsyncRelayCommand(DeleteBucketAsync);

        AddSubCategoryCommand = new RelayCommand(AddSubCategory, () => IsNotEditingSubCategory);
        EditSubCategoryCommand = new RelayCommand(EditSubCategory, () => CanEditSubCategory);
        CancelSubCategoryCommand = new RelayCommand(CancelSubCategory, () => IsEditingSubCategory);
        SaveSubCategoryCommand = new AsyncRelayCommand(SaveSubCategoryAsync, () => IsEditingSubCategory);
        DeleteSubCategoryCommand = new AsyncRelayCommand(DeleteSubCategoryAsync);


        AddCategoryCommand = new RelayCommand(AddCategory, () => IsNotEditingCategory);
        EditCategoryCommand = new RelayCommand(EditCategory, () => CanEditCategory);
        CancelCategoryCommand = new RelayCommand(CancelCategory, () => IsEditingCategory);
        SaveCategoryCommand = new AsyncRelayCommand(SaveCategoryAsync, () => IsEditingCategory);
        DeleteCategoryCommand = new AsyncRelayCommand(DeleteCategoryAsync);

        EditPeriodBucketCommand = new RelayCommand(EditPeriodBucket, () => CanEditPeriodBucket);
        CancelPeriodBucketCommand =
            new RelayCommand(CancelPeriodBucket, () => IsEditingPeriodBucket);
        SavePeriodBucketCommand =
            new AsyncRelayCommand(SavePeriodBucketAsync, () => IsEditingPeriodBucket);
        DeletePeriodBucketCommand =
            new AsyncRelayCommand(DeletePeriodBucketAsync, () => IsEditingPeriodBucket);

        AddTransactionCommand = new RelayCommand(AddTransaction, () => IsNotEditingTransaction);
        EditTransactionCommand = new RelayCommand(EditTransaction, () => CanEditTransaction);
        CancelTransactionCommand = new RelayCommand(CancelTransaction, () => IsEditingTransaction);
        SaveTransactionCommand =
            new AsyncRelayCommand(_ => SaveTransactionAsync(), () => IsEditingTransaction);
        DeleteTransactionCommand =
            new AsyncRelayCommand(DeleteTransactionAsync, () => IsEditingTransaction);

        AddPaycheckCommand = new RelayCommand(AddPaycheck);
        EditPaycheckCommand = new RelayCommand(EditPaycheck, () => CanEditPaycheck);
        CancelPaycheckCommand = new RelayCommand(CancelPaycheck, () => IsEditingPaycheck);
        SavePaycheckCommand = new AsyncRelayCommand(SavePaycheckAsync, () => IsEditingPaycheck);
        DeletePaycheckCommand =
            new AsyncRelayCommand(DeletePaycheckAsync, () => IsEditingPaycheck);

        AddAccountCommand = new RelayCommand(AddAccount, () => IsNotEditingAccount);
        EditAccountCommand = new RelayCommand(EditAccount, () => CanEditAccount);
        CancelAccountCommand = new RelayCommand(CancelAccount, () => IsEditingAccount);
        SaveAccountCommand = new AsyncRelayCommand(SaveAccountAsync, () => IsEditingAccount);
        DeleteAccountCommand = new AsyncRelayCommand(DeleteAccountAsync, () => IsEditingAccount);

        ShowAmortizationCommand =
            new RelayCommand<Account>(a => ShowAmortization(a as Account ?? throw new InvalidOperationException()));
        ShowAboutCommand = new RelayCommand(ShowAbout);
        SetThemeCommand = new RelayCommand(ToggleTheme);
        ExitCommand = new RelayCommand(Exit);
        BackupCommand = new RelayCommand(Backup);
        SetOneYearCommand = new RelayCommand(() => SetProjectionEndDate(1));
        SetFiveYearCommand = new RelayCommand(() => SetProjectionEndDate(5));
        SetTenYearCommand = new RelayCommand(() => SetProjectionEndDate(10));
        SetThirtyYearCommand = new RelayCommand(() => SetProjectionEndDate(30));

        PayBillCommand = new AsyncRelayCommand<ProjectionItem>(PayBillAsync);
        PayPeriodBillCommand = new AsyncRelayCommand<PeriodBill>(PayPeriodBillAsync);
        FundEnvelopeCommand = new AsyncRelayCommand<ProjectionItem>(FundEnvelopeAsync);
        SkipFundEnvelopeCommand = new AsyncRelayCommand<ProjectionItem>(SkipFundEnvelopeAsync);
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
        ExportTransactionsCommand = new RelayCommand(ExportTransactions);

        InitializeDataCommand = new AsyncRelayCommand(InitializeDataAsync);

        InitializeNavigationMenu();

        // Initialize commands directly in the constructor
        OpenManageExcludedAccountsCommand = new RelayCommand(OpenManageExcludedAccounts);
        CloseManageExcludedAccountsCommand = new RelayCommand(CloseManageExcludedAccounts);
        ToggleAccountExclusionCommand = new RelayCommand<int>(ToggleAccountExclusion);

        _filteredBillsView = CollectionViewSource.GetDefaultView(BillsWithNone);
        _filteredBillsView.Filter = FilterBillItem;
    }

    public void ToggleTheme() {
        IsDarkMode = !IsDarkMode;
        SetTheme(IsDarkMode);
    }

    private CancellationTokenSource? _recalculationCts;

    // <summary>
    /// Schedules a projection recalculation after a short delay. 
    /// Restarts the timer if called again before the delay expires.
    /// </summary>
    public async void RequestProjectionRecalculation() {
// 1. Cancel the previous pending delay/calculation

        if (_recalculationCts != null) {
            _recalculationCts.Cancel();
            _recalculationCts.Dispose();
        }

        _recalculationCts = new CancellationTokenSource();

        var token = _recalculationCts.Token;

        // 2. Fire-and-forget the debounced async runner
        _ = RunDebouncedProjectionsAsync(token);
    }

    private async Task RunDebouncedProjectionsAsync(CancellationToken cancellationToken) {
        try {
            // Wait 350ms for user to stop typing
            await Task.Delay(350, cancellationToken);

            // Check cancellation before hitting heavy math
            if (cancellationToken.IsCancellationRequested) return;

            // Run your existing async calculation method
            await CalculateProjectionsAsync(cancellationToken);
        }
        catch (OperationCanceledException) {
            // Expected when user types faster than 350ms delay
        }
    }


    public IRelayCommand InitializeDataCommand { get; }
    public IRelayCommand ExportTransactionsCommand { get; }

    public IAsyncRelayCommand<ProjectionItem> PayBillCommand { get; }
    public IAsyncRelayCommand<ProjectionItem> FundEnvelopeCommand { get; }
    public IAsyncRelayCommand<ProjectionItem> SkipFundEnvelopeCommand { get; }

    public IAsyncRelayCommand<PeriodBill> PayPeriodBillCommand { get; }

    private async Task InitializeDataAsync() {
        // Force the dispatcher to render the empty screen/loading state first

        await Task.Yield();

        IsLoading = true;
        IsGatheringData = true;
        IsProjecting = true;
        await Task.Yield();

        try {
            // 1. Load Snowball Options & attach PropertyChanged handler
            await LoadSnowballOptionsAsync();

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

    private async Task LoadSnowballOptionsAsync() {
        // Unhook previous listener if re-initializing
        if (SnowballOptions != null) {
            SnowballOptions.PropertyChanged -= OnSnowballOptionsPropertyChanged;
        }

        var json = await _budgetService.GetSettingAsync("SnowballStrategyOptions");

        if (!string.IsNullOrWhiteSpace(json)) {
            try {
                var options = JsonConvert.DeserializeObject<SnowballStrategyOptions>(json);
                if (options != null) {
                    SnowballOptions = options;
                }
            }
            catch (Exception ex) {
                Log.Error(ex, "Failed to deserialize SnowballOptions setting.");
            }
        }

        // Ensure we have a valid instance
        SnowballOptions ??= new SnowballStrategyOptions();

        // Hook up the debouncing listener
        SnowballOptions.PropertyChanged += OnSnowballOptionsPropertyChanged;
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

    private string _snowballAnalysisText = string.Empty;

    public string SnowballAnalysisText {
        get => _snowballAnalysisText;
        set => SetProperty(ref _snowballAnalysisText, value);
    }

    private DateTime? _snowballDebtFreeDate;

    public DateTime? SnowballDebtFreeDate {
        get => _snowballDebtFreeDate;
        set => SetProperty(ref _snowballDebtFreeDate, value);
    }

    private int _snowballMonthsSaved;

    public int SnowballMonthsSaved {
        get => _snowballMonthsSaved;
        set => SetProperty(ref _snowballMonthsSaved, value);
    }

    private decimal _snowballFinalNetWorth;

    public decimal SnowballFinalNetWorth {
        get => _snowballFinalNetWorth;
        set => SetProperty(ref _snowballFinalNetWorth, value);
    }

    private decimal _snowballNetWorthImprovement;

    public decimal SnowballNetWorthImprovement {
        get => _snowballNetWorthImprovement;
        set => SetProperty(ref _snowballNetWorthImprovement, value);
    }

    private decimal _snowballFinalDebt;

    public decimal SnowballFinalDebt {
        get => _snowballFinalDebt;
        set => SetProperty(ref _snowballFinalDebt, value);
    }

    private decimal _snowballDebtReductionVsStandard;

    public decimal SnowballDebtReductionVsStandard {
        get => _snowballDebtReductionVsStandard;
        set => SetProperty(ref _snowballDebtReductionVsStandard, value);
    }

    private bool _showSnowballAnalysis;

    public bool ShowSnowballAnalysis {
        get => _showSnowballAnalysis;
        set => SetProperty(ref _showSnowballAnalysis, value);
    }

    public RangeObservableCollection<Bill> Bills { get; } = new();


    public RangeObservableCollection<Paycheck> Paychecks { get; } = new();

    public RangeObservableCollection<Paycheck> PaychecksWithNone { get; } = new();

    private RangeObservableCollection<Account> _accounts = new();

    public RangeObservableCollection<Account> Accounts {
        get => _accounts;
        set {
            // Unhook previous non-null collection
            _accounts.CollectionChanged -= OnAccountsCollectionChanged;

            if (SetProperty(ref _accounts, value)) {
                // Hook up new non-null collection
                _accounts.CollectionChanged += OnAccountsCollectionChanged;
                RefreshExcludableAccounts();
            }
            else {
                // Re-hook if SetProperty returned false (value was identical)
                _accounts.CollectionChanged += OnAccountsCollectionChanged;
            }
        }
    }

    public RangeObservableCollection<Account> VisibleAccounts { get; } = new();

    public AccountType[] AccountTypes => (AccountType[])Enum.GetValues(typeof(AccountType));

    public RangeObservableCollection<Account> ActiveAccountsWithNone { get; } = new();

    public RangeObservableCollection<Account> AccountsWithNone { get; } = new();

    public RangeObservableCollection<Bill> BillsWithNone { get; } = new();

    public RangeObservableCollection<BudgetBucket> BucketsWithNone { get; } = new();

    public RangeObservableCollection<SubCategory> SubCategories { get; } = new();

    private SubCategory? _selectedSubCategory;
    private SubCategory? _editingSubCategoryClone;
    private bool _isEditingSubCategory;
    private Category? _selectedCategory;
    private Category? _editingCategoryClone;
    private bool _isEditingCategory;

    public RangeObservableCollection<Category> Categories { get; }
        = new RangeObservableCollection<Category>();

    public RangeObservableCollection<Category> CategoriesWithNone { get; }
        = new RangeObservableCollection<Category>();

    public SubCategory? SelectedSubCategory {
        get => _selectedSubCategory;
        set {
            if (SetProperty(ref _selectedSubCategory, value)) {
                OnPropertyChanged(nameof(CanEditSubCategory));
                EditSubCategoryCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public SubCategory? EditingSubCategoryClone {
        get => _editingSubCategoryClone;
        set => SetProperty(ref _editingSubCategoryClone, value);
    }

    public bool IsEditingSubCategory {
        get => _isEditingSubCategory;
        set {
            if (SetProperty(ref _isEditingSubCategory, value)) {
                OnPropertyChanged(nameof(IsNotEditingSubCategory));
                OnPropertyChanged(nameof(CanEditSubCategory));
                AddSubCategoryCommand.NotifyCanExecuteChanged();
                EditSubCategoryCommand.NotifyCanExecuteChanged();
                CancelSubCategoryCommand.NotifyCanExecuteChanged();
                SaveSubCategoryCommand.NotifyCanExecuteChanged();
                DeleteSubCategoryCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsNotEditingSubCategory => !IsEditingSubCategory;
    public bool CanEditSubCategory => SelectedSubCategory != null;

// Commands
    public IRelayCommand AddSubCategoryCommand { get; }
    public IRelayCommand EditSubCategoryCommand { get; }
    public IRelayCommand SaveSubCategoryCommand { get; }
    public IRelayCommand CancelSubCategoryCommand { get; }
    public IRelayCommand DeleteSubCategoryCommand { get; }


    // Properties
    public Category? SelectedCategory {
        get => _selectedCategory;
        set {
            if (SetProperty(ref _selectedCategory, value)) {
                OnPropertyChanged(nameof(CanEditCategory));
                EditCategoryCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public Category? EditingCategoryClone {
        get => _editingCategoryClone;
        set => SetProperty(ref _editingCategoryClone, value);
    }

    public bool IsEditingCategory {
        get => _isEditingCategory;
        set {
            if (SetProperty(ref _isEditingCategory, value)) {
                OnPropertyChanged(nameof(IsNotEditingCategory));
                OnPropertyChanged(nameof(CanEditCategory));
                AddCategoryCommand.NotifyCanExecuteChanged();
                EditCategoryCommand.NotifyCanExecuteChanged();
                CancelCategoryCommand.NotifyCanExecuteChanged();
                SaveCategoryCommand.NotifyCanExecuteChanged();
                DeleteCategoryCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsNotEditingCategory => !IsEditingCategory;
    public bool CanEditCategory => SelectedCategory != null;

    // Commands
    public IRelayCommand AddCategoryCommand { get; }
    public IRelayCommand EditCategoryCommand { get; }
    public IRelayCommand SaveCategoryCommand { get; }
    public IRelayCommand CancelCategoryCommand { get; }
    public IRelayCommand DeleteCategoryCommand { get; }

    public RangeObservableCollection<SubCategory> SubCategoriesWithNone { get; } = new();

    public RangeObservableCollection<ProjectionItem> Projections { get; } = new();

    public RangeObservableCollection<PeriodBill> CurrentPeriodBills {
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

    public RangeObservableCollection<PeriodBill> UnpaidPastDueBills { get; } = new();

    private void UpdateWarningMetrics() {
        var today = DateTime.Today;
        var upcomingLimit = today.AddDays(2);

        var pastDue = CurrentPeriodBills.Where(pb => !pb.HasActualAmount && pb.DueDate < today && pb.ActualAmount != 0)
            .ToList();
        var upcoming = CurrentPeriodBills.Where(pb =>
            !pb.HasActualAmount && pb.DueDate >= today && pb.DueDate <= upcomingLimit && pb.ActualAmount != 0).ToList();

        PastDueCount = pastDue.Count;
        UpcomingCount = upcoming.Count;

        var temp = new List<PeriodBill>(pastDue.Count);
        foreach (var b in pastDue) {
            temp.Add(b);
        }

        UnpaidPastDueBills.Clear();
        UnpaidPastDueBills.AddRange(temp);

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

    public RangeObservableCollection<PeriodBucket> BudgetBustedBuckets { get; } = new();

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

            var temp = new List<PeriodBucket>(myList.Count);
            foreach (var b in myList) {
                temp.Add(b);
            }

            BudgetBustedBuckets.Clear();
            BudgetBustedBuckets.AddRange(temp);
        }
        else {
            var temp = new List<PeriodBucket>(exceeded.Count);
            foreach (var b in exceeded) {
                temp.Add(b);
            }

            BudgetBustedBuckets.Clear();
            BudgetBustedBuckets.AddRange(temp);
        }

        OnPropertyChanged(nameof(ShowEnvelopeWarningWidget));
    }

    public bool ShowEnvelopeWarningWidget => BudgetExceededCount > 0 || EnvelopeNearingFullCount > 0;

    #endregion

    public RangeObservableCollection<BudgetBucket> Buckets { get; } = new();

    public RangeObservableCollection<PeriodBucket> CurrentPeriodBuckets {
        get => _currentPeriodBuckets;
        set {
            if (SetProperty(ref _currentPeriodBuckets, value)) {
                UpdateBucketWarningMetrics();
            }
        }
    }

    public RangeObservableCollection<Transaction> CurrentPeriodTransactions { get; } = new();

    public string ToggleReconciliationText {
        get => _toggleReconciliationText;
        set => SetProperty(ref _toggleReconciliationText, value);
    }

    private CancellationTokenSource? _cts;

    private async void OnCalculateProjections() {
        // 1. Immediately turn on the spinner state
        IsProjecting = true;

        // Cancel any pending calculation from a previous rapid date change
        if (_cts != null) {
            _cts.Cancel();
            _cts.Dispose();
        }

        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        try {
            // Force WPF to paint the UI (spinner shows instantly!)
            await Application.Current.Dispatcher.InvokeAsync(() => { },
                System.Windows.Threading.DispatcherPriority.Render);

            // Wait 300ms — if the user changes the date again, this task gets cancelled
            await Task.Delay(300, token);

            await CalculateProjectionsAsync(token);
        }
        catch (OperationCanceledException) {
            // Ignored: User changed date again before 300ms passed
        }
        catch (Exception ex) {
            Log.Error(ex, "Failed to calculate projections.");
        }
    }

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

    public RangeObservableCollection<Paycheck> PeriodPaychecks { get; } = new();

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
        set {
            if (SetProperty(ref _selectedOuterTabIndex, value)) {
                var match = NavigationItems.FirstOrDefault(x => x.TabIndex == value);
                if (match != null && _selectedNavigationItem != match) {
                    _selectedNavigationItem = match;
                    OnPropertyChanged(nameof(SelectedNavigationItem));
                }
            }
        }
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
                ImportAccountCommand.NotifyCanExecuteChanged();
                ReconcileAccountCommand.NotifyCanExecuteChanged();
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

    public bool IsEditingTransactionEnabled {
        get => _isEditingTransactionEnabled;
        private set => SetProperty(ref _isEditingTransactionEnabled, value);
    }

    public RangeObservableCollection<Account> TransactionAccounts { get; } = new();

    public RangeObservableCollection<Account> TransactionToAccounts { get; } = new();

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

    public IRelayCommand SetThemeCommand { get; }

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

    #region Snowball Overlay Support

    private bool _isManageExclusionsOpen;

    public bool IsManageExclusionsOpen {
        get => _isManageExclusionsOpen;
        set => SetProperty(ref _isManageExclusionsOpen, value);
    }

// Filtered list of accounts eligible for exclusion (Liabilities & Investments)
    public RangeObservableCollection<Account> ExcludableAccounts { get; } = new();

    private void OnAccountsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) {
        RefreshExcludableAccounts();
    }

    public void RefreshExcludableAccounts() {
        var filtered = (Accounts
            .Where(a => a.IsLiability || a.Type is AccountType.Brokerage
                or AccountType.Investment
                or AccountType.IRA
                or AccountType.RothIRA)
            .Where(a => !a.IsArchived)).ToList();

        var temp = new List<Account>(filtered.Count);

        foreach (var account in filtered) {
            // Sync the checkbox state from the HashSet
            account.IsExcludedInSnowball = SnowballOptions.ExcludedAccountIds.Contains(account.Id);
            temp.Add(account);
        }

        ExcludableAccounts.Clear();
        ExcludableAccounts.AddRange(temp);
    }

// Commands
    public IRelayCommand OpenManageExcludedAccountsCommand { get; }
    public IRelayCommand CloseManageExcludedAccountsCommand { get; }
    public IRelayCommand ToggleAccountExclusionCommand { get; }

    private void OpenManageExcludedAccounts() {
        IsManageExclusionsOpen = true;
    }

    private void CloseManageExcludedAccounts() {
        IsManageExclusionsOpen = false;
        // Trigger projection recalculation
        OnPropertyChanged(nameof(SnowballOptions));
    }

    private void ToggleAccountExclusion(int accountId) {
        if (SnowballOptions.ExcludedAccountIds.Contains(accountId)) {
            SnowballOptions.ExcludedAccountIds.Remove(accountId);
        }
        else {
            SnowballOptions.ExcludedAccountIds.Add(accountId);
        }

        // Update local account model state for UI
        var acc = ExcludableAccounts.FirstOrDefault(a => a.Id == accountId);
        if (acc != null) {
            acc.IsExcludedInSnowball = SnowballOptions.ExcludedAccountIds.Contains(accountId);
        }

        OnPropertyChanged(nameof(SnowballOptions));
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
#pragma warning disable CS0414 // Field is assigned but its value is never used
    private bool _isLoadingSubCategoryData;
#pragma warning restore CS0414 // Field is assigned but its value is never used
#pragma warning disable CS0414 // Field is assigned but its value is never used
    private bool _isLoadingCategoryData;
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
                    await _budgetService.UpsertBucketAsync(bb, null);
                    break;
            }

            RequestProjectionRecalculation();
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
                ToAccountId = SelectedBill.ToAccountId, NextDueDate = SelectedBill.NextDueDate,
                IsPrincipalOnly = SelectedBill.IsPrincipalOnly,
                Category = SelectedBill.Category, IsActive = SelectedBill.IsActive,
                IsArchived = SelectedBill.IsArchived,
                BucketId = SelectedBill.BucketId,
                SubCategoryId = SelectedBill.SubCategoryId
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

            var selectedBillId = SelectedBill?.Id;

            IsEditingBill = false;
            EditingBillClone = null;

            await LoadBillDataAsync();

            if (selectedBillId.HasValue) {
                SelectedBill = Bills.FirstOrDefault(a => a.Id == selectedBillId);
            }

            await LoadPeriodDataAsync();
            RequestProjectionRecalculation();
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
        target.BucketId = clone.BucketId;
        target.SubCategoryId = clone.SubCategoryId;
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
                RequestProjectionRecalculation();
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
                await _budgetService.UpsertPeriodBillAsync(EditingPeriodBillClone);
            }
            else {
                await _budgetService.UpsertPeriodBillAsync(EditingPeriodBillClone);
            }

            var selectedPeriodBillId = SelectedPeriodBill?.Id;

            IsEditingPeriodBill = false;
            EditingPeriodBillClone = null;

            await LoadBillDataAsync();
            await LoadPeriodDataAsync();

            if (selectedPeriodBillId.HasValue) {
                SelectedPeriodBill = CurrentPeriodBills.FirstOrDefault(a => a.Id == selectedPeriodBillId);
            }

            RequestProjectionRecalculation();
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
        target.TransactionAmount = clone.TransactionAmount;
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
                RequestProjectionRecalculation();
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
            EditingBucketClone = new BudgetBucket {
                Name = "New Bucket",
                Type = BucketType.Standard,
                ExpectedAmount = 0,
                TargetBalance = 0,
                CurrentBalance = 0,
                TargetFrequency = TargetFrequencyType.PaycheckFrequency,
                TargetAmount = 0,
                NextDueDate = null
            };

            // Clear any active allocations for the edit form
            EditableAllocations.Clear();

            SelectedBucket = null;

            // Populate selectable subcategories and mark currently assigned ones
            PopulateEditableSubCategories(bucketId: null);

            IsEditingBucket = true;
        }
        catch (Exception ex) {
            Log.Error(ex, "Error initializing new bucket.");
        }
    }

    private async void EditBucket() {
        try {
            CancelBucket();
            if (SelectedBucket == null) return;

            EditingBucketClone = new BudgetBucket {
                Id = SelectedBucket.Id,
                Name = SelectedBucket.Name,
                Type = SelectedBucket.Type,
                ExpectedAmount = SelectedBucket.ExpectedAmount,
                TargetBalance = SelectedBucket.TargetBalance,
                CurrentBalance = SelectedBucket.CurrentBalance,
                InitialBalance = SelectedBucket.InitialBalance,
                AccountId = SelectedBucket.AccountId,
                IsArchived = SelectedBucket.IsArchived,

                // New Projection Cadence Properties
                TargetFrequency = SelectedBucket.TargetFrequency,
                TargetAmount = SelectedBucket.TargetAmount,
                NextDueDate = SelectedBucket.NextDueDate
            };

            // Load existing allocations for the junction table
            var allocations = await _budgetService.GetAllocationsForBucketAsync(SelectedBucket.Id);
            EditableAllocations.ReplaceRange(allocations);

            // Populate selectable subcategories and mark currently assigned ones
            PopulateEditableSubCategories(SelectedBucket.Id);

            IsEditingBucket = true;
        }
        catch (Exception ex) {
            Log.Error(ex, "Error entering edit mode for bucket.");
        }
    }

    private void PopulateEditableSubCategories(int? bucketId) {
        // Create temporary list in memory first
        var items = new List<SelectableSubCategory>();

        foreach (var subCat in SubCategories) {
            var isCurrentlyAssignedToThis = EditingBucketClone != null
                                            && subCat.DefaultBucketId == EditingBucketClone.Id;

            // Find the name of the bucket it currently belongs to (if any)
            string? currentBucketName = null;
            if (subCat.DefaultBucketId.HasValue) {
                currentBucketName = Buckets
                    .FirstOrDefault(b => b.Id == subCat.DefaultBucketId.Value)?.Name;
            }

            var item = new SelectableSubCategory {
                Id = subCat.Id,
                Name = subCat.Name,
                CategoryName = subCat.CategoryName ?? "",
                CurrentBucketId = subCat.DefaultBucketId,
                CurrentBucketName = currentBucketName,
                EditingBucketId = EditingBucketClone?.Id ?? 0,
                IsSelected = isCurrentlyAssignedToThis
            };

            items.Add(item);
        }

        EditableSubCategories.ReplaceRange(items);
    }

    private async Task SaveBucketAsync() {
        if (EditingBucketClone == null) return;

        try {
            // 1. Sanitize foreign keys
            if (EditingBucketClone.AccountId == 0) EditingBucketClone.AccountId = null;

            // 2. Enforce Bucket Type Rules directly on the object being saved
            NormalizeBucketTypeRules(EditingBucketClone);

            // Get selected subcategory IDs
            var selectedSubCategoryIds = EditableSubCategories
                .Where(x => x.IsSelected)
                .Select(x => x.Id)
                .ToList();

            if (SelectedBucket != null) {
                UpdateBucketFromClone(SelectedBucket, EditingBucketClone);
                SelectedBucket.InitialBalance =
                    SelectedBucket.CurrentBalance; // Reset initial balance to new desired amount
                await _budgetService.UpsertBucketAsync(SelectedBucket, selectedSubCategoryIds);
            }
            else {
                await _budgetService.UpsertBucketAsync(EditingBucketClone, selectedSubCategoryIds);
            }

            var selectedBucketId = SelectedBucket?.Id ?? EditingBucketClone.Id;

            // 3. Save updated Paycheck Allocations (Junction table) for non-UpfrontFloor types
            if (EditingBucketClone.Type != BucketType.UpfrontFloor) {
                await _budgetService.SaveBucketPaycheckAllocationsAsync(
                    selectedBucketId,
                    EditingBucketClone.Type,
                    EditableAllocations
                );
            }

            // 4. Re-sync the master Bucket's CurrentBalance
            await _budgetService.RecalculateBucketBalanceAsync(selectedBucketId);

            IsEditingBucket = false;
            EditingBucketClone = null;
            EditableAllocations.Clear();

            await LoadBucketDataAsync();

            if (selectedBucketId > 0) {
                SelectedBucket = Buckets.FirstOrDefault(a => a.Id == selectedBucketId);
            }

            await LoadPeriodDataAsync();

            RequestProjectionRecalculation();
        }
        catch (Exception ex) {
            Log.Error(ex, "Error saving bucket.");
            MessageBox.Show("Failed to save bucket. See log for details.", "Error", MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void NormalizeBucketTypeRules(BudgetBucket bucket) {
        switch (bucket.Type) {
            case BucketType.UpfrontFloor:
                bucket.ExpectedAmount = 0;
                bucket.CurrentBalance = 0; // Upfront floor balance is tracked strictly via TargetBalance
                bucket.TargetFrequency = null;
                bucket.NextDueDate = null;
                EditableAllocations.Clear(); // Enforce rule: No paycheck allocations for static floors
                break;

            case BucketType.Standard:
                bucket.TargetBalance = 0;
                bucket.CurrentBalance = 0;
                // Default to PaycheckFrequency if frequency was left unset
                bucket.TargetFrequency ??= TargetFrequencyType.PaycheckFrequency;
                break;

            case BucketType.AccumulatingDrawdown:
                // Ensures valid bounds for accumulating funds
                if (bucket.TargetBalance < 0) bucket.TargetBalance = 0;
                if (bucket.Id <= 0) bucket.InitialBalance = bucket.CurrentBalance;
                bucket.TargetFrequency ??= TargetFrequencyType.PaycheckFrequency;
                break;
        }
    }

    private void UpdateBucketFromClone(BudgetBucket target, BudgetBucket clone) {
        target.Name = clone.Name;
        target.Type = clone.Type;
        target.ExpectedAmount = clone.ExpectedAmount;
        target.TargetBalance = clone.TargetBalance;
        target.CurrentBalance = clone.CurrentBalance;
        target.AccountId = clone.AccountId;

        // Updated Projection Cadence Properties
        target.TargetFrequency = clone.TargetFrequency;
        target.TargetAmount = clone.TargetAmount;
        target.NextDueDate = clone.NextDueDate;
    }

    private void CancelBucket() {
        try {
            IsEditingBucket = false;
            EditingBucketClone = null;
            EditableAllocations.Clear();
        }
        catch (Exception ex) {
            Log.Error(ex, "Error cancelling bucket edit.");
        }
    }

    private async Task DeleteBucketAsync() {
        if (EditingBucketClone == null) return;
        var messageBoxResult = MessageBox.Show(
            "Are you sure you want to delete this bucket?",
            "Delete Confirmation",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning
        );

        if (messageBoxResult == MessageBoxResult.Yes) {
            try {
                await _budgetService.DeleteBucketAsync(EditingBucketClone.Id);
                IsEditingBucket = false;
                EditingBucketClone = null;
                EditableAllocations.Clear();

                await LoadBucketDataAsync();
                await LoadPeriodDataAsync();
                await LoadSubCategoryDataAsync();
                await LoadCategoryDataAsync();
                RequestProjectionRecalculation();
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
                IsPaid = SelectedPeriodBucket.IsPaid,
                BucketType = SelectedPeriodBucket.BucketType
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
                await _budgetService.UpsertPeriodBucketAsync(EditingPeriodBucketClone);
            }
            else {
                await _budgetService.UpsertPeriodBucketAsync(EditingPeriodBucketClone);
            }

            var selectedPeriodBucketId = SelectedPeriodBucket?.Id;

            // 2. Re-sync the master Bucket's CurrentBalance
            await _budgetService.RecalculateBucketBalanceAsync(EditingPeriodBucketClone.BucketId);

            IsEditingPeriodBucket = false;
            EditingPeriodBucketClone = null;

            await LoadBucketDataAsync();
            await LoadPeriodDataAsync();

            if (selectedPeriodBucketId.HasValue) {
                SelectedPeriodBucket = CurrentPeriodBuckets.FirstOrDefault(a => a.Id == selectedPeriodBucketId);
            }

            RequestProjectionRecalculation();
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
        target.BucketType = clone.BucketType;
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
        if (EditingPeriodBucketClone == null || EditingPeriodBucketClone.Id == 0) return;
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
                var bucketId = EditingPeriodBucketClone.BucketId;
                await _budgetService.DeletePeriodBucketAsync(EditingPeriodBucketClone.Id);
                // 2. Re-sync the master Bucket's CurrentBalance
                await _budgetService.RecalculateBucketBalanceAsync(bucketId);
                IsEditingPeriodBucket = false;
                EditingPeriodBucketClone = null;
                await LoadBucketDataAsync();
                await LoadPeriodDataAsync();
                RequestProjectionRecalculation();
            }
            catch (Exception ex) {
                Log.Error(ex, "Error deleting period bucket.");
                MessageBox.Show("Failed to delete period bucket. See log for details.", "Error", MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }

    #endregion

    #region Category CRUD

    // Command Implementations
    private void AddCategory() {
        try {
            CancelCategory();
            EditingCategoryClone = new Category { Name = "New Category", SortOrder = 0 };
            SelectedCategory = null;
            IsEditingCategory = true;
        }
        catch (Exception ex) {
            Log.Error(ex, "Error initializing new category.");
        }
    }

    private void EditCategory() {
        try {
            CancelCategory();
            if (SelectedCategory == null) return;

            EditingCategoryClone = new Category {
                Id = SelectedCategory.Id,
                Name = SelectedCategory.Name,
                HexColor = SelectedCategory.HexColor,
                SortOrder = SelectedCategory.SortOrder,
                IsArchived = SelectedCategory.IsArchived
            };

            IsEditingCategory = true;
        }
        catch (Exception ex) {
            Log.Error(ex, "Error entering edit mode for category.");
        }
    }

    private async Task SaveCategoryAsync() {
        if (EditingCategoryClone == null) return;

        try {
            if (SelectedCategory != null) {
                UpdateCategoryFromClone(SelectedCategory, EditingCategoryClone);
                await _budgetService.UpsertCategoryAsync(SelectedCategory);
            }
            else {
                await _budgetService.UpsertCategoryAsync(EditingCategoryClone);
            }

            var selectedId = SelectedCategory?.Id ?? EditingCategoryClone.Id;

            IsEditingCategory = false;
            EditingCategoryClone = null;

            await LoadCategoryDataAsync();
            await LoadSubCategoryDataAsync(); // Refresh subcategories as Category Names might have changed

            if (selectedId > 0) {
                SelectedCategory = Categories.FirstOrDefault(c => c.Id == selectedId);
            }

            RequestProjectionRecalculation();
        }
        catch (Exception ex) {
            Log.Error(ex, "Error saving category.");
            MessageBox.Show("Failed to save category. See log for details.", "Error", MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void UpdateCategoryFromClone(Category target, Category clone) {
        target.Name = clone.Name;
        target.HexColor = clone.HexColor;
        target.SortOrder = clone.SortOrder;
    }

    private void CancelCategory() {
        IsEditingCategory = false;
        EditingCategoryClone = null;
    }

    private async Task DeleteCategoryAsync() {
        if (EditingCategoryClone == null) return;

        // Guard check: preventing delete if subcategories are assigned
        bool inUse = await _budgetService.IsCategoryInUseAsync(EditingCategoryClone.Id);
        if (inUse) {
            MessageBox.Show(
                "This category currently has subcategories assigned to it. Delete or reassign those subcategories first.",
                "Cannot Delete Category", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var result = MessageBox.Show("Are you sure you want to delete this category?",
            "Delete Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes) {
            try {
                await _budgetService.DeleteCategoryAsync(EditingCategoryClone.Id);
                IsEditingCategory = false;
                EditingCategoryClone = null;
                await LoadCategoryDataAsync();
                RequestProjectionRecalculation();
            }
            catch (Exception ex) {
                Log.Error(ex, "Error deleting category.");
                MessageBox.Show("Failed to delete category.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    #endregion

    #region Subcategory CRUD

    private void AddSubCategory() {
        try {
            CancelSubCategory();
            EditingSubCategoryClone = new SubCategory {
                CategoryId = Categories.FirstOrDefault()?.Id ?? 0,
                Name = "New Subcategory",
                SortOrder = 0
            };
            SelectedSubCategory = null;
            IsEditingSubCategory = true;
        }
        catch (Exception ex) {
            Log.Error(ex, "Error initializing new subcategory.");
        }
    }

    private void EditSubCategory() {
        try {
            CancelSubCategory();
            if (SelectedSubCategory == null) return;

            EditingSubCategoryClone = new SubCategory {
                Id = SelectedSubCategory.Id,
                CategoryId = SelectedSubCategory.CategoryId,
                Name = SelectedSubCategory.Name,
                DefaultBucketId = SelectedSubCategory.DefaultBucketId,
                SortOrder = SelectedSubCategory.SortOrder,
                IsArchived = SelectedSubCategory.IsArchived
            };

            IsEditingSubCategory = true;
        }
        catch (Exception ex) {
            Log.Error(ex, "Error entering edit mode for subcategory.");
        }
    }

    private async Task SaveSubCategoryAsync() {
        if (EditingSubCategoryClone == null) return;

        try {
            if (EditingSubCategoryClone.DefaultBucketId == 0)
                EditingSubCategoryClone.DefaultBucketId = null;

            if (SelectedSubCategory != null) {
                UpdateSubCategoryFromClone(SelectedSubCategory, EditingSubCategoryClone);
                await _budgetService.UpsertSubCategoryAsync(SelectedSubCategory);
            }
            else {
                await _budgetService.UpsertSubCategoryAsync(EditingSubCategoryClone);
            }

            var selectedId = SelectedSubCategory?.Id ?? EditingSubCategoryClone.Id;

            IsEditingSubCategory = false;
            EditingSubCategoryClone = null;

            await LoadCategoryDataAsync();
            await LoadSubCategoryDataAsync();

            if (selectedId > 0) {
                SelectedSubCategory = SubCategories.FirstOrDefault(s => s.Id == selectedId);
            }

            RequestProjectionRecalculation();
        }
        catch (Exception ex) {
            Log.Error(ex, "Error saving subcategory.");
            MessageBox.Show("Failed to save subcategory. See log for details.", "Error", MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void UpdateSubCategoryFromClone(SubCategory target, SubCategory clone) {
        target.CategoryId = clone.CategoryId;
        target.Name = clone.Name;
        target.DefaultBucketId = clone.DefaultBucketId == 0 ? null : clone.DefaultBucketId;
        target.SortOrder = clone.SortOrder;
    }

    private void CancelSubCategory() {
        IsEditingSubCategory = false;
        EditingSubCategoryClone = null;
    }

    private async Task DeleteSubCategoryAsync() {
        if (EditingSubCategoryClone == null) return;

        bool inUse = await _budgetService.IsSubCategoryInUseAsync(EditingSubCategoryClone.Id);
        if (inUse) {
            MessageBox.Show("This subcategory is currently assigned to existing transactions and cannot be deleted.",
                "Cannot Delete", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var result = MessageBox.Show("Are you sure you want to delete this subcategory?",
            "Delete Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes) {
            try {
                await _budgetService.DeleteSubCategoryAsync(EditingSubCategoryClone.Id);
                IsEditingSubCategory = false;
                EditingSubCategoryClone = null;
                await LoadCategoryDataAsync();
                await LoadSubCategoryDataAsync();
                RequestProjectionRecalculation();
            }
            catch (Exception ex) {
                Log.Error(ex, "Error deleting subcategory.");
                MessageBox.Show("Failed to delete subcategory.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    #endregion

    #region Transaction CRUD

    private void AddTransaction() {
        try {
            var guid = Guid.NewGuid().ToString();
            if (EditingTransactionClone != null) {
                EditingTransactionClone.PropertyChanged -= EditingTransactionClone_PropertyChanged;
            }

            var editTrans = new Transaction {
                Description = "", Memo = "", Amount = 0, TransactionDate = DateTime.Today,
                FitId = guid
            };
            RefreshTransactionEditState(editTrans);

            EditingTransactionClone = editTrans;
            EditingTransactionClone.PropertyChanged += EditingTransactionClone_PropertyChanged;

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
            if (EditingTransactionClone != null) {
                EditingTransactionClone.PropertyChanged -= EditingTransactionClone_PropertyChanged;
            }

            var editTrans = SelectedTransaction.Clone();

            RefreshTransactionEditState(editTrans);

            EditingTransactionClone = editTrans;
            EditingTransactionClone.PropertyChanged += EditingTransactionClone_PropertyChanged;

            IsEditingTransaction = true;
        }
        catch (Exception ex) {
            Log.Error(ex, "Error entering edit mode for transaction.");
        }
    }

    private void RefreshTransactionEditState(Transaction? source) {
        if (source == null) return;

        var fromAccount = Accounts.FirstOrDefault(a => a.Id == source.AccountId);
        var toAccount = Accounts.FirstOrDefault(a => a.Id == source.ToAccountId);

        bool fromArchived = fromAccount?.IsArchived ?? false;
        bool toArchived = toAccount?.IsArchived ?? false;

        IsEditingTransactionEnabled = !fromArchived && !toArchived;

        // If fromAccount is NULL but AccountId is not 0, it means it's missing from the Accounts collection.
        // This shouldn't happen if LoadAccountDataAsync(true) works, but we should be safe.
        if (fromAccount == null && source.AccountId != null &&
            source.AccountId != 0) {
            // We assume it might be archived if we can't find it in our current list (though our list SHOULD have archived)
            // To be safe, we disable editing if we can't find the account.
            IsEditingTransactionEnabled = false;
        }

        if (toAccount == null && source.ToAccountId != null &&
            source.ToAccountId != 0) {
            IsEditingTransactionEnabled = false;
        }

        // If editing is enabled, filter out archived accounts (except the ones already selected)
        // If editing is disabled (historical transaction with archived account), show ALL accounts so the archived ones are visible
        var filteredAccounts = IsEditingTransactionEnabled
            ? Accounts.Where(a => !a.IsArchived || a.Id == source.AccountId).ToList()
            : Accounts.ToList();

        // If the transaction has an account that is NOT in the list (e.g. deleted or just missing), we should still show it if we can
        if (source.AccountId != null && source.AccountId != 0 && filteredAccounts.All(a => a.Id != source.AccountId)) {
            var missingAccount = fromAccount;
            if (missingAccount != null) {
                filteredAccounts.Add(missingAccount);
            }
        }

        var accountsWithNone = new List<Account> { new Account { Id = 0, Name = "(None)" } };
        accountsWithNone.AddRange(filteredAccounts.OrderBy(a => a.IsArchived).ThenBy(a => a.Name));

        TransactionAccounts.Clear();
        TransactionAccounts.AddRange(accountsWithNone);

        var filteredToAccounts = IsEditingTransactionEnabled
            ? Accounts.Where(a => !a.IsArchived || a.Id == source.ToAccountId).ToList()
            : Accounts.ToList();

        // If the transaction has a to-account that is NOT in the list, we should still show it
        if (source.ToAccountId != null && source.ToAccountId != 0 &&
            filteredToAccounts.All(a => a.Id != source.ToAccountId)) {
            var missingAccount = toAccount;
            if (missingAccount != null) {
                filteredToAccounts.Add(missingAccount);
            }
        }

        var toAccountsWithNone = new List<Account> { new Account { Id = 0, Name = "(None)" } };
        toAccountsWithNone.AddRange(filteredToAccounts.OrderBy(a => a.IsArchived).ThenBy(a => a.Name));

        TransactionToAccounts.Clear();
        TransactionToAccounts.AddRange(toAccountsWithNone);
    }

    private async Task SaveTransactionAsync() {
        if (EditingTransactionClone == null) return;

        try {
            if (EditingTransactionClone.AccountId == 0) EditingTransactionClone.AccountId = null;
            if (EditingTransactionClone.ToAccountId == 0) EditingTransactionClone.ToAccountId = null;
            if (EditingTransactionClone.BillId == 0) EditingTransactionClone.BillId = null;
            if (EditingTransactionClone.BucketId == 0) EditingTransactionClone.BucketId = null;
            if (EditingTransactionClone.SubCategoryId == 0) EditingTransactionClone.SubCategoryId = null;

            if (SelectedTransaction != null) {
                UpdateTransactionFromClone(SelectedTransaction, EditingTransactionClone);
                await _budgetService.UpsertTransactionAsync(SelectedTransaction);
            }
            else {
                await _budgetService.UpsertTransactionAsync(EditingTransactionClone);
            }

            var selectedTransactionId = SelectedTransaction?.Id;

            IsEditingTransaction = false;
            EditingTransactionClone = null;

            await LoadPeriodDataAsync();

            if (selectedTransactionId.HasValue) {
                SelectedTransaction = CurrentPeriodTransactions.FirstOrDefault(a => a.Id == selectedTransactionId);
            }

            RequestProjectionRecalculation();
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
        target.SubCategoryId = clone.SubCategoryId == 0 ? null : clone.SubCategoryId;
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
                RequestProjectionRecalculation();
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

            var selectedPaycheckId = SelectedPaycheck?.Id;

            IsEditingPaycheck = false;
            EditingPaycheckClone = null;

            await LoadPaycheckDataAsync();

            if (selectedPaycheckId.HasValue) {
                SelectedPaycheck = Paychecks.FirstOrDefault(a => a.Id == selectedPaycheckId);
            }

            await LoadPeriodDataAsync();
            RefreshPaychecks();
            LoadPaychecks();
            RequestProjectionRecalculation();
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
                RequestProjectionRecalculation();
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
                IsPrimary = SelectedAccount.IsPrimary,
                IsArchived = SelectedAccount.IsArchived
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

            if (SelectedAccount.AccountAprHistory != null) {
                EditingAccountClone.AccountAprHistory =
                    JsonConvert.DeserializeObject<List<AccountAprHistory>>(
                        JsonConvert.SerializeObject(SelectedAccount.AccountAprHistory));
            }
            else {
                EditingAccountClone.AccountAprHistory = new();
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

                var selectedAccountId = SelectedAccount?.Id;
                if (SelectedAccount != null) {
                    UpdateAccountFromClone(SelectedAccount, EditingAccountClone);
                    await _budgetService.UpsertAccountAsync(SelectedAccount);
                }
                else {
                    EditingAccountClone.Id = await _budgetService.UpsertAccountAsync(EditingAccountClone);

                    var openingBalance = new Transaction() {
                        AccountId = EditingAccountClone.IsLiability ? EditingAccountClone.Id : null,
                        ToAccountId = EditingAccountClone.IsLiability
                            ? null
                            : EditingAccountClone.Id,
                        AccountName = EditingAccountClone.IsLiability
                            ? EditingAccountClone.Name
                            : null,
                        ToAccountName = EditingAccountClone.IsLiability
                            ? null
                            : EditingAccountClone.Name,
                        Amount = EditingAccountClone.Balance,
                        TransactionDate = EditingAccountClone.BalanceAsOf,
                        TransactionId = Guid.NewGuid(),
                        FitId = Guid.NewGuid().ToString(),
                        Description = Constants.OpeningBalance,
                        Memo = Constants.OpeningBalance
                    };

                    if (openingBalance.Amount != 0) {
                        try {
                            await _budgetService.UpsertTransactionAsync(openingBalance);
                        }
                        catch (Exception ex) {
                            Log.Error(ex, "Error upserting transaction in PropertyChanged.");
                        }

                        List<Transaction> transactions = new List<Transaction>();
                        if (openingBalance.AccountId.HasValue) {
                            transactions.AddRange(
                                await _budgetService.GetAccountTransactionsAsync(openingBalance.AccountId.Value));
                        }

                        if (openingBalance.ToAccountId.HasValue) {
                            transactions.AddRange(
                                await _budgetService.GetAccountTransactionsAsync(openingBalance.ToAccountId.Value));
                        }

                        string json = JsonConvert.SerializeObject(transactions.ToList());
                        var reconciliationTransactions =
                            JsonConvert.DeserializeObject<List<TransactionViewModel>>(json);
                        if (reconciliationTransactions != null) {
                            if (openingBalance.AccountId.HasValue) {
                                await _reconciliationService.ReconcileAccountAsync(
                                    openingBalance.AccountId.Value,
                                    reconciliationTransactions,
                                    openingBalance.Amount,
                                    openingBalance.TransactionDate);
                            }

                            if (openingBalance.ToAccountId.HasValue) {
                                await _reconciliationService.ReconcileAccountAsync(
                                    openingBalance.ToAccountId.Value,
                                    reconciliationTransactions,
                                    openingBalance.Amount,
                                    openingBalance.TransactionDate);
                            }
                        }
                    }
                }

                IsEditingAccount = false;
                EditingAccountClone = null;

                await LoadAccountDataAsync();

                if (selectedAccountId.HasValue) {
                    SelectedAccount = VisibleAccounts.FirstOrDefault(a => a.Id == selectedAccountId);
                }

                await LoadPeriodDataAsync();

                RequestProjectionRecalculation();
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

        if ((clone.IsLoanAccount) && clone.MortgageDetails != null) {
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

            if (clone.AccountAprHistory != null) {
                target.AccountAprHistory =
                    JsonConvert.DeserializeObject<List<AccountAprHistory>>(
                        JsonConvert.SerializeObject(clone.AccountAprHistory));
            }
            else {
                target.AccountAprHistory = null;
            }
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

        var affectedPaychecks = Paychecks.Where(x =>
            x.AccountId == EditingAccountClone.Id && (x.EndDate == null || x.EndDate > DateTime.Now)).ToList();
        var affectedBills = Bills.Where(x =>
            (x.AccountId == EditingAccountClone.Id || x.ToAccountId == EditingAccountClone.Id) && x.IsActive &&
            !x.IsArchived).ToList();
        var affectedBuckets = Buckets.Where(x => x.AccountId == EditingAccountClone.Id && !x.IsArchived).ToList();

        if (affectedPaychecks.Any() || affectedBills.Any() || affectedBuckets.Any()) {
            var availableAccounts = Accounts.Where(a => a.Id != EditingAccountClone.Id && !a.IsArchived).ToList();
            var vm = new ReassignAccountDependenciesViewModel(affectedPaychecks, affectedBills, affectedBuckets,
                availableAccounts, EditingAccountClone.Id!);
            var dialog = new ReassignAccountDependenciesDialog(vm) {
                Owner = Application.Current.MainWindow
            };

            if (dialog.ShowDialog() == true) {
                // Apply reassignments
                foreach (var pItem in vm.Paychecks) {
                    pItem.Item.AccountId = pItem.TargetAccountId;
                    await _budgetService.UpsertPaycheckAsync(pItem.Item);
                }

                foreach (var bItem in vm.Bills) {
                    bItem.Bill.AccountId = bItem.TargetAccountId;
                    bItem.Bill.ToAccountId = bItem.TargetToAccountId;
                    await _budgetService.UpsertBillAsync(bItem.Bill);
                }

                foreach (var bItem in vm.Buckets) {
                    bItem.Item.AccountId = bItem.TargetAccountId;
                    await _budgetService.UpsertBucketAsync(bItem.Item, null);
                }
            }
            else {
                return; // User canceled
            }
        }

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
                RequestProjectionRecalculation();
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

    public string StrategyTakeawayPrimary {
        get {
            // Using a small tolerance threshold ($1) avoids floating-point/decimal rounding glitches
            bool netWorthImproved = SnowballNetWorthImprovement > 1.00m;
            bool netWorthWorse = SnowballNetWorthImprovement < -1.00m;
            bool reducedDebt = SnowballDebtReductionVsStandard > 1.00m;
            bool increasedDebt = SnowballDebtReductionVsStandard < -1.00m;

            string primaryAnalysis;

            // 1. Dual-Metric Win-Win
            if (reducedDebt && netWorthImproved) {
                primaryAnalysis =
                    $"Clear Win: You eliminate {Math.Abs(SnowballDebtReductionVsStandard):C0} in debt while growing your net worth by an extra {Math.Abs(SnowballNetWorthImprovement):C0}.";
            }
            // 2. Wealth Growth Focus (Investing extra cash over debt)
            else if (increasedDebt && netWorthImproved) {
                primaryAnalysis =
                    $"Wealth Growth Focus: Investing extra cash boosts your net worth by {Math.Abs(SnowballNetWorthImprovement):C0}, but leaves {Math.Abs(SnowballDebtReductionVsStandard):C0} more debt balance than the standard plan.";
            }
            // 3. Risk Reduction Focus (Paying debt over investing)
            else if (reducedDebt && netWorthWorse) {
                primaryAnalysis =
                    $"Risk Reduction Focus: Pays off {Math.Abs(SnowballDebtReductionVsStandard):C0} more debt for peace of mind, though net worth ends up {Math.Abs(SnowballNetWorthImprovement):C0} lower than investing.";
            }
            // 4. Suboptimal Strategy (More debt AND lower net worth)
            else if (increasedDebt && netWorthWorse) {
                primaryAnalysis =
                    $"Suboptimal Strategy: This configuration increases your debt by {Math.Abs(SnowballDebtReductionVsStandard):C0} and lowers your final net worth by {Math.Abs(SnowballNetWorthImprovement):C0}.";
            }
            // 5. Single-Metric Edge Cases (Debt same, Net Worth changes OR Net Worth same, Debt changes)
            else if (netWorthImproved) {
                primaryAnalysis =
                    $"Net Worth Boost: Your net worth increases by {Math.Abs(SnowballNetWorthImprovement):C0} with no change to your debt payoff trajectory.";
            }
            else if (reducedDebt) {
                primaryAnalysis =
                    $"Debt Payoff Boost: You eliminate {Math.Abs(SnowballDebtReductionVsStandard):C0} more debt with no overall impact on your final net worth.";
            }
            else {
                primaryAnalysis = "Strategy matches your standard baseline plan.";
            }

            return $"{primaryAnalysis}";
        }
    }

    public string StrategyTakeawayDilemma {
        get {
            string dilemmaExplanation =
                "Choosing between paying down debt or investing involves a trade-off: debt paydown offers a guaranteed return and lowers monthly liabilities, while investing aims for higher long-term wealth growth, with no guarantee—at the cost and potential worry of carrying debt longer.";

            return $"{dilemmaExplanation}";
        }
    }

    private async Task CalculateProjectionsAsync(CancellationToken cancellationToken = default) {
        try {
            IsProjecting = true;
            IsSnowballProjecting = true;
            SnowballAnalysisText = "Analyzing strategy...";

            // 1. CAPTURE ALL VIEWMODEL SNAPSHOTS ON UI THREAD FIRST
            var showReconciled = true;
            var currentPeriodDate = CurrentPeriodDate;
            var projectionStartDate = ProjectionStartDate;
            var projectionEndDate = ProjectionEndDate;
            var useAutoSweep = UseAutoSweep;
            var allocation = EditableAllocations;

            // Snapshot options reference on UI thread
            var snowballOptions = SnowballOptions;

            // 2. BACKGROUND WORK
            var (resultList, snowballList, negativeAccounts) = await Task.Run(async () => {
                cancellationToken.ThrowIfCancellationRequested();

                var paychecks = await _budgetService.GetAllPaychecksAsync();
                var bills = await _budgetService.GetAllBillsAsync();
                var buckets = await _budgetService.GetAllBucketsAsync();
                var periodBills = await _budgetService.GetAllPeriodBillsAsync();
                var periodBuckets = await _budgetService.GetAllPeriodBucketsAsync();

                cancellationToken.ThrowIfCancellationRequested();

                List<AccountReconciliation>? reconciliations = null;

                var start = currentPeriodDate == DateTime.MinValue ? DateTime.Today : currentPeriodDate;
                if (projectionStartDate.HasValue) start = projectionStartDate.Value;

                var accounts = (await _budgetService.GetAllAccountsAsOfAsync(start.AddDays(-1), true)).ToList();
                var end = projectionEndDate;
                if (end < start) end = start.AddYears(1);

                var rawPaycheckTransactions = await _budgetService.GetAllPaycheckTransactionsAsync();
                var rawBillTransactions = await _budgetService.GetBillTransactionsAsync();
                var rawBucketTransactions = await _budgetService.GetBucketTransactionsAsync();
                var transactions =
                    (await _budgetService.GetAllTransactionsAsync(start.AddDays(-90), end.AddDays(365))).ToList();

                cancellationToken.ThrowIfCancellationRequested();

                // CLONE/COPY TRANSACTIONS BEFORE MUTATING TO PREVENT SHARED STATE DATA RACES
                var paycheckTransactions = rawPaycheckTransactions
                    .Select(x => new Transaction {
                        Id = x.Id,
                        PaycheckId = x.PaycheckId,
                        PaycheckOccurrenceDate = x.PaycheckOccurrenceDate,
                        TransactionDate = (x.PaycheckOccurrenceDate != null &&
                                           x.TransactionDate != x.PaycheckOccurrenceDate)
                            ? x.PaycheckOccurrenceDate.Value
                            : x.TransactionDate,
                        Amount = x.Amount,
                        AccountId = x.AccountId
                    }).ToList();

                var allTransactions = transactions.Select(x => {
                    var copy = x.CloneReflection();

                    if (copy.PaycheckId != null && copy.PaycheckOccurrenceDate != null &&
                        copy.TransactionDate != copy.PaycheckOccurrenceDate) {
                        copy.TransactionDate = copy.PaycheckOccurrenceDate.Value;
                    }

                    return copy;
                }).ToList();

                // Run Projection Engine (Standard)
                var results = _projectionEngine.CalculateProjections(
                    paycheckTransactions,
                    rawBillTransactions.ToList(),
                    rawBucketTransactions.ToList(),
                    allTransactions,
                    start, end, accounts, paychecks.ToList(), bills.ToList(), buckets.ToList(),
                    allocation.ToList(),
                    periodBills.ToList(), periodBuckets.ToList(), transactions.ToList(), reconciliations?.ToList(),
                    showReconciled, true, useAutoSweep, null);

                cancellationToken.ThrowIfCancellationRequested();

                // Run Projection Engine (Snowball)
                var snowballResults = _projectionEngine.CalculateProjections(
                    paycheckTransactions,
                    rawBillTransactions.ToList(),
                    rawBucketTransactions.ToList(),
                    allTransactions,
                    start, end, accounts, paychecks.ToList(), bills.ToList(), buckets.ToList(),
                    allocation.ToList(),
                    periodBills.ToList(), periodBuckets.ToList(), transactions.ToList(), reconciliations?.ToList(),
                    showReconciled, true, useAutoSweep, snowballOptions);

                var list = results.ToList();
                var snowballList = snowballResults.ToList();

                // Check for negative checking/savings accounts or floor cushion breaches
                var breachedAccounts = new HashSet<string>();
                foreach (var item in list) {
                    if (item.Description.Contains("Necessity")) {
                        var s = "";
                    }

                    // Option A: Catch items specifically marked as breaching their floor cushion
                    if (item.IsBelowFloor) {
                        var targetAcc = accounts.FirstOrDefault(a => a.Id == (item.FromAccountId ?? item.ToAccountId));
                        if (targetAcc != null) {
                            breachedAccounts.Add(targetAcc.Name);
                        }
                    }

                    // Option B: Fallback check against raw balances or spendable balance per checking/savings account
                    foreach (var acc in accounts) {
                        if (acc.Type is not (AccountType.Checking or AccountType.Savings)) continue;

                        // If checking/savings actual balance goes negative or item flags floor breach
                        if (item.AccountBalances.TryGetValue(acc.Name, out decimal balance) && balance < 0) {
                            breachedAccounts.Add(acc.Name);
                        }
                    }
                }

                return (list, snowballList, breachedAccounts);
            }, cancellationToken);

            // 3. CHECK IF CANCELED BEFORE MUTATING UI STATE
            if (cancellationToken.IsCancellationRequested) return;

            // Apply results to UI collections safely

            var temp = new List<ProjectionItem>(resultList.Count);
            foreach (var b in resultList) {
                temp.Add(b);
            }

            Projections.Clear();
            Projections.AddRange(temp);

            var tempSnowball = new List<ProjectionItem>(snowballList.Count);
            foreach (var b in snowballList) {
                tempSnowball.Add(b);
            }

            SnowballProjections.Clear();
            SnowballProjections.AddRange(tempSnowball);

            if (snowballOptions?.EnableSnowball == true) {
                UpdateSnowballAnalysis(resultList, snowballList);
            }
            else {
                ShowSnowballAnalysis = false;
            }

            //for dashboard
            OnPropertyChanged(nameof(TotalLiquidCash));
            OnPropertyChanged(nameof(EnvelopeFloorRequirements));
            OnPropertyChanged(nameof(AccumulatingDrawdownReserves));
            OnPropertyChanged(nameof(UpcomingBillsRequirements));
            OnPropertyChanged(nameof(UnspentStandardEnvelopeRequirements));
            OnPropertyChanged(nameof(TotalRequiredReserves));
            OnPropertyChanged(nameof(UnallocatedSurplusCash));
            OnPropertyChanged(nameof(RecommendedDebtAllocation));
            OnPropertyChanged(nameof(RecommendedInvestmentAllocation));

            OnPropertyChanged(nameof(LowestProjectedCheckingBalance));
            OnPropertyChanged(nameof(ReadinessStatus));
            OnPropertyChanged(nameof(ReadinessStatusTitle));
            OnPropertyChanged(nameof(ReadinessSuggestionMessage));
            OnPropertyChanged(nameof(ReadinessStatusHeaderBrush));
            OnPropertyChanged(nameof(ReadinessStatusBackgroundBrush));
            OnPropertyChanged(nameof(ReadinessStatusBorderBrush));
            
            if (negativeAccounts.Any()) {
                string message =
                    $"Warning: The following accounts breach their balance floor in the projection: {string.Join(", ", negativeAccounts)}";
                ShowWarningToast(message);
            }
        }
        catch (OperationCanceledException) {
            // Suppress expected cancellation when user cancels/edits
        }
        catch (Exception ex) {
            Log.Error(ex, "Error calculating projections.");
            ShowWarningToast("Failed to calculate projections. Check logs.");
        }
        finally {
            // Only turn off visual indicator if this run wasn't canceled mid-flight
            if (!cancellationToken.IsCancellationRequested) {
                IsProjecting = false;
                IsSnowballProjecting = false;
            }
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

    public void ShowSuccessToast(string message) {
        Application.Current.Dispatcher.Invoke(() => {
            // Avoid duplicate toasts with the same message
            if (Toasts.Any(t => t.Message == message)) return;

            var toast = new ToastViewModel(message,
                t => { Application.Current.Dispatcher.Invoke(() => Toasts.Remove(t)); }, ToastType.Success);
            Toasts.Add(toast);
        });
    }

    public void ShowWarningToast(string message) {
        Application.Current.Dispatcher.Invoke(() => {
            // Avoid duplicate toasts with the same message
            if (Toasts.Any(t => t.Message == message)) return;

            var toast = new ToastViewModel(message,
                t => { Application.Current.Dispatcher.Invoke(() => Toasts.Remove(t)); }, ToastType.Warning);
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
            await Task.Yield();

            await LoadCategoryDataAsync();
            await Task.Yield();

            await LoadSubCategoryDataAsync();

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
            var accounts = (await _budgetService.GetAllAccountsAsync(true)).ToList();
            if (accounts.All(a => !(a.Name == "Household Cash" && a.Type == AccountType.Cash))) {
                Log.Information("Household Cash account not found. Creating default.");
                var cashAccount = new Account {
                    Name = "Household Cash",
                    Type = AccountType.Cash,
                    Balance = 0,
                    IncludeInTotal = true
                };
                await _budgetService.UpsertAccountAsync(cashAccount);
                accounts = (await _budgetService.GetAllAccountsAsync(true)).ToList();
            }

            var accountBalances = (await _budgetService.GetAllAccountsAsOfAsync(DateTime.Now, true)).ToList();
            accounts = accounts.OrderBy(b => b.Name).ToList();
            foreach (var a in accounts) {
                a.Balance = accountBalances.SingleOrDefault(b => b.Id == a.Id)?.Balance ?? 0;
            }

            // 1. Unsubscribe previous items to prevent memory leaks
            foreach (var a in Accounts) {
                a.PropertyChanged -= Item_PropertyChanged;
            }

            // 2. Clear all collections cleanly
            Accounts.Clear();
            VisibleAccounts.Clear();

            // Prepare temporary lists off the UI thread
            var visibleList = new List<Account>(accounts.Count);

            foreach (var a in accounts) {
                a.PropertyChanged += Item_PropertyChanged;
                if (!a.IsArchived) {
                    visibleList.Add(a);
                }
            }

            // Batch update both collections (1 layout pass per collection)
            Accounts.AddRange(accounts);
            VisibleAccounts.AddRange(visibleList);

            // 4. Re-populate AccountsWithNone
            AccountsWithNone.Clear();

            var accountsWithNoneList = new List<Account>(accounts.Count + 1) {
                new Account { Id = 0, Name = "(None)" }
            };
            accountsWithNoneList.AddRange(accounts);
            AccountsWithNone.AddRange(accountsWithNoneList);


            // 5. Re-populate ActiveAccountsWithNone

            ActiveAccountsWithNone.Clear();

            var filtered = accounts.Where(a => !a.IsArchived).ToList();
            var activeAccountsWithNoneList = new List<Account>(filtered.Count + 1) {
                new Account { Id = 0, Name = "(None)" }
            };
            activeAccountsWithNoneList.AddRange(filtered);
            ActiveAccountsWithNone.AddRange(activeAccountsWithNoneList);

            if (Accounts.Any(x => x.Type == AccountType.Checking && x.IsPrimary) &&
                Accounts.Any(x => x.Type == AccountType.CreditCard)) {
                UseAutoSweep = true;
                OnPropertyChanged(nameof(UseAutoSweep));
            }

            //for dashboard
            OnPropertyChanged(nameof(TotalLiquidCash));
            OnPropertyChanged(nameof(EnvelopeFloorRequirements));
            OnPropertyChanged(nameof(AccumulatingDrawdownReserves));
            OnPropertyChanged(nameof(UpcomingBillsRequirements));
            OnPropertyChanged(nameof(UnspentStandardEnvelopeRequirements));
            OnPropertyChanged(nameof(TotalRequiredReserves));
            OnPropertyChanged(nameof(UnallocatedSurplusCash));
            OnPropertyChanged(nameof(RecommendedDebtAllocation));
            OnPropertyChanged(nameof(RecommendedInvestmentAllocation));
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

    // Inside your ViewModel:
    private ICollectionView _filteredBillsView;
    public ICollectionView FilteredBillsView => _filteredBillsView;

    private async Task LoadBillDataAsync() {
        Log.Information("Loading bill data.");
        _isLoadingBillData = true;
        try {
            // 1. Unsubscribe old items from both collections to prevent memory leaks
            foreach (var item in Bills) {
                item.PropertyChanged -= Item_PropertyChanged;
            }

            foreach (var item in BillsWithNone) {
                item.PropertyChanged -= Item_PropertyChanged;
            }

            // 2. Clear both collections
            Bills.Clear();
            BillsWithNone.Clear();

            // 3. Query and order new items into a concrete list
            var billsList = (await _budgetService.GetAllBillsAsync(true))
                .OrderBy(b => b.DueDay)
                .ThenBy(b => b.Name)
                .ToList();

            // 4. Attach event handlers to all loaded bills
            foreach (var b in billsList) {
                b.PropertyChanged += Item_PropertyChanged;
            }

            // 5. Prepare the "None" list (pre-allocated capacity)
            var unarchivedBills = billsList.Where(b => !b.IsArchived).ToList();
            var billsWithNoneList = new List<Bill>(unarchivedBills.Count + 1) {
                new Bill { Id = 0, Name = "(None)" }
            };
            billsWithNoneList.AddRange(unarchivedBills);

            // 6. Batch add using RangeObservableCollection (fires 1 layout update per collection)
            Bills.AddRange(billsList);
            BillsWithNone.AddRange(billsWithNoneList);

            Log.Information("Bill data loaded successfully. Bills: {BillCount}", Bills.Count);
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

    private bool FilterBillItem(object item) {
        if (item is not Bill bill) return false;

        // Always show "None" if present
        if (bill.Id == 0) return true;

        string searchText = EditingTransactionClone?.Description?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(searchText)) return true;

        // Show items containing the typed text (case-insensitive)
        return bill.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase);
    }

    private async Task LoadBucketDataAsync() {
        Log.Information("Loading all bucket data.");
        _isLoadingBucketData = true;
        try {
            // 1. Unsubscribe old items from both collections to prevent memory leaks
            foreach (var item in Buckets) {
                item.PropertyChanged -= Item_PropertyChanged;
            }

            foreach (var item in BucketsWithNone) {
                item.PropertyChanged -= Item_PropertyChanged;
            }

            // 2. Clear both collections
            Buckets.Clear();
            BucketsWithNone.Clear();

            // 3. Query and order new items into a concrete list
            var bucketsList = (await _budgetService.GetAllBucketsAsync(true))
                .OrderBy(b => b.Name)
                .ToList();

            // 4. Attach event handlers to all loaded buckets
            foreach (var b in bucketsList) {
                b.PropertyChanged += Item_PropertyChanged;
            }

            // 5. Prepare the "None" list (pre-allocated capacity)
            var unarchivedBuckets = bucketsList.Where(b => !b.IsArchived).ToList();
            var bucketsWithNoneList = new List<BudgetBucket>(unarchivedBuckets.Count + 1) {
                new BudgetBucket { Id = 0, Name = "(None)" }
            };
            bucketsWithNoneList.AddRange(unarchivedBuckets);

            // 6. Batch add using RangeObservableCollection (fires 1 layout update per collection)
            Buckets.AddRange(bucketsList);
            BucketsWithNone.AddRange(bucketsWithNoneList);

            Log.Information("Bucket data loaded successfully. Buckets: {BucketCount}", Buckets.Count);
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

    private async Task LoadSubCategoryDataAsync() {
        Log.Information("Loading all sub category data.");
        _isLoadingSubCategoryData = true;
        try {
            // 1. Unsubscribe old items from both collections to prevent memory leaks
            foreach (var item in SubCategories) {
                item.PropertyChanged -= Item_PropertyChanged;
            }

            foreach (var item in SubCategoriesWithNone) {
                item.PropertyChanged -= Item_PropertyChanged;
            }

            // 2. Clear both collections
            SubCategories.Clear();
            SubCategoriesWithNone.Clear();

            // 3. Query and order new items into a concrete list
            var subCategoriesList = (await _budgetService.GetAllSubCategoriesAsync(true))
                .OrderBy(b => b.Name)
                .ToList();

            // 4. Attach event handlers to all loaded subcategories
            foreach (var b in subCategoriesList) {
                b.PropertyChanged += Item_PropertyChanged;
            }

            // 5. Prepare the "None" list (pre-allocated capacity)
            var unarchivedSubCategories = subCategoriesList.Where(b => !b.IsArchived).ToList();
            var subCategoriesWithNoneList = new List<SubCategory>(unarchivedSubCategories.Count + 1) {
                new SubCategory { Id = 0, Name = "(None)" }
            };
            subCategoriesWithNoneList.AddRange(unarchivedSubCategories);

            // 6. Batch add using RangeObservableCollection (fires 1 layout update per collection)
            SubCategories.AddRange(subCategoriesList);
            SubCategoriesWithNone.AddRange(subCategoriesWithNoneList);

            Log.Information("Sub Category data loaded successfully. SubCategories: {SubCategoryCount}",
                SubCategories.Count);
        }
        catch (Exception ex) {
            Log.Error(ex, "Failed to load sub category data.");
            MessageBox.Show("Failed to load sub category data. See log for details.", "Error", MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally {
            _isLoadingSubCategoryData = false;
        }
    }

    private async Task LoadCategoryDataAsync() {
        Log.Information("Loading all category data.");
        _isLoadingCategoryData = true;
        try {
            // 1. Unsubscribe old items from both collections to prevent memory leaks
            foreach (var item in Categories) {
                item.PropertyChanged -= Item_PropertyChanged;
            }

            foreach (var item in CategoriesWithNone) {
                item.PropertyChanged -= Item_PropertyChanged;
            }

            // 2. Clear both collections
            Categories.Clear();
            // SubCategoriesWithNone.Clear();

            // 3. Query and order new items into a concrete list
            var categoriesList = (await _budgetService.GetAllCategoriesAsync(true))
                .OrderBy(b => b.Name)
                .ToList();

            // 4. Attach event handlers to all loaded subcategories
            foreach (var b in categoriesList) {
                b.PropertyChanged += Item_PropertyChanged;
            }

            // 5. Prepare the "None" list (pre-allocated capacity)
            var unarchivedCategories = categoriesList.Where(b => !b.IsArchived).ToList();
            var categoriesWithNoneList = new List<Category>(unarchivedCategories.Count + 1) {
                new Category { Id = 0, Name = "(None)" }
            };
            categoriesWithNoneList.AddRange(unarchivedCategories);

            // 6. Batch add using RangeObservableCollection (fires 1 layout update per collection)
            Categories.AddRange(categoriesList);
            CategoriesWithNone.AddRange(categoriesWithNoneList);

            Log.Information("Category data loaded successfully. Categories: {CategoryCount}",
                Categories.Count);
        }
        catch (Exception ex) {
            Log.Error(ex, "Failed to load sub category data.");
            MessageBox.Show("Failed to load sub category data. See log for details.", "Error", MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally {
            _isLoadingSubCategoryData = false;
        }
    }

    private async Task LoadPaycheckDataAsync() {
        Log.Information("Loading Paycheck data.");
        _isLoadingPaycheckData = true;
        try {
            // 1. Unsubscribe old items from both collections to prevent memory leaks
            foreach (var item in Paychecks) {
                item.PropertyChanged -= Item_PropertyChanged;
            }

            foreach (var item in PaychecksWithNone) {
                item.PropertyChanged -= Item_PropertyChanged;
            }

            // 2. Clear both collections
            Paychecks.Clear();
            PaychecksWithNone.Clear();

            // 3. Query and order new items into a concrete list
            var paychecksList = (await _budgetService.GetAllPaychecksAsync())
                .OrderBy(b => b.Name)
                .ToList();

            // 4. Attach event handlers to all loaded paychecks
            foreach (var b in paychecksList) {
                b.PropertyChanged += Item_PropertyChanged;
            }

            // 5. Prepare the "None" list (pre-allocated capacity)
            var paychecksWithNoneList = new List<Paycheck>(paychecksList.Count + 1) {
                new Paycheck { Id = 0, Name = "(None)" }
            };
            paychecksWithNoneList.AddRange(paychecksList);

            // 6. Batch add using RangeObservableCollection (fires 1 layout update per collection)
            Paychecks.AddRange(paychecksList);
            PaychecksWithNone.AddRange(paychecksWithNoneList);

            Log.Information("Paycheck data loaded successfully. Paychecks: {PaycheckCount}", Paychecks.Count);
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

            var temp = new List<Paycheck>(allPaychecks.Count);
            foreach (var b in allPaychecks) {
                temp.Add(b);
            }

            PeriodPaychecks.Clear();
            PeriodPaychecks.AddRange(temp);

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

            //for dashboard
            OnPropertyChanged(nameof(TotalLiquidCash));
            OnPropertyChanged(nameof(EnvelopeFloorRequirements));
            OnPropertyChanged(nameof(AccumulatingDrawdownReserves));
            OnPropertyChanged(nameof(UpcomingBillsRequirements));
            OnPropertyChanged(nameof(UnspentStandardEnvelopeRequirements));
            OnPropertyChanged(nameof(TotalRequiredReserves));
            OnPropertyChanged(nameof(UnallocatedSurplusCash));
            OnPropertyChanged(nameof(RecommendedDebtAllocation));
            OnPropertyChanged(nameof(RecommendedInvestmentAllocation));
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
                var periodBill = pBills.FirstOrDefault(existing =>
                    existing.BillId == pb.BillId && existing.PeriodDate.Date == pb.PeriodDate.Date);
                if (periodBill != null) {
                    UpdatePeriodBillFromClone(pb, periodBill);
                }
            }

            projectedBillsForPeriod = projectedBillsForPeriod.OrderBy(pb => pb.DueDate).ToList();

            foreach (var item in CurrentPeriodBills) {
                item.PropertyChanged -= PeriodBill_PropertyChanged;
            }

            CurrentPeriodBills.Clear();

            foreach (var b in projectedBillsForPeriod) {
                b.PropertyChanged += PeriodBill_PropertyChanged;
            }

            CurrentPeriodBills.AddRange(projectedBillsForPeriod);

            UpdateWarningMetrics();
        }
        catch (Exception ex) {
            Log.Error(ex, "Error loading period bills.");
        }
    }

    private async Task LoadPeriodBucketsAsync() {
        try {
            var pBuckets = (await _budgetService.GetPeriodBucketsIncludingMonthlyAsync(CurrentPeriodDate)).ToList();

            foreach (var bucket in Buckets) {
                // Load allocations for non-UpfrontFloor buckets
                List<BucketPaycheckAllocation> allocations = new();
                if (bucket.Type != BucketType.UpfrontFloor) {
                    allocations = (await _budgetService.GetAllocationsForBucketAsync(bucket.Id)).ToList();
                }

                bool isLinkedToCurrentPaycheck = allocations.Any(a => a.PaycheckId == SelectedPeriodPaycheckId);
                bool isStandaloneOrMonthly = !allocations.Any() || ShowByMonth;

                if (isStandaloneOrMonthly || isLinkedToCurrentPaycheck) {
                    if (pBuckets.All(existing => existing.BucketId != bucket.Id)) {
                        var pb = new PeriodBucket {
                            BucketId = bucket.Id,
                            BucketName = bucket.Name,
                            PeriodDate = !allocations.Any()
                                ? new DateTime(CurrentPeriodDate.Year, CurrentPeriodDate.Month, 1)
                                : CurrentPeriodDate,
                            ActualAmount = bucket.ExpectedAmount,
                            IsPaid = false,
                            FitId = Guid.NewGuid(),
                            BucketType = bucket.Type
                        };
                        pBuckets.Add(pb);
                    }
                }
            }

            foreach (var item in CurrentPeriodBuckets) {
                item.PropertyChanged -= PeriodBucket_PropertyChanged;
            }

            CurrentPeriodBuckets.Clear();

            foreach (var b in pBuckets) {
                b.PropertyChanged += PeriodBucket_PropertyChanged;
            }

            CurrentPeriodBuckets.AddRange(pBuckets);
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

            var temp = new List<Transaction>(transactions.Count);
            foreach (var b in transactions) {
                temp.Add(b);
            }

            CurrentPeriodTransactions.Clear();
            CurrentPeriodTransactions.AddRange(temp);
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

    private void InitializeNavigationMenu() {
        NavigationItems.Clear();

        NavigationItems.Add(new NavigationItemViewModel {
            Title = "Dashboard",
            IconKind = "Speedometer",
            TabIndex = 0
        });
        NavigationItems.Add(new NavigationItemViewModel {
            Title = "Accounts",
            IconKind = "Bank",
            TabIndex = 1
        });
        NavigationItems.Add(new NavigationItemViewModel {
            Title = "Bills",
            IconKind = "Receipt",
            TabIndex = 2
        });
        NavigationItems.Add(new NavigationItemViewModel {
            Title = "Envelopes",
            IconKind = "FolderStar",
            TabIndex = 3
        });
        NavigationItems.Add(new NavigationItemViewModel {
            Title = "Transactions",
            IconKind = "FormatListBulleted",
            TabIndex = 4
        });
        NavigationItems.Add(new NavigationItemViewModel {
            Title = "Projections",
            IconKind = "ChartLine",
            TabIndex = 5
        });
        NavigationItems.Add(new NavigationItemViewModel {
            Title = "Settings",
            IconKind = "Cog",
            TabIndex = 6
        });

        // Default selected menu item to Dashboard
        SelectedNavigationItem = NavigationItems.FirstOrDefault();
    }

    private async Task NavigatePeriodAsync(int direction) {
        try {
            if (ShowByMonth) {
                CurrentPeriodDate = CurrentPeriodDate.AddMonths(direction);
                await LoadPeriodDataAsync();
                return;
            }

            var oldestTransaction = await _budgetService.GetOldestTransactionAsync();

            var allPaycheckDates = new List<DateTime>();
            var end = DateTime.Today.AddYears(1);
            if (oldestTransaction.HasValue) {
                allPaycheckDates.Add(oldestTransaction.Value); //at least show the opening balance entry
            }

            foreach (var pay in Paychecks.Where(p => p.Id == SelectedPeriodPaycheckId)) {
                var nextPay = pay.StartDate;
                while (nextPay < end) {
                    allPaycheckDates.Add(nextPay);
                    nextPay = pay.Frequency switch {
                        Frequency.Weekly => nextPay.AddDays(7),
                        Frequency.BiWeekly => nextPay.AddDays(14),
                        Frequency.Monthly => nextPay.AddMonths(1),
                        _ => nextPay.AddYears(100) //that is optimistic
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
        if (SelectedAccount == null) return;
        try {
            var window = new ReconciliationWindow(SelectedAccount, _budgetService) {
                Owner = Application.Current.MainWindow
            };
            window.ShowDialog();

            var selectedAccountId = SelectedAccount.Id;

            await LoadAccountDataAsync();

            SelectedAccount = VisibleAccounts.FirstOrDefault(a => a.Id == selectedAccountId);

            await LoadPeriodDataAsync();

            RequestProjectionRecalculation();
        }
        catch (Exception ex) {
            Log.Error(ex, "Error showing reconciliation window.");
            MessageBox.Show("Failed to open reconciliation window. See log for details.", "Error", MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async Task ImportAccountAsync() {
        if (SelectedAccount == null) return;
        try {
            var window = new ImportReconciliationWindow(SelectedAccount, _budgetService) {
                Owner = Application.Current.MainWindow
            };
            window.ShowDialog();

            var selectedAccountId = SelectedAccount.Id;

            await LoadAccountDataAsync();

            SelectedAccount = VisibleAccounts.FirstOrDefault(a => a.Id == selectedAccountId);

            await LoadPeriodDataAsync();

            RequestProjectionRecalculation();
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
            RequestProjectionRecalculation();
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

            var temp = new List<Paycheck>(allPaychecks.Count);
            foreach (var b in allPaychecks) {
                temp.Add(b);
            }

            PeriodPaychecks.Clear();
            PeriodPaychecks.AddRange(temp);
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

    private void ExportTransactions() {
        var viewModel = new ExportTransactionsViewModel(_budgetService);
        var dialog = new ExportTransactionsDialog(viewModel) {
            Owner = Application.Current.MainWindow
        };
        dialog.ShowDialog();
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

    private void UpdateSnowballAnalysis(List<ProjectionItem> standard, List<ProjectionItem> snowball) {
        if (standard == null || snowball == null || !standard.Any() || !snowball.Any()) {
            ShowSnowballAnalysis = false;
            return;
        }

        var lastStd = standard.Last();
        var lastSnow = snowball.Last();

        decimal stdTotalDebt = standard.Count > 0 ? GetTotalDebt(standard.Last()) : 0;
        decimal snowTotalDebt = snowball.Count > 0 ? GetTotalDebt(snowball.Last()) : 0;

        DateTime? stdDebtFreeDate = FindDebtFreeDate(standard);
        DateTime? snowDebtFreeDate = FindDebtFreeDate(snowball);

        SnowballDebtFreeDate = snowDebtFreeDate;
        if (snowDebtFreeDate.HasValue && stdDebtFreeDate.HasValue) {
            var diff = stdDebtFreeDate.Value - snowDebtFreeDate.Value;
            SnowballMonthsSaved = (int)(diff.TotalDays / 30);
        }
        else {
            SnowballMonthsSaved = 0;
        }

        SnowballFinalNetWorth = lastSnow.Balance;
        SnowballNetWorthImprovement = lastSnow.Balance - lastStd.Balance;

        SnowballFinalDebt = snowTotalDebt;
        SnowballDebtReductionVsStandard = stdTotalDebt - snowTotalDebt;

        ShowSnowballAnalysis = true;

        // Keep the text property for backward compatibility or simple tooltip if needed, 
        // but we'll use individual properties for the UI now.
        var sb = new System.Text.StringBuilder();
        if (snowDebtFreeDate.HasValue) {
            sb.AppendLine($"Estimated Debt Free: {snowDebtFreeDate.Value:MMM yyyy}");
            if (SnowballMonthsSaved > 0) sb.AppendLine($"(Saves {SnowballMonthsSaved} months)");
        }
        else {
            sb.AppendLine($"Final Debt: {snowTotalDebt:C0}");
        }

        sb.AppendLine($"Final Net Worth: {lastSnow.Balance:C0}");
        SnowballAnalysisText = sb.ToString();
        OnPropertyChanged(nameof(StrategyTakeawayPrimary));

        OnPropertyChanged(nameof(StrategyTakeawayDilemma));
    }

    private decimal GetTotalDebt(ProjectionItem item) {
        decimal totalDebt = 0;
        var debtAccountNames = Accounts.Where(a =>
                a.IsLiability)
            .Select(a => a.Name)
            .ToList();

        foreach (var name in debtAccountNames) {
            if (item.AccountBalances.TryGetValue(name, out decimal bal) && bal < 0) {
                totalDebt += -bal;
            }
        }

        return totalDebt;
    }

    private DateTime? FindDebtFreeDate(List<ProjectionItem> items) {
        var debtAccountNames = Accounts.Where(a =>
                a.IsLiability)
            .Select(a => a.Name)
            .ToList();

        foreach (var item in items) {
            bool hasDebt = false;
            foreach (var name in debtAccountNames) {
                if (item.AccountBalances.TryGetValue(name, out decimal bal) && bal < -0.01m) {
                    hasDebt = true;
                    break;
                }
            }

            if (!hasDebt) return item.TransactionDate;
        }

        return null;
    }

    private async void EditingTransactionClone_PropertyChanged(object? sender, PropertyChangedEventArgs e) {
        if (e.PropertyName == nameof(Transaction.SubCategoryId)) {
            ApplyDefaultBucketForSubCategory();
        }
        // Handle Description triggering SubCategoryId lookup
        else if (e.PropertyName == nameof(Transaction.Description)) {
            // 1. Refresh the ComboBox dropdown list based on current typed text
            FilteredBillsView?.Refresh();

            // 2. Only auto-suggest subcategory if the typed text EXACTLY matches an existing bill/payee
            await TryAutoSuggestSubCategoryAsync();
        }
    }

    private void ApplyDefaultBucketForSubCategory() {
        if (EditingTransactionClone == null) return;

        // 1. Only auto-fill if it's a NEW transaction (Id == 0)
        // 2. AND a SubCategoryId is selected
        // 3. AND the user hasn't already picked a Bucket
        if (EditingTransactionClone.Id == 0 &&
            EditingTransactionClone.SubCategoryId.HasValue &&
            !EditingTransactionClone.BucketId.HasValue) {
            // Find the matching SubCategory from your collection
            var selectedSub = SubCategoriesWithNone?
                .FirstOrDefault(s => s.Id == EditingTransactionClone.SubCategoryId.Value);

            // If the subcategory has a default bucket set, auto-assign it
            if (selectedSub != null && selectedSub.DefaultBucketId.HasValue) {
                EditingTransactionClone.BucketId = selectedSub.DefaultBucketId.Value;
            }
        }
    }

    private async Task TryAutoSuggestSubCategoryAsync() {
        if (EditingTransactionClone == null) return;

        string typedText = EditingTransactionClone.Description?.Trim() ?? string.Empty;

        // Conditions:
        // 1. Must be a NEW transaction (Id == 0)
        // 2. SubCategoryId must not already be explicitly set
        // 3. Description must have actual text (at least 2 characters)
        if (EditingTransactionClone.Id == 0 &&
            !EditingTransactionClone.SubCategoryId.HasValue &&
            typedText.Length >= 2) {
            // Check if the typed text strictly matches a bill OR fetch subcategory for exact name
            var suggestedSubId = await _budgetService.GetSuggestedSubCategoryIdAsync(
                typedText,
                EditingTransactionClone.TransactionDate);

            // Re-verify that the user hasn't typed more characters while the async DB query ran
            if (suggestedSubId.HasValue && EditingTransactionClone.Description?.Trim() == typedText) {
                EditingTransactionClone.SubCategoryId = suggestedSubId.Value;
            }
        }
    }

    private async Task PayBillAsync(ProjectionItem? projection) {
        if (projection == null || projection.Type != ProjectionEngine.ProjectionEventType.Bill) return;

        try {
            var bill = Bills.FirstOrDefault(b => b.Id == projection.BillId);
            if (bill == null) return;
            // 1. Map projection details to a concrete Transaction
            var transaction = new Transaction {
                AccountId = bill.AccountId,
                Amount = -Math.Abs(bill.ExpectedAmount), // Outflow
                ToAccountId = bill.ToAccountId,
                TransactionDate = DateTime.Today,
                Description = bill.Name,
                NormalizedDescription = TransactionMatcher.NormalizeName(bill.Name),
                BillId = projection.BillId,
                BucketId = null, //future default bucket?
                SubCategoryId = bill.SubCategoryId, //future default subcategory?
                FromAccountIsCleared = false // Outstanding until reconciled via CSV/QFX
            };

            // 2. Commit transaction to database
            if (await _budgetService.UpsertTransactionAsync(transaction)) {
                // 3. Optional: Play audio cue
                SystemSounds.Asterisk.Play(); // Built-in system sound, or use System.Media.SoundPlayer for a custom WAV

                // 4. Trigger Toast Notification
                ShowSuccessToast($"Marked bill {bill.Name} for {bill.ExpectedAmount:C} as paid.");

                await LoadPeriodDataAsync();
                // 5. Refresh grid / remove projection
                await CalculateProjectionsAsync();
            }
            else {
                throw new Exception($"Failed to record bill payment for {bill.Name}.");
            }
        }
        catch (Exception ex) {
            // Fail gracefully to UI
            MessageBox.Show($"Failed to record bill payment: {ex.Message}", "Error", MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async Task PayPeriodBillAsync(PeriodBill? periodBill) {
        if (periodBill == null) return;

        try {
            var bill = Bills.FirstOrDefault(b => b.Id == periodBill.BillId);
            if (bill == null) return;
            // 1. Map projection details to a concrete Transaction
            var transaction = new Transaction {
                AccountId = bill.AccountId,
                Amount = -Math.Abs(bill.ExpectedAmount), // Outflow
                ToAccountId = bill.ToAccountId,
                TransactionDate = DateTime.Today,
                Description = bill.Name,
                NormalizedDescription = TransactionMatcher.NormalizeName(bill.Name),
                BillId = bill.Id,
                BucketId = null, //future default bucket?
                SubCategoryId = bill.SubCategoryId, //future default subcategory?
                FromAccountIsCleared = false // Outstanding until reconciled via CSV/QFX
            };

            // 2. Commit transaction to database
            if (await _budgetService.UpsertTransactionAsync(transaction)) {
                // 3. Optional: Play audio cue
                SystemSounds.Asterisk.Play(); // Built-in system sound, or use System.Media.SoundPlayer for a custom WAV

                // 4. Trigger Toast Notification
                ShowSuccessToast($"Marked bill {bill.Name} for {bill.ExpectedAmount:C} as paid.");

                await LoadPeriodDataAsync();
                // 5. Refresh grid / remove projection
                RequestProjectionRecalculation();
            }
            else {
                throw new Exception($"Failed to record bill payment for {bill.Name}.");
            }
        }
        catch (Exception ex) {
            // Fail gracefully to UI
            MessageBox.Show($"Failed to record bill payment: {ex.Message}", "Error", MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    #endregion

    #region Draw down

    private async Task FundEnvelopeAsync(ProjectionItem? projection) {
        if (projection == null || projection.Type != ProjectionEngine.ProjectionEventType.AccumulatingDrawdown) return;

        try {
            var bucket = Buckets.FirstOrDefault(b => b.Id == projection.BucketId);
            if (bucket == null) return;

            // Resolve transaction date: use linked paycheck start date if available, or fallback to projection date
            var allocations = await _budgetService.GetAllocationsForBucketAsync(bucket.Id);
            var primaryAllocation = allocations.FirstOrDefault();

            DateTime transactionDate = projection.TransactionDate;
            if (primaryAllocation != null) {
                var pay = Paychecks.FirstOrDefault(p => p.Id == primaryAllocation.PaycheckId);
                if (pay != null) {
                    var nextPay = pay.StartDate;
                    while (nextPay <= projection.TransactionDate) {
                        transactionDate = nextPay;
                        nextPay = pay.Frequency switch {
                            Frequency.Weekly => nextPay.AddDays(7),
                            Frequency.BiWeekly => nextPay.AddDays(14),
                            Frequency.Monthly => nextPay.AddMonths(1),
                            _ => nextPay.AddYears(100)
                        };
                    }
                }
            }

            // Commit transaction to database
            await _budgetService.FundPeriodBucketAsync(bucket.Id, transactionDate, projection.Amount);
            SystemSounds.Asterisk.Play();

            ShowSuccessToast($"Set aside {projection.Amount:C} for {bucket.Name}.");
            await LoadBucketDataAsync();
            await LoadPeriodDataAsync();
            await CalculateProjectionsAsync();
        }
        catch (Exception ex) {
            MessageBox.Show($"Failed to set aside money for envelope: {ex.Message}", "Error", MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async Task SkipFundEnvelopeAsync(ProjectionItem? projection) {
        if (projection == null || projection.Type != ProjectionEngine.ProjectionEventType.AccumulatingDrawdown) return;

        try {
            var bucket = Buckets.FirstOrDefault(b => b.Id == projection.BucketId);
            if (bucket == null) return;

            var allocations = await _budgetService.GetAllocationsForBucketAsync(bucket.Id);
            var primaryAllocation = allocations.FirstOrDefault();

            DateTime transactionDate = projection.TransactionDate;
            if (primaryAllocation != null) {
                var pay = Paychecks.FirstOrDefault(p => p.Id == primaryAllocation.PaycheckId);
                if (pay != null) {
                    var nextPay = pay.StartDate;
                    while (nextPay <= projection.TransactionDate) {
                        transactionDate = nextPay;
                        nextPay = pay.Frequency switch {
                            Frequency.Weekly => nextPay.AddDays(7),
                            Frequency.BiWeekly => nextPay.AddDays(14),
                            Frequency.Monthly => nextPay.AddMonths(1),
                            _ => nextPay.AddYears(100)
                        };
                    }
                }
            }

            await _budgetService.FundPeriodBucketAsync(bucket.Id, transactionDate, 0);
            SystemSounds.Asterisk.Play();

            ShowSuccessToast($"Skipped funding for {bucket.Name}.");
            await LoadBucketDataAsync();
            await LoadPeriodDataAsync();
            await CalculateProjectionsAsync();
        }
        catch (Exception ex) {
            MessageBox.Show($"Failed to skip funding for envelope: {ex.Message}", "Error", MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    #endregion

    #region Dashboard Cash Availability Properties

    public decimal TotalLiquidCash => Accounts
        .Where(a => !a.IsArchived && a.IncludeInTotal &&
                    (a.Type == AccountType.Checking || a.Type == AccountType.Savings))
        .Sum(a => a.Balance);

    public decimal EnvelopeFloorRequirements => Buckets
        .Where(b => !b.IsArchived && b.Type == BucketType.UpfrontFloor)
        .Sum(b => b.TargetBalance);

    public decimal AccumulatingDrawdownReserves => Buckets
        .Where(b => !b.IsArchived && b.Type == BucketType.AccumulatingDrawdown)
        .Sum(b => b.CurrentBalance);

    //The remaining amount of unpaid bills.
    public decimal UpcomingBillsRequirements => CurrentPeriodBills
        .Where(pb => !pb.HasActualAmount && pb.ActualAmount > 0)
        .Sum(pb => pb.ActualAmount);

    public decimal UnspentStandardEnvelopeRequirements => CurrentPeriodBuckets
        .Where(pb => pb.BucketType == BucketType.Standard && pb.TransactionAmount <= pb.ActualAmount)
        .Sum(pb => pb.ActualAmount - pb.TransactionAmount);

    public decimal TotalRequiredReserves => EnvelopeFloorRequirements + AccumulatingDrawdownReserves +
                                            UpcomingBillsRequirements + UnspentStandardEnvelopeRequirements;

    public decimal UnallocatedSurplusCash => Math.Max(0, TotalLiquidCash - TotalRequiredReserves);

    public decimal RecommendedDebtAllocation =>
        UnallocatedSurplusCash * (decimal)SnowballOptions.SurplusSweepPercentage;

    public decimal RecommendedInvestmentAllocation => UnallocatedSurplusCash - RecommendedDebtAllocation;

    #endregion

    #region Dashboard Cash Readiness & Action Suggestions

    public enum CashHealthStatus {
        Optimal,
        TransferRecommended,
        GlobalDeficit
    }

    /// <summary>
    /// Finds the minimum projected balance across all Checking accounts between now and the next deposit.
    /// </summary>
    public decimal LowestProjectedCheckingBalance {
        get {
            if (Projections == null || !Projections.Any())
                return TotalLiquidCash;

            // Find primary checking account names
            var checkingAccountNames = Accounts
                .Where(a => !a.IsArchived && a.Type == AccountType.Checking)
                .Select(a => a.Name)
                .ToList();

            if (!checkingAccountNames.Any()) return 0;

            // Evaluate projected balances up to the next incoming paycheck
            var nextPaycheckItem =
                Projections.FirstOrDefault(p => p.Type == ProjectionEngine.ProjectionEventType.Paycheck);
            DateTime horizonDate = nextPaycheckItem?.TransactionDate ?? DateTime.Today.AddDays(14);

            var relevantProjections = Projections
                .Where(p => p.TransactionDate >= DateTime.Today && p.TransactionDate <= horizonDate)
                .ToList();

            if (!relevantProjections.Any()) return TotalLiquidCash;

            decimal minBalance = decimal.MaxValue;
            foreach (var proj in relevantProjections) {
                foreach (var accName in checkingAccountNames) {
                    if (proj.AccountBalances.TryGetValue(accName, out decimal bal)) {
                        if (bal < minBalance) minBalance = bal;
                    }
                }
            }

            return minBalance == decimal.MaxValue ? 0 : minBalance;
        }
    }

    /// <summary>
    /// Determines current health state based on reserve math and checking account low-water marks.
    /// </summary>
    public CashHealthStatus ReadinessStatus {
        get {
            if (UnallocatedSurplusCash < 0 || TotalLiquidCash < TotalRequiredReserves) {
                return CashHealthStatus.GlobalDeficit;
            }

            if (LowestProjectedCheckingBalance < 0) {
                return CashHealthStatus.TransferRecommended;
            }

            return CashHealthStatus.Optimal;
        }
    }

    public string ReadinessStatusTitle => ReadinessStatus switch {
        CashHealthStatus.Optimal => "Fully Funded & Ready",
        CashHealthStatus.TransferRecommended => "Action Recommended: Account Rebalance Needed",
        CashHealthStatus.GlobalDeficit => "Warning: Reserve Shortfall Detected",
        _ => "Cash Status"
    };

    public string ReadinessStatusIcon => ReadinessStatus switch {
        CashHealthStatus.Optimal => "CheckCircle",
        CashHealthStatus.TransferRecommended => "AlertCircle",
        CashHealthStatus.GlobalDeficit => "AlertOctagon",
        _ => "Information"
    };

    public string ReadinessStatusHeaderBrush => ReadinessStatus switch {
        CashHealthStatus.Optimal => "#22C55E", // Green
        CashHealthStatus.TransferRecommended => "#EAB308", // Yellow / Amber
        CashHealthStatus.GlobalDeficit => "#EF4444", // Red
        _ => "#3B82F6"
    };

    public string ReadinessStatusBackgroundBrush => ReadinessStatus switch {
        CashHealthStatus.Optimal => "#F0FDF4",
        CashHealthStatus.TransferRecommended => "#FEFCE8",
        CashHealthStatus.GlobalDeficit => "#FEF2F2",
        _ => "#F8FAFC"
    };

    public string ReadinessStatusBorderBrush => ReadinessStatus switch {
        CashHealthStatus.Optimal => "#BBF7D0",
        CashHealthStatus.TransferRecommended => "#FEF08A",
        CashHealthStatus.GlobalDeficit => "#FECACA",
        _ => "#E2E8F0"
    };

    /// <summary>
    /// Generates clear human-readable suggestions on what action to take next.
    /// </summary>
    public string ReadinessSuggestionMessage {
        get {
            if (ReadinessStatus == CashHealthStatus.GlobalDeficit) {
                decimal deficit = Math.Abs(TotalLiquidCash - TotalRequiredReserves);
                return
                    $"Your total liquid cash is short by {deficit:C2} to satisfy all safety floors, accumulating drawdowns, and upcoming period expenses. Consider pausing surplus investments or debt sweeps.";
            }

            if (ReadinessStatus == CashHealthStatus.TransferRecommended) {
                decimal transferNeeded = Math.Abs(LowestProjectedCheckingBalance);
                // Check if savings balance is available
                var savingsBalance = Accounts
                    .Where(a => !a.IsArchived && a.Type == AccountType.Savings)
                    .Sum(a => a.Balance);

                if (savingsBalance >= transferNeeded) {
                    return
                        $"Overall liquid cash is sufficient, but Checking is projected to drop to {LowestProjectedCheckingBalance:C2} prior to your next deposit.\n👉 Suggested Action: Move at least {transferNeeded:C2} from Savings to Primary Checking to prevent potential overdrafts.";
                }

                return
                    $"Checking is projected to drop to {LowestProjectedCheckingBalance:C2} before your next paycheck. Transfer funds into Checking to ensure upcoming payments clear safely.";
            }

            if (UnallocatedSurplusCash > 0) {
                return
                    $"All account safety cushions, savings goals, and period budgets are fully covered. You have {UnallocatedSurplusCash:C2} in extra cash available to pay down debt or grow investments.";
            }

            return "All account requirements and budgets are fully satisfied by current liquid balances.";
        }
    }

    #endregion

    public static void SetTheme(bool isDark) {
        var newThemeUri = new Uri(
            isDark ? "Themes/DarkTheme.xaml" : "Themes/LightTheme.xaml",
            UriKind.Relative
        );

        var appResources = Application.Current.Resources.MergedDictionaries;

        // Clear existing theme dictionary and load the new one
        appResources.Clear();
        appResources.Add(new ResourceDictionary { Source = newThemeUri });
    }
}