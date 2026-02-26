using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using MyBudget.Models;
using MyBudget.Services;

namespace MyBudget.ViewModels;

public class MainViewModel : ViewModelBase {
    private readonly BudgetService _budgetService;
    private readonly IProjectionEngine _projectionEngine;
    private ObservableCollection<Bill> _bills = new();
    private ObservableCollection<Paycheck> _paychecks = new();
    private ObservableCollection<Account> _accounts = new();
    private ObservableCollection<ProjectionItem> _projections = new();
    private ObservableCollection<PeriodBill> _currentPeriodBills = new();
    private ObservableCollection<BudgetBucket> _buckets = new();
    private ObservableCollection<PeriodBucket> _currentPeriodBuckets = new();
    private ObservableCollection<AdHocTransaction> _currentPeriodAdHocTransactions = new();
    private Bill? _selectedBill;
    private BudgetBucket? _selectedBucket;
    private Account? _selectedAccount;
    private AdHocTransaction? _selectedAdHocTransaction;
    private bool _isEditingBill;
    private bool _isEditingBucket;
    private bool _isEditingAccount;
    private bool _isEditingAdHocTransaction;
    private bool _isCalculatingProjections;
    private Bill? _editingBillClone;
    private BudgetBucket? _editingBucketClone;
    private Account? _editingAccountClone;
    private AdHocTransaction? _editingAdHocTransactionClone;
    private Paycheck? _editingPaycheckClone;
    private DateTime _currentPeriodDate = DateTime.MinValue;
    private bool _showByMonth;
    private int _selectedPeriodPaycheckId;
    private ObservableCollection<Paycheck> _periodPaychecks = new();
    private bool _isEditingPaycheck;
    private Paycheck? _selectedPaycheck;
    
    public bool IsCalculatingProjections => _isCalculatingProjections;

    public static MainViewModel? Instance { get; private set; }

    public MainViewModel() {
        Instance = this;
        _budgetService = new BudgetService();
        _projectionEngine = new ProjectionEngine();
        LoadData();
        InitializePeriod();
        LoadPeriodBills();
        CalculateProjections();
    }

    #region Properties

    public ObservableCollection<Bill> Bills {
        get => _bills;
        set => SetProperty(ref _bills, value);
    }

    public ObservableCollection<Paycheck> Paychecks {
        get => _paychecks;
        set => SetProperty(ref _paychecks, value);
    }

    public ObservableCollection<Account> Accounts {
        get => _accounts;
        set => SetProperty(ref _accounts, value);
    }

    public AccountType[] AccountTypes => (AccountType[])Enum.GetValues(typeof(AccountType));

    public ObservableCollection<ProjectionItem> Projections {
        get => _projections;
        set => SetProperty(ref _projections, value);
    }

    public ObservableCollection<PeriodBill> CurrentPeriodBills {
        get => _currentPeriodBills;
        set => SetProperty(ref _currentPeriodBills, value);
    }

    public ObservableCollection<BudgetBucket> Buckets {
        get => _buckets;
        set => SetProperty(ref _buckets, value);
    }

    public ObservableCollection<PeriodBucket> CurrentPeriodBuckets {
        get => _currentPeriodBuckets;
        set => SetProperty(ref _currentPeriodBuckets, value);
    }

    public ObservableCollection<AdHocTransaction> CurrentPeriodAdHocTransactions {
        get => _currentPeriodAdHocTransactions;
        set => SetProperty(ref _currentPeriodAdHocTransactions, value);
    }

    public bool ShowByMonth {
        get => _showByMonth;
        set {
            if (SetProperty(ref _showByMonth, value)) {
                InitializePeriod();
                LoadPeriodBills();
            }
        }
    }

    public int SelectedPeriodPaycheckId {
        get => _selectedPeriodPaycheckId;
        set {
            if (SetProperty(ref _selectedPeriodPaycheckId, value)) {
                SetCurrentPeriodDate(value);
                CalculateProjections();
            }
        }
    }

    public ObservableCollection<Paycheck> PeriodPaychecks {
        get => _periodPaychecks;
        set => SetProperty(ref _periodPaychecks, value);
    }

    public string PeriodDisplay {
        get {
            if (ShowByMonth) return _currentPeriodDate.ToString("MMMM yyyy");
            return $"Period: {_currentPeriodDate:d}";
        }
    }

    public DateTime CurrentPeriodDate {
        get => _currentPeriodDate;
        set {
            if (SetProperty(ref _currentPeriodDate, value)) {
                OnPropertyChanged(nameof(PeriodDisplay));
                LoadPeriodBills();
            }
        }
    }

    public Bill? SelectedBill {
        get => _selectedBill;
        set {
            if (SetProperty(ref _selectedBill, value)) {
                OnPropertyChanged(nameof(CanEditBill));
            }
        }
    }

    public BudgetBucket? SelectedBucket {
        get => _selectedBucket;
        set {
            if (SetProperty(ref _selectedBucket, value)) {
                OnPropertyChanged(nameof(CanEditBucket));
            }
        }
    }

    public Account? SelectedAccount {
        get => _selectedAccount;
        set {
            if (SetProperty(ref _selectedAccount, value)) {
                OnPropertyChanged(nameof(CanEditAccount));
            }
        }
    }

    public AdHocTransaction? SelectedAdHocTransaction {
        get => _selectedAdHocTransaction;
        set {
            if (SetProperty(ref _selectedAdHocTransaction, value)) {
                OnPropertyChanged(nameof(CanEditAdHocTransaction));
            }
        }
    }

    public Paycheck? SelectedPaycheck {
        get => _selectedPaycheck;
        set {
            if (SetProperty(ref _selectedPaycheck, value)) {
                OnPropertyChanged(nameof(CanEditPaycheck));
            }
        }
    }
    
    public bool IsEditingBill {
        get => _isEditingBill;
        set {
            if (SetProperty(ref _isEditingBill, value)) {
                OnPropertyChanged(nameof(IsNotEditingBill));
                OnPropertyChanged(nameof(CanEditBill));
            }
        }
    }


    
    public bool IsNotEditingBill => !IsEditingBill;
    public bool CanEditBill => SelectedBill != null && !IsEditingBill;
    
    public bool IsEditingPaycheck {
        get => _isEditingPaycheck;
        set {
            if (SetProperty(ref _isEditingPaycheck, value)) {
                OnPropertyChanged(nameof(IsNotEditingPaycheck));
                OnPropertyChanged(nameof(CanEditPaycheck));
            }
        }
    }
    public bool IsNotEditingPaycheck => !IsEditingPaycheck;
    
    public bool CanEditPaycheck => SelectedPaycheck != null && !IsEditingPaycheck;
    
    public bool IsEditingBucket {
        get => _isEditingBucket;
        set {
            if (SetProperty(ref _isEditingBucket, value)) {
                OnPropertyChanged(nameof(IsNotEditingBucket));
                OnPropertyChanged(nameof(CanEditBucket));
            }
        }
    }

    public bool IsNotEditingBucket => !IsEditingBucket;
    public bool CanEditBucket => SelectedBucket != null && !IsEditingBucket;

    public bool IsEditingAccount {
        get => _isEditingAccount;
        set {
            if (SetProperty(ref _isEditingAccount, value)) {
                OnPropertyChanged(nameof(IsNotEditingAccount));
                OnPropertyChanged(nameof(CanEditAccount));
            }
        }
    }

    public bool IsNotEditingAccount => !IsEditingAccount;
    public bool CanEditAccount => SelectedAccount != null && !IsEditingAccount;

    public bool IsEditingAdHocTransaction {
        get => _isEditingAdHocTransaction;
        set {
            if (SetProperty(ref _isEditingAdHocTransaction, value)) {
                OnPropertyChanged(nameof(IsNotEditingAdHocTransaction));
                OnPropertyChanged(nameof(CanEditAdHocTransaction));
            }
        }
    }

    public bool IsNotEditingAdHocTransaction => !IsEditingAdHocTransaction;
    public bool CanEditAdHocTransaction => SelectedAdHocTransaction != null && !IsEditingAdHocTransaction;

    public Bill? EditingBillClone {
        get => _editingBillClone;
        set => SetProperty(ref _editingBillClone, value);
    }

    public BudgetBucket? EditingBucketClone {
        get => _editingBucketClone;
        set => SetProperty(ref _editingBucketClone, value);
    }

    public Account? EditingAccountClone {
        get => _editingAccountClone;
        set => SetProperty(ref _editingAccountClone, value);
    }

    public AdHocTransaction? EditingAdHocTransactionClone {
        get => _editingAdHocTransactionClone;
        set => SetProperty(ref _editingAdHocTransactionClone, value);
    }
    
    public Paycheck? EditingPaycheckClone {
        get => _editingPaycheckClone;
        set => SetProperty(ref _editingPaycheckClone, value);
    }
    
    
    public ICommand AddBillCommand => new RelayCommand(_ => AddBill(), _ => IsNotEditingBill);
    public ICommand EditBillCommand => new RelayCommand(_ => EditBill(), _ => CanEditBill);
    public ICommand SaveBillCommand => new RelayCommand(_ => SaveBill(), _ => IsEditingBill);
    public ICommand CancelBillCommand => new RelayCommand(_ => CancelBill(), _ => IsEditingBill);
    public ICommand DeletePeriodBillCommand => new RelayCommand(pb => DeletePeriodBill(pb as PeriodBill));
    public ICommand AddBucketCommand => new RelayCommand(_ => AddBucket(), _ => IsNotEditingBucket);
    public ICommand EditBucketCommand => new RelayCommand(_ => EditBucket(), _ => CanEditBucket);
    public ICommand SaveBucketCommand => new RelayCommand(_ => SaveBucket(), _ => IsEditingBucket);
    public ICommand CancelBucketCommand => new RelayCommand(_ => CancelBucket(), _ => IsEditingBucket);
    public ICommand DeletePeriodBucketCommand => new RelayCommand(pb => DeletePeriodBucket(pb as PeriodBucket));

    public ICommand AddAdHocTransactionCommand =>
        new RelayCommand(_ => AddAdHocTransaction(), _ => IsNotEditingAdHocTransaction);

    public ICommand EditAdHocTransactionCommand =>
        new RelayCommand(_ => EditAdHocTransaction(), _ => CanEditAdHocTransaction);
    public ICommand EditPaycheckCommand =>
        new RelayCommand(_ => EditPaycheck(), _ => CanEditPaycheck);
    public ICommand SaveAdHocTransactionCommand =>
        new RelayCommand(_ => SaveAdHocTransaction(), _ => IsEditingAdHocTransaction);

    public ICommand CancelPaycheckCommand =>
        new RelayCommand(_ => CancelPaycheck(), _ => IsEditingPaycheck);
    
    public ICommand SavePaycheckCommand =>
        new RelayCommand(_ => SavePaycheck(), _ => IsEditingPaycheck);
    
    public ICommand CancelAdHocTransactionCommand =>
        new RelayCommand(_ => CancelAdHocTransaction(), _ => IsEditingAdHocTransaction);

    public ICommand DeleteAdHocTransactionCommand =>
        new RelayCommand(t => DeleteAdHocTransaction(t as AdHocTransaction));

    public ICommand AddPaycheckCommand => new RelayCommand(_ => AddPaycheck());
    public ICommand AddAccountCommand => new RelayCommand(_ => AddAccount(), _ => IsNotEditingAccount);
    public ICommand EditAccountCommand => new RelayCommand(_ => EditAccount(), _ => CanEditAccount);
    public ICommand SaveAccountCommand => new RelayCommand(_ => SaveAccount(), _ => IsEditingAccount);
    public ICommand CancelAccountCommand => new RelayCommand(_ => CancelAccount(), _ => IsEditingAccount);

    public ICommand DeleteAccountCommand =>
        new RelayCommand(a => DeleteAccount(a as Account), _ => IsNotEditingAccount);

    public ICommand NextPeriodCommand => new RelayCommand(_ => NavigatePeriod(1));
    public ICommand PrevPeriodCommand => new RelayCommand(_ => NavigatePeriod(-1));
    public ICommand ShowAmortizationCommand => new RelayCommand(a => ShowAmortization(a as Account));
    public ICommand ShowAboutCommand => new RelayCommand(_ => ShowAbout());

    private bool _isLoadingData;

    #endregion

    #region Events

    private void Item_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) {
        if (_isLoadingData) return;
        if (sender is Bill b) _budgetService.UpsertBill(b);
        if (sender is Paycheck p) {
            _budgetService.UpsertPaycheck(p);
            RefreshPaychecks();
            if (p.Id == _selectedPeriodPaycheckId) {
                OnPropertyChanged(nameof(SelectedPeriodPaycheckId));
                LoadPeriodBills();
            }
        }

        if (sender is Account a) _budgetService.UpsertAccount(a);
        if (sender is BudgetBucket bb) _budgetService.UpsertBucket(bb);
        CalculateProjections();
    }

    private void PeriodBill_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) {
        if (sender is PeriodBill pb) {
            _budgetService.UpsertPeriodBill(pb);
            CalculateProjections();
        }
    }

    private void PeriodBucket_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) {
        if (sender is PeriodBucket pb) {
            _budgetService.UpsertPeriodBucket(pb);
            CalculateProjections();
        }
    }

    private void AdHocTransaction_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) {
        if (sender is AdHocTransaction t) {
            _budgetService.UpsertAdHocTransaction(t);
            CalculateProjections();
        }
    }

    #endregion

    #region Bill CRUD

    private void AddBill() {
        //var bill = new Bill { Name = "New Bill", ExpectedAmount = 0, DueDay = 1, IsActive = true };
        // _budgetService.UpsertBill(bill);
        // LoadData();
        // CalculateProjections();

        EditingBillClone = new Bill { Name = "New Bill", ExpectedAmount = 0, DueDay = 1, IsActive = true };
        SelectedBill = null;
        IsEditingBill = true;
    }

    private void EditBill() {
        if (SelectedBill != null) {
            EditingBillClone = new Bill {
                Id = SelectedBill.Id, Name = SelectedBill.Name, ExpectedAmount = SelectedBill.ExpectedAmount,
                Frequency = SelectedBill.Frequency, DueDay = SelectedBill.DueDay, AccountId = SelectedBill.AccountId,
                ToAccountId = SelectedBill.ToAccountId, NextDueDate = SelectedBill.NextDueDate,
                Category = SelectedBill.Category, IsActive = SelectedBill.IsActive
            };
            IsEditingBill = true;
        }
    }

    private void SaveBill() {
        //if (EditingBillClone != null && SelectedBill != null) {
        if (EditingBillClone != null) {
            if (SelectedBill != null) {
                UpdateBillFromClone(SelectedBill, EditingBillClone);
                _budgetService.UpsertBill(SelectedBill);
            }
            else {
                _budgetService.UpsertBill(EditingBillClone);
                LoadData();
            }

            IsEditingBill = false;
            EditingBillClone = null;
            CalculateProjections();
        }
    }

    private void UpdateBillFromClone(Bill target, Bill clone) {
        target.Name = clone.Name;
        target.ExpectedAmount = clone.ExpectedAmount;
        target.Frequency = clone.Frequency;
        target.DueDay = clone.DueDay;
        target.AccountId = clone.AccountId;
        target.ToAccountId = clone.ToAccountId;
        target.NextDueDate = clone.NextDueDate;
        target.Category = clone.Category;
        target.IsActive = clone.IsActive;
    }

    private void CancelBill() {
        IsEditingBill = false;
        EditingBillClone = null;
    }

    private void DeletePeriodBill(PeriodBill? pb) {
        if (pb != null) {
            MessageBoxResult messageBoxResult = System.Windows.MessageBox.Show(
                "Are you sure you want to delete this period's bill?", // Message
                "Delete Confirmation", // Title
                System.Windows.MessageBoxButton.YesNo, // Buttons
                System.Windows.MessageBoxImage.Warning // Icon
            );

            // Check the user's response
            if (messageBoxResult == MessageBoxResult.Yes) {
                // User confirmed deletion, proceed with your delete logic here
                _budgetService.DeletePeriodBill(pb.Id);
                LoadPeriodBills();
                CalculateProjections();
            }
        }
    }

    public ICommand DeleteBillCommand => new RelayCommand(b => DeleteBill(b as Bill));

    private void DeleteBill(Bill? b) {
        if (b != null) {
            MessageBoxResult messageBoxResult = System.Windows.MessageBox.Show(
                "Are you sure you want to delete this bill?", // Message
                "Delete Confirmation", // Title
                System.Windows.MessageBoxButton.YesNo, // Buttons
                System.Windows.MessageBoxImage.Warning // Icon
            );

            // Check the user's response
            if (messageBoxResult == MessageBoxResult.Yes) {
                // User confirmed deletion, proceed with your delete logic here
                _budgetService.DeleteBill(b.Id);
                LoadData();
                CalculateProjections();
            }
        }
    }

    #endregion

    #region Bucket CRUD

    private void AddBucket() {
        // var bucket = new BudgetBucket { Name = "New Bucket", ExpectedAmount = 0 };
        // _budgetService.UpsertBucket(bucket);
        // LoadData();
        // CalculateProjections();
        EditingBucketClone = new BudgetBucket { Name = "New Bucket", ExpectedAmount = 0 };
        SelectedBucket = null;
        IsEditingBucket = true;
    }

    private void EditBucket() {
        if (SelectedBucket != null) {
            EditingBucketClone = new BudgetBucket {
                Id = SelectedBucket.Id, Name = SelectedBucket.Name, ExpectedAmount = SelectedBucket.ExpectedAmount,
                AccountId = SelectedBucket.AccountId
            };
            IsEditingBucket = true;
        }
    }

    private void SaveBucket() {
        //if (EditingBucketClone != null && SelectedBucket != null) {
        if (EditingBucketClone != null) {
            if (SelectedBucket != null) {
                UpdateBucketFromClone(SelectedBucket, EditingBucketClone);
                _budgetService.UpsertBucket(SelectedBucket);
            }
            else {
                _budgetService.UpsertBucket(EditingBucketClone);
                LoadData();
            }

            IsEditingBucket = false;
            EditingBucketClone = null;
            CalculateProjections();
        }
    }

    private void UpdateBucketFromClone(BudgetBucket target, BudgetBucket clone) {
        target.Name = clone.Name;
        target.ExpectedAmount = clone.ExpectedAmount;
        target.AccountId = clone.AccountId;
        target.PayCheckId = clone.PayCheckId;
    }

    private void CancelBucket() {
        IsEditingBucket = false;
        EditingBucketClone = null;
    }

    private void DeletePeriodBucket(PeriodBucket? pb) {
        if (pb != null) {
            MessageBoxResult messageBoxResult = System.Windows.MessageBox.Show(
                "Are you sure you want to delete this period's bucket?", // Message
                "Delete Confirmation", // Title
                System.Windows.MessageBoxButton.YesNo, // Buttons
                System.Windows.MessageBoxImage.Warning // Icon
            );

            // Check the user's response
            if (messageBoxResult == MessageBoxResult.Yes) {
                // User confirmed deletion, proceed with your delete logic here
                _budgetService.DeletePeriodBucket(pb.Id);
                LoadPeriodBills();
                CalculateProjections();
            }
        }
    }

    public ICommand DeleteBucketCommand => new RelayCommand(b => DeleteBucket(b as BudgetBucket));

    private void DeleteBucket(BudgetBucket? b) {
        if (b != null) {
            MessageBoxResult messageBoxResult = System.Windows.MessageBox.Show(
                "Are you sure you want to delete this bucket?", // Message
                "Delete Confirmation", // Title
                System.Windows.MessageBoxButton.YesNo, // Buttons
                System.Windows.MessageBoxImage.Warning // Icon
            );

            // Check the user's response
            if (messageBoxResult == MessageBoxResult.Yes) {
                // User confirmed deletion, proceed with your delete logic here
                _budgetService.DeleteBucket(b.Id);
                LoadData();
                CalculateProjections();
            }
        }
    }

    #endregion

    #region AdHocTransaction CRUD

    private void AddAdHocTransaction() {
        EditingAdHocTransactionClone = new AdHocTransaction
            { Description = "New Ad-Hoc", Amount = 0, Date = DateTime.Today, PeriodDate = CurrentPeriodDate };
        SelectedAdHocTransaction = null;
        IsEditingAdHocTransaction = true;
    }

    private void EditAdHocTransaction() {
        if (SelectedAdHocTransaction != null) {
            EditingAdHocTransactionClone = new AdHocTransaction {
                Id = SelectedAdHocTransaction.Id, Description = SelectedAdHocTransaction.Description,
                Amount = SelectedAdHocTransaction.Amount, Date = SelectedAdHocTransaction.Date,
                AccountId = SelectedAdHocTransaction.AccountId, ToAccountId = SelectedAdHocTransaction.ToAccountId,
                BucketId = SelectedAdHocTransaction.BucketId, PeriodDate = SelectedAdHocTransaction.PeriodDate,
                IsPrincipalOnly = SelectedAdHocTransaction.IsPrincipalOnly,
                IsRebalance = SelectedAdHocTransaction.IsRebalance, PaycheckId = SelectedAdHocTransaction.PaycheckId,
                PaycheckOccurrenceDate = SelectedAdHocTransaction.PaycheckOccurrenceDate
            };
            IsEditingAdHocTransaction = true;
        }
    }

    private void SaveAdHocTransaction() {
        if (EditingAdHocTransactionClone != null) {
            if (SelectedAdHocTransaction != null) {
                UpdateAdHocFromClone(SelectedAdHocTransaction, EditingAdHocTransactionClone);
                _budgetService.UpsertAdHocTransaction(SelectedAdHocTransaction);
            }
            else {
                _budgetService.UpsertAdHocTransaction(EditingAdHocTransactionClone);
            }

            IsEditingAdHocTransaction = false;
            EditingAdHocTransactionClone = null;
            
            LoadPeriodBills();
            CalculateProjections();
        }
    }

    private void UpdateAdHocFromClone(AdHocTransaction target, AdHocTransaction clone) {
        target.Description = clone.Description;
        target.Amount = clone.Amount;
        target.Date = clone.Date;
        target.AccountId = clone.AccountId;
        target.ToAccountId = clone.ToAccountId;
        target.BucketId = clone.BucketId;
        target.PeriodDate = clone.PeriodDate;
        target.IsPrincipalOnly = clone.IsPrincipalOnly;
        target.IsRebalance = clone.IsRebalance;
        target.PaycheckId = clone.PaycheckId;
        target.PaycheckOccurrenceDate = clone.PaycheckOccurrenceDate;
    }

    private void CancelAdHocTransaction() {
        if (SelectedAdHocTransaction != null && SelectedAdHocTransaction.Id == 0) {
            CurrentPeriodAdHocTransactions.Remove(SelectedAdHocTransaction);
        }

        IsEditingAdHocTransaction = false;
        EditingAdHocTransactionClone = null;
    }

    private void DeleteAdHocTransaction(AdHocTransaction? t) {
        if (t != null) {
            MessageBoxResult messageBoxResult = System.Windows.MessageBox.Show(
                "Are you sure you want to delete this transaction?", // Message
                "Delete Confirmation", // Title
                System.Windows.MessageBoxButton.YesNo, // Buttons
                System.Windows.MessageBoxImage.Warning // Icon
            );

            // Check the user's response
            if (messageBoxResult == MessageBoxResult.Yes) {
                // User confirmed deletion, proceed with your delete logic here
                _budgetService.DeleteAdHocTransaction(t.Id);
                LoadPeriodBills();
                CalculateProjections();
            }
        }
    }

    #endregion

    #region Paycheck CRUD

    private void AddPaycheck() {
        
        EditingPaycheckClone = new Paycheck
            { Name = "New Paycheck", ExpectedAmount = 0, StartDate = DateTime.Today, Frequency = Frequency.BiWeekly };
        SelectedPaycheck = null;
        IsEditingPaycheck = true;
        
        // var p = new Paycheck
        //     { Name = "New Paycheck", ExpectedAmount = 0, StartDate = DateTime.Today, Frequency = Frequency.BiWeekly };
        // _budgetService.UpsertPaycheck(p);
        // LoadData();
        // RefreshPaychecks();
        // LoadPaychecks();
        // CalculateProjections();
    }
    
    private void EditPaycheck() {
        if (SelectedPaycheck != null) {
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
    }
    
    private void SavePaycheck() {
        if (EditingPaycheckClone != null) {
            if (SelectedPaycheck != null) {
                UpdatePaycheckFromClone(SelectedPaycheck, EditingPaycheckClone);
                _budgetService.UpsertPaycheck(SelectedPaycheck);
            }
            else {
                _budgetService.UpsertPaycheck(EditingPaycheckClone);
            }

            IsEditingPaycheck = false;
            EditingPaycheckClone = null;
            
            LoadData();
            RefreshPaychecks();
            LoadPaychecks();
            CalculateProjections();
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
        IsEditingPaycheck = false;
        EditingPaycheckClone = null;
    }

    public ICommand DeletePaycheckCommand => new RelayCommand(p => DeletePaycheck(p as Paycheck));

    private void DeletePaycheck(Paycheck? p) {
        if (p != null) {
            MessageBoxResult messageBoxResult = System.Windows.MessageBox.Show(
                "Are you sure you want to delete this paycheck?", // Message
                "Delete Confirmation", // Title
                System.Windows.MessageBoxButton.YesNo, // Buttons
                System.Windows.MessageBoxImage.Warning // Icon
            );

            // Check the user's response
            if (messageBoxResult == MessageBoxResult.Yes) {
                // User confirmed deletion, proceed with your delete logic here
                _budgetService.DeletePaycheck(p.Id);
                LoadData();
                RefreshPaychecks();
                CalculateProjections();
            }
        }
    }

    #endregion

    #region Account CRUD

    private void AddAccount() {
        EditingAccountClone = new Account {
            Name = "New Account", Type = AccountType.Checking, Balance = 0, BalanceAsOf = DateTime.Today,
            IncludeInTotal = true
        };
        SelectedAccount = null;
        IsEditingAccount = true;
    }

    private void EditAccount() {
        if (SelectedAccount != null) {
            EditingAccountClone = new Account {
                Id = SelectedAccount.Id, Name = SelectedAccount.Name, BankName = SelectedAccount.BankName,
                Balance = SelectedAccount.Balance, BalanceAsOf = SelectedAccount.BalanceAsOf,
                AnnualGrowthRate = SelectedAccount.AnnualGrowthRate, IncludeInTotal = SelectedAccount.IncludeInTotal,
                Type = SelectedAccount.Type
            };
            IsEditingAccount = true;
        }
    }

    private void SaveAccount() {
        if (EditingAccountClone != null) {
            if (SelectedAccount != null) {
                UpdateAccountFromClone(SelectedAccount, EditingAccountClone);
                _budgetService.UpsertAccount(SelectedAccount);
            }
            else {
                _budgetService.UpsertAccount(EditingAccountClone);
                LoadData();
            }

            IsEditingAccount = false;
            EditingAccountClone = null;
            CalculateProjections();
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
    }

    private void CancelAccount() {
        IsEditingAccount = false;
        EditingAccountClone = null;
    }

    private void DeleteAccount(Account? a) {
        if (a != null) {
            MessageBoxResult messageBoxResult = System.Windows.MessageBox.Show(
                "Are you sure you want to delete this account?", // Message
                "Delete Confirmation", // Title
                System.Windows.MessageBoxButton.YesNo, // Buttons
                System.Windows.MessageBoxImage.Warning // Icon
            );

            // Check the user's response
            if (messageBoxResult == MessageBoxResult.Yes) {
                // User confirmed deletion, proceed with your delete logic here
                _budgetService.DeleteAccount(a.Id);
                LoadData();
                CalculateProjections();
            }
        }
    }

    #endregion

    #region Helpers

    public void CalculateProjections() {
        if (_isCalculatingProjections) return;
        _isCalculatingProjections = true;
        try {
            var accounts = _budgetService.GetAllAccounts();
            var paychecks = _budgetService.GetAllPaychecks();
            var bills = _budgetService.GetAllBills();
            var buckets = _budgetService.GetAllBuckets();
            var periodBills = _budgetService.GetAllPeriodBills();
            var periodBuckets = _budgetService.GetAllPeriodBuckets();
            var adHocTransactions = _budgetService.GetAllAdHocTransactions();

            DateTime start = CurrentPeriodDate == DateTime.MinValue ? DateTime.Today : CurrentPeriodDate;
            DateTime end = start.AddYears(1);

            var results = _projectionEngine.CalculateProjections(
                start, end, accounts, paychecks, bills, buckets, periodBills, periodBuckets, adHocTransactions);

            Projections = new ObservableCollection<ProjectionItem>(results);
        }
        finally {
            _isCalculatingProjections = false;
        }
    }

    // public DateTime FindPeriodDateFor(DateTime date) {
    //     if (ShowByMonth) return new DateTime(date.Year, date.Month, 1);
    //
    //     var allPaycheckDates = new List<DateTime>();
    //     foreach (var pay in Paychecks) {
    //         DateTime nextPay = pay.StartDate;
    //         while (nextPay <= date) {
    //             allPaycheckDates.Add(nextPay);
    //             nextPay = pay.Frequency switch {
    //                 Frequency.Weekly => nextPay.AddDays(7),
    //                 Frequency.BiWeekly => nextPay.AddDays(14),
    //                 Frequency.Monthly => nextPay.AddMonths(1),
    //                 _ => nextPay.AddYears(100)
    //             };
    //         }
    //     }
    //
    //     return allPaycheckDates.Where(d => d <= date).OrderByDescending(d => d).FirstOrDefault();
    // }
    
    // public AdHocTransaction? GetAdHocForPaycheck(int paycheckId, DateTime date) {
    //     return _budgetService.GetAllAdHocTransactions()
    //         .FirstOrDefault(a => a.PaycheckId == paycheckId && a.Date.Date == date.Date);
    // }

    public List<PeriodBill> GetProjectedBillsForPeriod(DateTime periodStart) {
        DateTime periodEnd = periodStart.AddDays(14); // Default
        if (ShowByMonth) {
            periodEnd = periodStart.AddMonths(1);
        }
        else {
            var allPaycheckDates = new List<DateTime>();
            foreach (var pay in Paychecks) {
                DateTime nextPay = pay.StartDate;
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

    private void LoadData() {
        _isLoadingData = true;
        try {
            var accounts = _budgetService.GetAllAccounts().ToList();
            if (accounts.All(a => a.Name != "Household Cash")) {
                var cashAccount = new Account {
                    Name = "Household Cash",
                    Type = AccountType.Savings,
                    Balance = 0,
                    IncludeInTotal = true
                };
                _budgetService.UpsertAccount(cashAccount);
                accounts = _budgetService.GetAllAccounts().ToList();
            }

            accounts = accounts.OrderBy(b => b.Name).ToList();
            foreach (var a in accounts) a.PropertyChanged += Item_PropertyChanged;
            Accounts = new ObservableCollection<Account>(accounts);

            var bills = _budgetService.GetAllBills();
            bills = bills.OrderBy(b => b.DueDay).ThenBy(b => b.Name).ToList();
            foreach (var b in bills) b.PropertyChanged += Item_PropertyChanged;
            Bills = new ObservableCollection<Bill>(bills);

            var paychecks = _budgetService.GetAllPaychecks();
            paychecks = paychecks.OrderBy(b => b.Name).ToList();
            foreach (var p in paychecks) p.PropertyChanged += Item_PropertyChanged;
            Paychecks = new ObservableCollection<Paycheck>(paychecks);

            var buckets = _budgetService.GetAllBuckets();
            buckets = buckets.OrderBy(b => b.Name).ToList();
            foreach (var b in buckets) b.PropertyChanged += Item_PropertyChanged;
            Buckets = new ObservableCollection<BudgetBucket>(buckets);
        }
        finally {
            _isLoadingData = false;
        }
    }

    private void LoadPaychecks() {
        var allPaychecks = Paychecks.ToList();
        if (!allPaychecks.Any()) {
            CurrentPeriodDate = DateTime.Today;
            return;
        }

        PeriodPaychecks = new ObservableCollection<Paycheck>(allPaychecks);

        SetCurrentPeriodDate();
    }

    private void LoadPeriodBills() {
        var pBills = _budgetService.GetPeriodBills(CurrentPeriodDate).ToList();
        pBills = pBills.OrderBy(pb => pb.DueDate).ToList();
        // Always ensure projected bills for this period are in the database and collection
        var projectedBillsForPeriod = GetProjectedBillsForPeriod(CurrentPeriodDate);
        bool addedAny = false;
        foreach (var pb in projectedBillsForPeriod) {
            if (!pBills.Any(existing => existing.BillId == pb.BillId && existing.DueDate.Date == pb.DueDate.Date)) {
                _budgetService.UpsertPeriodBill(pb);
                addedAny = true;
            }
        }

        if (addedAny) {
            pBills = _budgetService.GetPeriodBills(CurrentPeriodDate).ToList();
            pBills = pBills.OrderBy(pb => pb.DueDate).ToList();
        }

        CurrentPeriodBills = new ObservableCollection<PeriodBill>(pBills);
        foreach (var pb in CurrentPeriodBills) pb.PropertyChanged += PeriodBill_PropertyChanged;

        var pBuckets = _budgetService.GetPeriodBuckets(CurrentPeriodDate).ToList();

        // Same for buckets
        bool addedAnyBucket = false;
        foreach (var bucket in Buckets) {
            if (!pBuckets.Any(existing => existing.BucketId == bucket.Id)) {
                var pb = new PeriodBucket {
                    BucketId = bucket.Id,
                    BucketName = bucket.Name,
                    PeriodDate = CurrentPeriodDate,
                    ActualAmount = bucket.ExpectedAmount,
                    IsPaid = false
                };
                _budgetService.UpsertPeriodBucket(pb);
                addedAnyBucket = true;
            }
        }

        if (addedAnyBucket) pBuckets = _budgetService.GetPeriodBuckets(CurrentPeriodDate).ToList();

        CurrentPeriodBuckets = new ObservableCollection<PeriodBucket>(pBuckets);
        foreach (var pb in CurrentPeriodBuckets) pb.PropertyChanged += PeriodBucket_PropertyChanged;

        var adHocs = _budgetService.GetAdHocTransactions(CurrentPeriodDate).ToList();
        adHocs = adHocs.OrderBy(pb => pb.Date).ToList();
        CurrentPeriodAdHocTransactions = new ObservableCollection<AdHocTransaction>(adHocs);
        foreach (var t in CurrentPeriodAdHocTransactions) t.PropertyChanged += AdHocTransaction_PropertyChanged;
    }

    private void InitializePeriod() {
        if (ShowByMonth) {
            CurrentPeriodDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            return;
        }

        LoadPaychecks();
    }
    
    private void NavigatePeriod(int direction) {
        if (ShowByMonth) {
            CurrentPeriodDate = CurrentPeriodDate.AddMonths(direction);
            LoadPeriodBills();
            return;
        }

        var allPaycheckDates = new List<DateTime>();
        DateTime end = DateTime.Today.AddYears(1);
        foreach (var pay in Paychecks.Where(p => p.Id == SelectedPeriodPaycheckId)) {
            DateTime nextPay = pay.StartDate;
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
        int currentIndex = sortedDates.FindIndex(d => d.Date == CurrentPeriodDate.Date);

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

        //InitializePeriod();
        LoadPeriodBills();
    }
    
    private void RefreshPaychecks() {
        var allPaychecks = Paychecks.ToList();
        if (!allPaychecks.Any()) {
            CurrentPeriodDate = DateTime.Today;
            return;
        }

        PeriodPaychecks = new ObservableCollection<Paycheck>(allPaychecks);
    }

    public void SaveNewAdHoc(AdHocTransaction adHoc) {
        _budgetService.UpsertAdHocTransaction(adHoc);
    }
    
    private void SetCurrentPeriodDate(int? id = null) {
        var allPaychecks = Paychecks.ToList();
        if (!allPaychecks.Any()) {
            CurrentPeriodDate = DateTime.Today;
            return;
        }

        DateTime latestPayBeforeToday = DateTime.MinValue;
        foreach (var pay in allPaychecks.Where(p => id == null || p.Id == id)) {
            DateTime nextPay = pay.StartDate;
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
            DateTime nextPay = pay.StartDate;
            bool found = false;
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


        //PeriodPaychecks = new ObservableCollection<Paycheck>(currentPeriodPaychecks);
        if (id == null && currentPeriodPaychecks.Any()) {
            _selectedPeriodPaycheckId = currentPeriodPaychecks.First().Id;
            OnPropertyChanged(nameof(SelectedPeriodPaycheckId));
        }
    }

    private void ShowAbout() {
        var about = new AboutWindow {
            Owner = Application.Current.MainWindow
        };
        about.ShowDialog();
    }
    
    private void ShowAmortization(Account? account) {
        var amortization = new AmortizationWindow(account) {
            Owner = Application.Current.MainWindow
        };
        amortization.ShowDialog();
    }
    
    #endregion
}