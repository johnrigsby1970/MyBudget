using System.Collections.ObjectModel;
using System.Windows.Input;
using MyBudget.Models;
using MyBudget.Services;
using System.Windows;

namespace MyBudget.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly BudgetService _budgetService;
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
    private DateTime _currentPeriodDate = DateTime.MinValue;
    private bool _showByMonth;
    private int _selectedPeriodPaycheckId;
    private ObservableCollection<Paycheck> _periodPaychecks = new();

    public bool IsCalculatingProjections => _isCalculatingProjections;

    public static MainViewModel? Instance { get; private set; }

    public MainViewModel()
    {
        Instance = this;
        _budgetService = new BudgetService();
        LoadData();
        InitializePeriod();
        LoadPeriodBills();
        CalculateProjections();
    }

    public ObservableCollection<Bill> Bills
    {
        get => _bills;
        set => SetProperty(ref _bills, value);
    }

    public ObservableCollection<Paycheck> Paychecks
    {
        get => _paychecks;
        set => SetProperty(ref _paychecks, value);
    }

    public ObservableCollection<Account> Accounts
    {
        get => _accounts;
        set => SetProperty(ref _accounts, value);
    }

    public AccountType[] AccountTypes => (AccountType[])Enum.GetValues(typeof(AccountType));

    public ObservableCollection<ProjectionItem> Projections
    {
        get => _projections;
        set => SetProperty(ref _projections, value);
    }

    public ObservableCollection<PeriodBill> CurrentPeriodBills
    {
        get => _currentPeriodBills;
        set => SetProperty(ref _currentPeriodBills, value);
    }

    public ObservableCollection<BudgetBucket> Buckets
    {
        get => _buckets;
        set => SetProperty(ref _buckets, value);
    }

    public ObservableCollection<PeriodBucket> CurrentPeriodBuckets
    {
        get => _currentPeriodBuckets;
        set => SetProperty(ref _currentPeriodBuckets, value);
    }

    public ObservableCollection<AdHocTransaction> CurrentPeriodAdHocTransactions
    {
        get => _currentPeriodAdHocTransactions;
        set => SetProperty(ref _currentPeriodAdHocTransactions, value);
    }


    public bool ShowByMonth
    {
        get => _showByMonth;
        set
        {
            if (SetProperty(ref _showByMonth, value))
            {
                InitializePeriod();
                LoadPeriodBills();
            }
        }
    }

    public int SelectedPeriodPaycheckId
    {
        get => _selectedPeriodPaycheckId;
        set
        {
            if (SetProperty(ref _selectedPeriodPaycheckId, value))
            {
                var pc = Paychecks.FirstOrDefault(p => p.Id == value);
                if (pc != null)
                {
                    CurrentPeriodDate = pc.StartDate; // This will trigger LoadPeriodBills
                }
            }
        }
    }

    public ObservableCollection<Paycheck> PeriodPaychecks
    {
        get => _periodPaychecks;
        set => SetProperty(ref _periodPaychecks, value);
    }

    public Bill? SelectedBill
    {
        get => _selectedBill;
        set
        {
            if (SetProperty(ref _selectedBill, value))
            {
                OnPropertyChanged(nameof(CanEditBill));
            }
        }
    }

    public bool IsEditingBill
    {
        get => _isEditingBill;
        set
        {
            if (SetProperty(ref _isEditingBill, value))
            {
                OnPropertyChanged(nameof(CanEditBill));
                OnPropertyChanged(nameof(IsNotEditingBill));
            }
        }
    }

    public bool IsNotEditingBill => !IsEditingBill;
    public bool CanEditBill => SelectedBill != null && !IsEditingBill;

    public BudgetBucket? SelectedBucket
    {
        get => _selectedBucket;
        set
        {
            if (SetProperty(ref _selectedBucket, value))
            {
                OnPropertyChanged(nameof(CanEditBucket));
            }
        }
    }

    public bool IsEditingBucket
    {
        get => _isEditingBucket;
        set
        {
            if (SetProperty(ref _isEditingBucket, value))
            {
                OnPropertyChanged(nameof(CanEditBucket));
                OnPropertyChanged(nameof(IsNotEditingBucket));
            }
        }
    }

    public bool IsNotEditingBucket => !IsEditingBucket;
    public bool CanEditBucket => SelectedBucket != null && !IsEditingBucket;

    public Account? SelectedAccount
    {
        get => _selectedAccount;
        set
        {
            if (SetProperty(ref _selectedAccount, value))
            {
                OnPropertyChanged(nameof(CanEditAccount));
            }
        }
    }

    public bool IsEditingAccount
    {
        get => _isEditingAccount;
        set
        {
            if (SetProperty(ref _isEditingAccount, value))
            {
                OnPropertyChanged(nameof(CanEditAccount));
                OnPropertyChanged(nameof(IsNotEditingAccount));
            }
        }
    }

    public bool IsNotEditingAccount => !IsEditingAccount;
    public bool CanEditAccount => SelectedAccount != null && !IsEditingAccount;

    public AdHocTransaction? SelectedAdHocTransaction
    {
        get => _selectedAdHocTransaction;
        set
        {
            if (SetProperty(ref _selectedAdHocTransaction, value))
            {
                OnPropertyChanged(nameof(CanEditAdHocTransaction));
            }
        }
    }

    public bool IsEditingAdHocTransaction
    {
        get => _isEditingAdHocTransaction;
        set
        {
            if (SetProperty(ref _isEditingAdHocTransaction, value))
            {
                OnPropertyChanged(nameof(CanEditAdHocTransaction));
                OnPropertyChanged(nameof(IsNotEditingAdHocTransaction));
            }
        }
    }

    public bool IsNotEditingAdHocTransaction => !IsEditingAdHocTransaction;
    public bool CanEditAdHocTransaction => SelectedAdHocTransaction != null && !IsEditingAdHocTransaction;

    public Account? EditingAccountClone
    {
        get => _editingAccountClone;
        set => SetProperty(ref _editingAccountClone, value);
    }

    public BudgetBucket? EditingBucketClone
    {
        get => _editingBucketClone;
        set => SetProperty(ref _editingBucketClone, value);
    }

    public Bill? EditingBillClone
    {
        get => _editingBillClone;
        set => SetProperty(ref _editingBillClone, value);
    }

    public AdHocTransaction? EditingAdHocTransactionClone
    {
        get => _editingAdHocTransactionClone;
        set => SetProperty(ref _editingAdHocTransactionClone, value);
    }

    public DateTime CurrentPeriodDate
    {
        get => _currentPeriodDate;
        set
        {
            if (SetProperty(ref _currentPeriodDate, value))
            {
                LoadPeriodBills();
                OnPropertyChanged(nameof(PeriodDisplay));
            }
        }
    }

    public string PeriodDisplay => $"Period: {CurrentPeriodDate:d}";

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
    public ICommand AddAdHocTransactionCommand => new RelayCommand(_ => AddAdHocTransaction(), _ => IsNotEditingAdHocTransaction);
    public ICommand EditAdHocTransactionCommand => new RelayCommand(_ => EditAdHocTransaction(), _ => CanEditAdHocTransaction);
    public ICommand SaveAdHocTransactionCommand => new RelayCommand(_ => SaveAdHocTransaction(), _ => IsEditingAdHocTransaction);
    public ICommand CancelAdHocTransactionCommand => new RelayCommand(_ => CancelAdHocTransaction(), _ => IsEditingAdHocTransaction);
    public ICommand DeleteAdHocTransactionCommand => new RelayCommand(t => DeleteAdHocTransaction(t as AdHocTransaction), _ => IsNotEditingAdHocTransaction);
    public ICommand AddPaycheckCommand => new RelayCommand(_ => AddPaycheck());
    public ICommand AddAccountCommand => new RelayCommand(_ => AddAccount(), _ => IsNotEditingAccount);
    public ICommand EditAccountCommand => new RelayCommand(_ => EditAccount(), _ => CanEditAccount);
    public ICommand SaveAccountCommand => new RelayCommand(_ => SaveAccount(), _ => IsEditingAccount);
    public ICommand CancelAccountCommand => new RelayCommand(_ => CancelAccount(), _ => IsEditingAccount);
    public ICommand DeleteAccountCommand => new RelayCommand(a => DeleteAccount(a as Account), _ => IsNotEditingAccount);
    public ICommand NextPeriodCommand => new RelayCommand(_ => NavigatePeriod(1));
    public ICommand PrevPeriodCommand => new RelayCommand(_ => NavigatePeriod(-1));
    public ICommand ShowAmortizationCommand => new RelayCommand(a => ShowAmortization(a as Account));
    public ICommand ShowAboutCommand => new RelayCommand(_ => ShowAbout());

    private void LoadData()
    {
        var accounts = _budgetService.GetAllAccounts().ToList();
        if (!accounts.Any(a => a.Name == "Household Cash"))
        {
            var cashAccount = new Account
            {
                Name = "Household Cash",
                Type = AccountType.Savings,
                Balance = 0,
                IncludeInTotal = true
            };
            _budgetService.UpsertAccount(cashAccount);
            accounts = _budgetService.GetAllAccounts().ToList();
        }
        foreach (var a in accounts) a.PropertyChanged += Item_PropertyChanged;
        Accounts = new ObservableCollection<Account>(accounts);

        var bills = _budgetService.GetAllBills();
        foreach (var b in bills) b.PropertyChanged += Item_PropertyChanged;
        Bills = new ObservableCollection<Bill>(bills);

        var paychecks = _budgetService.GetAllPaychecks();
        foreach (var p in paychecks) p.PropertyChanged += Item_PropertyChanged;
        Paychecks = new ObservableCollection<Paycheck>(paychecks);

        var buckets = _budgetService.GetAllBuckets();
        foreach (var b in buckets) b.PropertyChanged += Item_PropertyChanged;
        Buckets = new ObservableCollection<BudgetBucket>(buckets);
    }

    private void InitializePeriod()
    {
        if (ShowByMonth)
        {
            CurrentPeriodDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            return;
        }

        // Set current period date to the closest paycheck start date <= today
        var allPaychecks = Paychecks.ToList();
        if (!allPaychecks.Any())
        {
            CurrentPeriodDate = DateTime.Today;
            return;
        }

        DateTime latestPayBeforeToday = DateTime.MinValue;
        foreach (var pay in allPaychecks)
        {
            DateTime nextPay = pay.StartDate;
            while (nextPay <= DateTime.Today.AddDays(1)) // Small buffer
            {
                if (nextPay <= DateTime.Today && nextPay > latestPayBeforeToday)
                    latestPayBeforeToday = nextPay;
                
                nextPay = pay.Frequency switch
                {
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

        // Populate PeriodPaychecks for the selection dropdown
        var currentPeriodPaychecks = new List<Paycheck>();
        foreach (var pay in allPaychecks)
        {
            DateTime nextPay = pay.StartDate;
            // Check if this paycheck has an occurrence on CurrentPeriodDate
            bool found = false;
            while (nextPay <= CurrentPeriodDate)
            {
                if (nextPay.Date == CurrentPeriodDate.Date)
                {
                    found = true;
                    break;
                }
                nextPay = pay.Frequency switch
                {
                    Frequency.Weekly => nextPay.AddDays(7),
                    Frequency.BiWeekly => nextPay.AddDays(14),
                    Frequency.Monthly => nextPay.AddMonths(1),
                    _ => nextPay.AddYears(100)
                };
            }
            if (found) currentPeriodPaychecks.Add(pay);
        }
        PeriodPaychecks = new ObservableCollection<Paycheck>(currentPeriodPaychecks);
        if (currentPeriodPaychecks.Any())
        {
             _selectedPeriodPaycheckId = currentPeriodPaychecks.First().Id;
             OnPropertyChanged(nameof(SelectedPeriodPaycheckId));
        }
    }

    private void LoadPeriodBills()
    {
        var pBills = _budgetService.GetPeriodBills(CurrentPeriodDate).ToList();
        
        // Always ensure projected bills for this period are in the database and collection
        var projectedBillsForPeriod = GetProjectedBillsForPeriod(CurrentPeriodDate);
        bool addedAny = false;
        foreach (var pb in projectedBillsForPeriod)
        {
            if (!pBills.Any(existing => existing.BillId == pb.BillId && existing.DueDate.Date == pb.DueDate.Date))
            {
                _budgetService.UpsertPeriodBill(pb);
                addedAny = true;
            }
        }

        if (addedAny)
        {
            pBills = _budgetService.GetPeriodBills(CurrentPeriodDate).ToList();
        }

        foreach (var pb in pBills) pb.PropertyChanged += PeriodBill_PropertyChanged;
        CurrentPeriodBills = new ObservableCollection<PeriodBill>(pBills);

        // Load Buckets for this period
        var pBuckets = _budgetService.GetPeriodBuckets(CurrentPeriodDate).ToList();
        var projectedBuckets = Buckets.Select(b => new PeriodBucket
        {
            BucketId = b.Id,
            BucketName = b.Name,
            PeriodDate = CurrentPeriodDate,
            ActualAmount = b.ExpectedAmount,
            IsPaid = false
        }).ToList();

        bool addedAnyBucket = false;
        foreach (var pb in projectedBuckets)
        {
            if (!pBuckets.Any(existing => existing.BucketId == pb.BucketId))
            {
                _budgetService.UpsertPeriodBucket(pb);
                addedAnyBucket = true;
            }
        }

        if (addedAnyBucket)
        {
            pBuckets = _budgetService.GetPeriodBuckets(CurrentPeriodDate).ToList();
        }

        foreach (var pb in pBuckets) pb.PropertyChanged += PeriodBucket_PropertyChanged;
        CurrentPeriodBuckets = new ObservableCollection<PeriodBucket>(pBuckets);

        // Load Ad-Hoc Transactions for this period
        var adHocs = _budgetService.GetAdHocTransactions(CurrentPeriodDate).ToList();
        foreach (var t in adHocs) t.PropertyChanged += AdHocTransaction_PropertyChanged;
        CurrentPeriodAdHocTransactions = new ObservableCollection<AdHocTransaction>(adHocs);

        // Ensure AdHocTransactions exist for paychecks in this period
        var cashAccount = Accounts.FirstOrDefault(a => a.Name == "Household Cash");
        DateTime windowEnd = ShowByMonth ? CurrentPeriodDate.AddMonths(1) : CurrentPeriodDate.AddDays(1);
        
        // If it's by paycheck, we might want a bigger window to find the paycheck that defines the period?
        // But the issue says: "Theoretically it could create an entry for each check that falls within the date window."
        // For "By Paycheck", the window is likely until the NEXT paycheck of ANY type.
        
        if (!ShowByMonth)
        {
             // Find next paycheck date
             var nextDates = new List<DateTime>();
             foreach(var pay in Paychecks)
             {
                 DateTime np = pay.StartDate;
                 while(np <= CurrentPeriodDate)
                 {
                     np = pay.Frequency switch
                     {
                         Frequency.Weekly => np.AddDays(7),
                         Frequency.BiWeekly => np.AddDays(14),
                         Frequency.Monthly => np.AddMonths(1),
                         _ => np.AddYears(100)
                     };
                 }
                 nextDates.Add(np);
             }
             if (nextDates.Any()) windowEnd = nextDates.Min();
             else windowEnd = CurrentPeriodDate.AddDays(14);
        }

        bool adHocChanged = false;
        foreach (var pay in Paychecks)
        {
            DateTime nextPay = pay.StartDate;
            while (nextPay < windowEnd)
            {
                if (nextPay >= CurrentPeriodDate && (pay.EndDate == null || nextPay <= pay.EndDate))
                {
                    // Only auto-create AdHoc paycheck records for dates that have arrived (today or past)
                    if (nextPay.Date <= DateTime.Today.Date)
                    {
                        var defaultToAccountId = pay.AccountId ?? cashAccount?.Id;
                        var existingAdHoc = adHocs.FirstOrDefault(a => a.Description == $"Pay: {pay.Name}" && a.Date.Date == nextPay.Date);

                        if (existingAdHoc == null)
                        {
                            var adHoc = new AdHocTransaction
                            {
                                Description = $"Pay: {pay.Name}",
                                Amount = pay.ExpectedAmount,
                                Date = nextPay,
                                PeriodDate = CurrentPeriodDate,
                                ToAccountId = defaultToAccountId
                            };
                            _budgetService.UpsertAdHocTransaction(adHoc);
                            adHocChanged = true;
                        }
                        else
                        {
                            // Keep user-edited Amount as-is, but ensure destination tracks the paycheck's selected account by default
                            if (existingAdHoc.ToAccountId != defaultToAccountId)
                            {
                                existingAdHoc.ToAccountId = defaultToAccountId;
                                _budgetService.UpsertAdHocTransaction(existingAdHoc);
                                adHocChanged = true;
                            }
                        }
                    }
                }
                
                nextPay = pay.Frequency switch
                {
                    Frequency.Weekly => nextPay.AddDays(7),
                    Frequency.BiWeekly => nextPay.AddDays(14),
                    Frequency.Monthly => nextPay.AddMonths(1),
                    _ => nextPay.AddYears(100)
                };
            }
        }
        if (adHocChanged)
        {
            adHocs = _budgetService.GetAdHocTransactions(CurrentPeriodDate).ToList();
            foreach (var t in adHocs) t.PropertyChanged += AdHocTransaction_PropertyChanged;
            CurrentPeriodAdHocTransactions = new ObservableCollection<AdHocTransaction>(adHocs);
        }
        
        CalculateProjections();
    }

    public List<PeriodBill> GetProjectedBillsForPeriod(DateTime periodStart)
    {
        // Find next paycheck date to define period end
        DateTime periodEnd = periodStart.AddDays(14); // Default
        if (ShowByMonth)
        {
            periodEnd = periodStart.AddMonths(1);
        }
        else
        {
            var allPaycheckDates = new List<DateTime>();
            foreach (var pay in Paychecks)
            {
                DateTime nextPay = pay.StartDate;
                while (nextPay < periodStart.AddYears(1))
                {
                    if (nextPay > periodStart)
                    {
                        allPaycheckDates.Add(nextPay);
                        break;
                    }
                    nextPay = pay.Frequency switch
                    {
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
        foreach (var bill in Bills)
        {
            // Calculate the first occurrence of this bill on or after periodStart
            DateTime nextDue;
            if (bill.NextDueDate.HasValue)
            {
                nextDue = bill.NextDueDate.Value;
                // If it's way in the past, move it forward
                while (nextDue < periodStart)
                {
                    nextDue = bill.Frequency switch
                    {
                        Frequency.Monthly => nextDue.AddMonths(1),
                        Frequency.Yearly => nextDue.AddYears(1),
                        Frequency.Weekly => nextDue.AddDays(7),
                        Frequency.BiWeekly => nextDue.AddDays(14),
                        _ => nextDue.AddYears(100)
                    };
                }
            }
            else
            {
                // Fallback to monthly on the due day
                nextDue = new DateTime(periodStart.Year, periodStart.Month, Math.Min(bill.DueDay, DateTime.DaysInMonth(periodStart.Year, periodStart.Month)));
                if (nextDue < periodStart) nextDue = nextDue.AddMonths(1);
            }

            // We only care if it falls within [periodStart, periodEnd)
            while (nextDue < periodEnd)
            {
                if (nextDue >= periodStart)
                {
                    result.Add(new PeriodBill
                    {
                        BillId = bill.Id,
                        BillName = bill.Name,
                        PeriodDate = periodStart,
                        DueDate = nextDue,
                        ActualAmount = bill.ExpectedAmount,
                        IsPaid = false
                    });
                }
                
                nextDue = bill.Frequency switch
                {
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

    private void NavigatePeriod(int direction)
    {
        if (ShowByMonth)
        {
            CurrentPeriodDate = CurrentPeriodDate.AddMonths(direction);
            LoadPeriodBills();
            return;
        }

        var allPaycheckDates = new List<DateTime>();
        DateTime end = DateTime.Today.AddYears(1);
        foreach (var pay in Paychecks)
        {
            DateTime nextPay = pay.StartDate;
            while (nextPay < end)
            {
                allPaycheckDates.Add(nextPay);
                nextPay = pay.Frequency switch
                {
                    Frequency.Weekly => nextPay.AddDays(7),
                    Frequency.BiWeekly => nextPay.AddDays(14),
                    Frequency.Monthly => nextPay.AddMonths(1),
                    _ => nextPay.AddYears(100)
                };
            }
        }
        var uniqueDates = allPaycheckDates.Distinct().OrderBy(d => d).ToList();
        if (!uniqueDates.Any()) return;

        int currentIndex = uniqueDates.IndexOf(CurrentPeriodDate);
        if (currentIndex == -1)
        {
            // Find closest
            if (direction > 0)
                CurrentPeriodDate = uniqueDates.FirstOrDefault(d => d > CurrentPeriodDate);
            else
                CurrentPeriodDate = uniqueDates.LastOrDefault(d => d < CurrentPeriodDate);
        }
        else
        {
            int nextIndex = currentIndex + direction;
            if (nextIndex >= 0 && nextIndex < uniqueDates.Count)
                CurrentPeriodDate = uniqueDates[nextIndex];
        }
        
        InitializePeriod(); // Re-populate PeriodPaychecks
        LoadPeriodBills();
    }

    private void Item_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is Bill bill) _budgetService.UpsertBill(bill);
        if (sender is Paycheck paycheck) _budgetService.UpsertPaycheck(paycheck);
        if (sender is Account account) _budgetService.UpsertAccount(account);
        if (sender is BudgetBucket bucket) _budgetService.UpsertBucket(bucket);
        
        System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
        {
            if (sender is Bill || sender is Paycheck || sender is BudgetBucket)
            {
                LoadPeriodBills();
            }
            CalculateProjections();
        }));
    }

    private void PeriodBill_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is PeriodBill pb) _budgetService.UpsertPeriodBill(pb);
        
        System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
        {
            CalculateProjections();
        }));
    }

    private void PeriodBucket_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is PeriodBucket pb) _budgetService.UpsertPeriodBucket(pb);
        
        System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
        {
            CalculateProjections();
        }));
    }

    private void AdHocTransaction_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is AdHocTransaction t) _budgetService.UpsertAdHocTransaction(t);
        
        System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
        {
            CalculateProjections();
        }));
    }

    private void EditBill()
    {
        if (SelectedBill == null) return;
        
        // Clone for editing
        EditingBillClone = new Bill
        {
            Id = SelectedBill.Id,
            Name = SelectedBill.Name,
            ExpectedAmount = SelectedBill.ExpectedAmount,
            Frequency = SelectedBill.Frequency,
            DueDay = SelectedBill.DueDay,
            AccountId = SelectedBill.AccountId,
            ToAccountId = SelectedBill.ToAccountId,
            NextDueDate = SelectedBill.NextDueDate,
            Category = SelectedBill.Category,
            IsActive = SelectedBill.IsActive
        };
        
        IsEditingBill = true;
    }

    private void SaveBill()
    {
        if (EditingBillClone == null) return;

        if (SelectedBill == null)
        {
            // This was a new bill
            SelectedBill = new Bill();
            UpdateBillFromClone(SelectedBill, EditingBillClone);
            _budgetService.UpsertBill(SelectedBill);
            
            // Re-load to get the generated ID and setup events
            LoadData();
            
            // Re-select by matching properties as we don't have the ID easily until LoadData
            SelectedBill = Bills.FirstOrDefault(b => b.Name == SelectedBill.Name && b.ExpectedAmount == SelectedBill.ExpectedAmount && b.DueDay == SelectedBill.DueDay);
        }
        else
        {
            // Existing bill
            UpdateBillFromClone(SelectedBill, EditingBillClone);
            _budgetService.UpsertBill(SelectedBill);
        }
        
        IsEditingBill = false;
        EditingBillClone = null;
        
        LoadPeriodBills();
        CalculateProjections();
    }

    private void UpdateBillFromClone(Bill target, Bill clone)
    {
        target.Name = clone.Name;
        target.ExpectedAmount = clone.ExpectedAmount;
        target.Frequency = clone.Frequency;
        target.DueDay = clone.DueDay;
        target.AccountId = clone.AccountId;
        target.ToAccountId = clone.ToAccountId;
        target.NextDueDate = clone.NextDueDate;
        target.Category = clone.Category;
    }

    private void CancelBill()
    {
        IsEditingBill = false;
        EditingBillClone = null;
    }

    private void AddBucket()
    {
        SelectedBucket = null;
        EditingBucketClone = new BudgetBucket { Name = "New Bucket", ExpectedAmount = 0 };
        IsEditingBucket = true;
    }

    private void EditBucket()
    {
        if (SelectedBucket == null) return;
        EditingBucketClone = new BudgetBucket
        {
            Id = SelectedBucket.Id,
            Name = SelectedBucket.Name,
            ExpectedAmount = SelectedBucket.ExpectedAmount,
            AccountId = SelectedBucket.AccountId
        };
        IsEditingBucket = true;
    }

    private void SaveBucket()
    {
        if (EditingBucketClone == null) return;
        if (SelectedBucket == null)
        {
            SelectedBucket = new BudgetBucket();
            UpdateBucketFromClone(SelectedBucket, EditingBucketClone);
            _budgetService.UpsertBucket(SelectedBucket);
            LoadData();
            SelectedBucket = Buckets.FirstOrDefault(b => b.Name == SelectedBucket.Name && b.ExpectedAmount == SelectedBucket.ExpectedAmount);
        }
        else
        {
            UpdateBucketFromClone(SelectedBucket, EditingBucketClone);
            _budgetService.UpsertBucket(SelectedBucket);
        }
        IsEditingBucket = false;
        EditingBucketClone = null;
        LoadPeriodBills();
        CalculateProjections();
    }

    private void UpdateBucketFromClone(BudgetBucket target, BudgetBucket clone)
    {
        target.Name = clone.Name;
        target.ExpectedAmount = clone.ExpectedAmount;
        target.AccountId = clone.AccountId;
    }

    private void CancelBucket()
    {
        IsEditingBucket = false;
        EditingBucketClone = null;
    }

    private void DeletePeriodBill(PeriodBill? pb)
    {
        if (pb == null || pb.IsPaid) return;
        
        _budgetService.DeletePeriodBill(pb.Id);
        LoadPeriodBills();
        CalculateProjections();
    }

    private void DeletePeriodBucket(PeriodBucket? pb)
    {
        if (pb == null || pb.IsPaid) return;
        _budgetService.DeletePeriodBucket(pb.Id);
        LoadPeriodBills();
        CalculateProjections();
    }

    private void AddAdHocTransaction()
    {
        SelectedAdHocTransaction = null;
        EditingAdHocTransactionClone = new AdHocTransaction 
        { 
            Description = "Ad-Hoc Item", 
            Amount = 0, 
            Date = DateTime.Today,
            PeriodDate = CurrentPeriodDate 
        };
        IsEditingAdHocTransaction = true;
    }

    private void EditAdHocTransaction()
    {
        if (SelectedAdHocTransaction == null) return;
        EditingAdHocTransactionClone = new AdHocTransaction
        {
            Id = SelectedAdHocTransaction.Id,
            Description = SelectedAdHocTransaction.Description,
            Amount = SelectedAdHocTransaction.Amount,
            Date = SelectedAdHocTransaction.Date,
            AccountId = SelectedAdHocTransaction.AccountId,
            ToAccountId = SelectedAdHocTransaction.ToAccountId,
            BucketId = SelectedAdHocTransaction.BucketId,
            PeriodDate = SelectedAdHocTransaction.PeriodDate,
            IsPrincipalOnly = SelectedAdHocTransaction.IsPrincipalOnly
        };
        IsEditingAdHocTransaction = true;
    }

    private void SaveAdHocTransaction()
    {
        if (EditingAdHocTransactionClone == null) return;
        
        if (SelectedAdHocTransaction == null)
        {
            SelectedAdHocTransaction = new AdHocTransaction();
            UpdateAdHocFromClone(SelectedAdHocTransaction, EditingAdHocTransactionClone);
            _budgetService.UpsertAdHocTransaction(SelectedAdHocTransaction);
            LoadPeriodBills();
            SelectedAdHocTransaction = CurrentPeriodAdHocTransactions.FirstOrDefault(t => t.Description == SelectedAdHocTransaction.Description && t.Amount == SelectedAdHocTransaction.Amount && t.Date == SelectedAdHocTransaction.Date);
        }
        else
        {
            UpdateAdHocFromClone(SelectedAdHocTransaction, EditingAdHocTransactionClone);
            _budgetService.UpsertAdHocTransaction(SelectedAdHocTransaction);
        }

        IsEditingAdHocTransaction = false;
        EditingAdHocTransactionClone = null;
        CalculateProjections();
    }

    private void UpdateAdHocFromClone(AdHocTransaction target, AdHocTransaction clone)
    {
        target.Description = clone.Description;
        target.Amount = clone.Amount;
        target.Date = clone.Date;
        target.AccountId = clone.AccountId;
        target.ToAccountId = clone.ToAccountId;
        target.BucketId = clone.BucketId;
        target.PeriodDate = clone.PeriodDate;
        target.IsPrincipalOnly = clone.IsPrincipalOnly;
    }

    private void CancelAdHocTransaction()
    {
        IsEditingAdHocTransaction = false;
        EditingAdHocTransactionClone = null;
    }

    private void DeleteAdHocTransaction(AdHocTransaction? t)
    {
        if (t == null) return;
        _budgetService.DeleteAdHocTransaction(t.Id);
        LoadPeriodBills();
        CalculateProjections();
    }

    private void AddBill()
    {
        SelectedBill = null;
        EditingBillClone = new Bill { Name = "New Bill", ExpectedAmount = 0, Frequency = Frequency.Monthly, DueDay = DateTime.Today.Day };
        IsEditingBill = true;
    }

    private void AddPaycheck()
    {
        var newPay = new Paycheck { Name = "Paycheck", ExpectedAmount = 2000, Frequency = Frequency.BiWeekly, StartDate = DateTime.Today };
        _budgetService.UpsertPaycheck(newPay);
        LoadData();
        CalculateProjections();
    }

    private void AddAccount()
    {
        SelectedAccount = null;
        EditingAccountClone = new Account 
        { 
            Name = "New Account", 
            BankName = "Bank", 
            Balance = 0, 
            Type = AccountType.Checking,
            MortgageDetails = new MortgageDetails()
        };
        IsEditingAccount = true;
    }

    private void EditAccount()
    {
        if (SelectedAccount == null) return;
        EditingAccountClone = new Account
        {
            Id = SelectedAccount.Id,
            Name = SelectedAccount.Name,
            BankName = SelectedAccount.BankName,
            Balance = SelectedAccount.Balance,
            AnnualGrowthRate = SelectedAccount.AnnualGrowthRate,
            IncludeInTotal = SelectedAccount.IncludeInTotal,
            Type = SelectedAccount.Type,
            MortgageDetails = SelectedAccount.MortgageDetails != null ? new MortgageDetails
            {
                Id = SelectedAccount.MortgageDetails.Id,
                AccountId = SelectedAccount.MortgageDetails.AccountId,
                InterestRate = SelectedAccount.MortgageDetails.InterestRate,
                Escrow = SelectedAccount.MortgageDetails.Escrow,
                MortgageInsurance = SelectedAccount.MortgageDetails.MortgageInsurance,
                LoanPayment = SelectedAccount.MortgageDetails.LoanPayment,
                PaymentDate = SelectedAccount.MortgageDetails.PaymentDate
            } : new MortgageDetails()
        };
        IsEditingAccount = true;
    }

    private void SaveAccount()
    {
        if (EditingAccountClone == null) return;
        if (SelectedAccount == null)
        {
            SelectedAccount = new Account();
            UpdateAccountFromClone(SelectedAccount, EditingAccountClone);
            _budgetService.UpsertAccount(SelectedAccount);
            LoadData();
            SelectedAccount = Accounts.FirstOrDefault(a => a.Name == SelectedAccount.Name && a.Type == SelectedAccount.Type);
        }
        else
        {
            UpdateAccountFromClone(SelectedAccount, EditingAccountClone);
            _budgetService.UpsertAccount(SelectedAccount);
        }
        IsEditingAccount = false;
        EditingAccountClone = null;
        CalculateProjections();
    }

    private void UpdateAccountFromClone(Account target, Account clone)
    {
        target.Name = clone.Name;
        target.BankName = clone.BankName;
        target.Balance = clone.Balance;
        target.AnnualGrowthRate = clone.AnnualGrowthRate;
        target.IncludeInTotal = clone.IncludeInTotal;
        target.Type = clone.Type;
        if (clone.Type == AccountType.Mortgage)
        {
            if (target.MortgageDetails == null) target.MortgageDetails = new MortgageDetails();
            target.MortgageDetails.InterestRate = clone.MortgageDetails!.InterestRate;
            target.MortgageDetails.Escrow = clone.MortgageDetails.Escrow;
            target.MortgageDetails.MortgageInsurance = clone.MortgageDetails.MortgageInsurance;
            target.MortgageDetails.LoanPayment = clone.MortgageDetails.LoanPayment;
            target.MortgageDetails.PaymentDate = clone.MortgageDetails.PaymentDate;
        }
    }

    private void CancelAccount()
    {
        IsEditingAccount = false;
        EditingAccountClone = null;
    }

    private void ShowAmortization(Account? account)
    {
        if (account == null || account.Type != AccountType.Mortgage || account.MortgageDetails == null) return;
        
        var window = new AmortizationWindow(account);
        window.Owner = System.Windows.Application.Current.MainWindow;
        window.ShowDialog();
    }

    private void DeleteAccount(Account? account)
    {
        if (account == null) return;
        
        if (_budgetService.IsAccountInUse(account.Id))
        {
            System.Windows.MessageBox.Show($"Account '{account.Name}' cannot be removed because it is in use by bills, buckets, or ad-hoc transactions.", "Account In Use", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }

        _budgetService.DeleteAccount(account.Id);
        LoadData();
        CalculateProjections();
    }

    public AdHocTransaction? GetAdHocForPaycheck(string description, DateTime date)
    {
        return _budgetService.GetAllAdHocTransactions()
            .FirstOrDefault(a => a.Description == description && a.Date.Date == date.Date);
    }

    public void SaveNewAdHoc(AdHocTransaction adHoc)
    {
        _budgetService.UpsertAdHocTransaction(adHoc);
    }

    public DateTime FindPeriodDateFor(DateTime date)
    {
        if (ShowByMonth) return new DateTime(date.Year, date.Month, 1);

        var allPaycheckDates = new List<DateTime>();
        foreach (var pay in Paychecks)
        {
            DateTime nextPay = pay.StartDate;
            while (nextPay <= date)
            {
                allPaycheckDates.Add(nextPay);
                nextPay = pay.Frequency switch
                {
                    Frequency.Weekly => nextPay.AddDays(7),
                    Frequency.BiWeekly => nextPay.AddDays(14),
                    Frequency.Monthly => nextPay.AddMonths(1),
                    _ => nextPay.AddYears(100)
                };
            }
        }
        return allPaycheckDates.Where(d => d <= date).OrderByDescending(d => d).FirstOrDefault();
    }

    private void ShowAbout()
    {
        var about = new AboutWindow();
        about.Owner = Application.Current.MainWindow;
        about.ShowDialog();
    }

    public void CalculateProjections()
    {
        if (_isCalculatingProjections) return;
        _isCalculatingProjections = true;
        try
        {
            var list = new List<ProjectionItem>();
        DateTime current = CurrentPeriodDate;
        if (current == DateTime.MinValue) current = DateTime.Today;
        DateTime end = current.AddYears(1);
        
        var accountBalances = Accounts.ToDictionary(a => a.Id, a => a.Balance);
        var accountNames = Accounts.ToDictionary(a => a.Id, a => a.Name);
        var accountBalanceDates = Accounts.ToDictionary(a => a.Id, a => a.BalanceAsOf);

        // Calculate starting running balance based on included accounts' starting balances
        // Only include accounts whose BalanceAsOf is effective at the current start date
        var includedTotalAccounts = new HashSet<int>();
        foreach (var acc in Accounts.Where(a => a.IncludeInTotal))
        {
            if (acc.BalanceAsOf <= current)
            {
                includedTotalAccounts.Add(acc.Id);
            }
        }

        // Find the earliest unbalanced paycheck to potentially start the projection earlier
        var unbalancedPaychecks = Paychecks.Where(p => !p.IsBalanced).ToList();
        if (unbalancedPaychecks.Any())
        {
            DateTime earliestUnbalanced = unbalancedPaychecks.Select(p => p.StartDate).Min();
            if (earliestUnbalanced < current)
            {
                current = earliestUnbalanced;
            }
        }

        var events = new List<(DateTime Date, decimal Amount, string Description, int? AccountId, int? ToAccountId, int? BucketId, int? PaycheckId, ProjectionEngine.ProjectionEventType Type, bool IsPrincipalOnly, bool IsRebalance)>();

        var cashAccount = Accounts.FirstOrDefault(a => a.Name == "Household Cash");

        var allAdHocs = _budgetService.GetAllAdHocTransactions().ToList();

        // 1. Paychecks
        foreach (var pay in Paychecks)
        {
            DateTime nextPay = pay.StartDate;
            DateTime endPay = pay.StartDate;
            endPay = pay.Frequency switch
            {
                Frequency.Weekly => endPay.AddDays(7),
                Frequency.BiWeekly => endPay.AddDays(14),
                Frequency.Monthly => endPay.AddMonths(1),
                _ => endPay.AddYears(100)
            };

            while (nextPay < end)
            {
                if (nextPay >= current && (pay.EndDate == null || nextPay <= pay.EndDate))
                {
                    // Association mechanism: check if an AdHocTransaction overrides this paycheck occurrence
                    var adHocOverride = allAdHocs.FirstOrDefault(a => a.PaycheckId == pay.Id && a.Date >= nextPay && a.Date < endPay);

                    if (adHocOverride == null)
                    {
                        int? toAccountId = pay.AccountId ?? cashAccount?.Id;
                        events.Add((nextPay, pay.ExpectedAmount, $"Expected Pay: {pay.Name}", null, toAccountId, null, pay.Id, ProjectionEngine.ProjectionEventType.Paycheck, false, false));
                    }
                }
                
                nextPay = pay.Frequency switch
                {
                    Frequency.Weekly => nextPay.AddDays(7),
                    Frequency.BiWeekly => nextPay.AddDays(14),
                    Frequency.Monthly => nextPay.AddMonths(1),
                    _ => nextPay.AddYears(100)
                };
                endPay = nextPay;
                endPay = pay.Frequency switch
                {
                    Frequency.Weekly => endPay.AddDays(7),
                    Frequency.BiWeekly => endPay.AddDays(14),
                    Frequency.Monthly => endPay.AddMonths(1),
                    _ => endPay.AddYears(100)
                };
            }
        }

        // 2. Bills & Transfers
        var primaryChecking = Accounts.FirstOrDefault(a => a.Type == AccountType.Checking)?.Id;
        var allPeriodBills = _budgetService.GetAllPeriodBills().ToList();
        var allPeriodBuckets = _budgetService.GetAllPeriodBuckets().ToList();

        foreach (var bill in Bills)
        {
            DateTime nextDue = bill.NextDueDate ?? current; 
            if (bill.NextDueDate == null)
            {
                 nextDue = new DateTime(current.Year, current.Month, Math.Min(bill.DueDay, DateTime.DaysInMonth(current.Year, current.Month)));
                 if (nextDue < current) nextDue = nextDue.AddMonths(1);
            }

            while (nextDue < end)
            {
                var pb = allPeriodBills.FirstOrDefault(p => p.BillId == bill.Id && p.DueDate.Date == nextDue.Date);
                decimal amountToUse = (pb != null) ? pb.ActualAmount : bill.ExpectedAmount;
                string paidSuffix = (pb != null && pb.IsPaid) ? " (PAID)" : "";

                if (bill.ToAccountId.HasValue)
                {
                    var fromAccId = bill.AccountId ?? primaryChecking;
                    events.Add((nextDue, amountToUse, $"Transfer: {bill.Name}{paidSuffix}", fromAccId, bill.ToAccountId.Value, null, null, ProjectionEngine.ProjectionEventType.Transfer, false, false));
                }
                else
                {
                    events.Add((nextDue, -amountToUse, $"Bill: {bill.Name}{paidSuffix}", bill.AccountId ?? primaryChecking, null, null, null, ProjectionEngine.ProjectionEventType.Bill, false, false));
                }

                nextDue = bill.Frequency switch
                {
                    Frequency.Monthly => nextDue.AddMonths(1),
                    Frequency.Yearly => nextDue.AddYears(1),
                    Frequency.Weekly => nextDue.AddDays(7),
                    Frequency.BiWeekly => nextDue.AddDays(14),
                    _ => nextDue.AddYears(100)
                };
            }
        }

        // 3. Buckets
        // We'll calculate the actual bucket expenses later by subtracting associated ad-hoc transactions
        foreach (var pay in Paychecks)
        {
            DateTime nextPay = pay.StartDate;
            while (nextPay < end)
            {
                if (nextPay >= current && (pay.EndDate == null || nextPay <= pay.EndDate))
                {
                    foreach (var bucket in Buckets)
                    {
                        var pb = allPeriodBuckets.FirstOrDefault(p => p.BucketId == bucket.Id && p.PeriodDate.Date == nextPay.Date);
                        decimal amountToUse = (pb != null) ? pb.ActualAmount : bucket.ExpectedAmount;
                        // Note: Bucket logic will be handled during event processing
                        events.Add((nextPay, -amountToUse, $"Bucket: {bucket.Name}", bucket.AccountId ?? primaryChecking, null, bucket.Id, null, ProjectionEngine.ProjectionEventType.Bucket, false, false));
                    }
                }
                nextPay = pay.Frequency switch
                {
                    Frequency.Weekly => nextPay.AddDays(7),
                    Frequency.BiWeekly => nextPay.AddDays(14),
                    Frequency.Monthly => nextPay.AddMonths(1),
                    _ => nextPay.AddYears(100)
                };
            }
        }

        // 4. Ad-Hoc Transactions
        foreach (var t in allAdHocs)
        {
            if (t.Date >= current && t.Date < end)
            {
                string bucketSuffix = t.BucketId.HasValue ? $" (Bucket: {Buckets.FirstOrDefault(b => b.Id == t.BucketId)?.Name})" : "";
                if (t.ToAccountId.HasValue)
                {
                    var toAcc = Accounts.FirstOrDefault(a => a.Id == t.ToAccountId.Value);
                    bool isDebtAccount = toAcc != null && (toAcc.Type == AccountType.Mortgage || toAcc.Type == AccountType.PersonalLoan);
                    
                    if (isDebtAccount && t.IsPrincipalOnly)
                    {
                        events.Add((t.Date, t.Amount, $"Ad-Hoc Principal: {t.Description}{bucketSuffix}", t.AccountId, t.ToAccountId.Value, t.BucketId, t.PaycheckId, ProjectionEngine.ProjectionEventType.AdHoc, true, t.IsRebalance));
                    }
                    else
                    {
                        events.Add((t.Date, t.Amount, $"Ad-Hoc Transfer: {t.Description}{bucketSuffix}", t.AccountId, t.ToAccountId.Value, t.BucketId, t.PaycheckId, ProjectionEngine.ProjectionEventType.AdHoc, false, t.IsRebalance));
                    }
                }
                else if (t.AccountId.HasValue)
                {
                    events.Add((t.Date, -t.Amount, $"Ad-Hoc: {t.Description}{bucketSuffix}", t.AccountId, null, t.BucketId, t.PaycheckId, ProjectionEngine.ProjectionEventType.AdHoc, false, t.IsRebalance));
                }
                else
                {
                    events.Add((t.Date, t.Amount, $"Ad-Hoc: {t.Description}{bucketSuffix}", null, null, t.BucketId, t.PaycheckId, ProjectionEngine.ProjectionEventType.AdHoc, false, t.IsRebalance));
                }
            }
        }

        // 5. Investment Earnings & Mortgage Interest
        foreach (var acc in Accounts)
        {
            if (acc.Type == AccountType.Mortgage && acc.MortgageDetails != null)
            {
                DateTime nextInterest = acc.MortgageDetails.PaymentDate;
                if (nextInterest == DateTime.MinValue) nextInterest = current;
                while (nextInterest < current) nextInterest = nextInterest.AddMonths(1);

                while (nextInterest < end)
                {
                    // Check if an AdHocTransaction overrides this interest occurrence (to the same account on this date)
                    var hasInterestAdHoc = allAdHocs.Any(a => a.ToAccountId == acc.Id && a.Date.Date == nextInterest.Date);
                    
                    if (!hasInterestAdHoc)
                    {
                        events.Add((nextInterest, 0, $"Interest: {acc.Name}", acc.Id, null, null, null, ProjectionEngine.ProjectionEventType.Interest, false, false));
                    }
                    nextInterest = nextInterest.AddMonths(1);
                }
            }
        }

        var sortedEvents = events.OrderBy(e => e.Date).ThenByDescending(e => e.Type == ProjectionEngine.ProjectionEventType.Paycheck).ToList();

        // 6. Filter events to start only from the earliest as-of date or today
        // Actually, the user wants projections NOT to start prior to as-of dates.
        // If an event occurs before the as-of date of ALL accounts it affects, should we skip it?
        // Specifically: "projection should not start prior to the as of dates of the earliest check startdate and no bill should pull from an account with an as of date after the bill date."
        
        DateTime earliestPaycheckStart = Paychecks.Any() ? Paychecks.Min(p => p.StartDate) : DateTime.Today;
        DateTime earliestAccountAsOf = Accounts.Any() ? Accounts.Min(a => a.BalanceAsOf) : DateTime.Today;
        DateTime projectionStartDate = earliestPaycheckStart < earliestAccountAsOf ? earliestPaycheckStart : earliestAccountAsOf;
        
        if (current < projectionStartDate)
        {
            current = projectionStartDate;
        }
        
        // Re-filter and re-sort events to ensure they are within the now potentially moved 'current' window
        sortedEvents = sortedEvents.Where(e => e.Date >= current).OrderBy(e => e.Date).ThenByDescending(e => e.Type == ProjectionEngine.ProjectionEventType.Paycheck).ToList();

        var paycheckDates = sortedEvents.Where(e => e.Type == ProjectionEngine.ProjectionEventType.Paycheck || (e.Type == ProjectionEngine.ProjectionEventType.AdHoc && e.PaycheckId.HasValue)).Select(e => e.Date).Distinct().OrderBy(d => d).ToList();
        
        // Ensure the projection start date is considered a period boundary if no paycheck falls on it
        if (!paycheckDates.Any() || paycheckDates[0] > current)
        {
            paycheckDates.Insert(0, current);
        }
        
        // Tracking accumulated growth
        var accumulatedGrowth = Accounts.ToDictionary(a => a.Id, a => 0m);
        // Track bucket spending per period
        var bucketSpending = new Dictionary<(DateTime PeriodDate, int BucketId), decimal>();
        foreach (var t in allAdHocs)
        {
            if (t.BucketId.HasValue)
            {
                // Find which period this ad-hoc falls into
                DateTime periodDate = paycheckDates.LastOrDefault(d => d <= t.Date);
                if (periodDate != DateTime.MinValue)
                {
                    var key = (periodDate, t.BucketId.Value);
                    if (!bucketSpending.ContainsKey(key)) bucketSpending[key] = 0;
                    bucketSpending[key] += Math.Abs(t.Amount);
                }
            }
        }

        // Re-calculate accountBalances based on BalanceAsOf and events before 'current'
        foreach (var acc in Accounts)
        {
            // Reset to current account balance from DB (the UI should have latest)
            accountBalances[acc.Id] = acc.Balance;
            
            // Apply events that happened between acc.BalanceAsOf and current
            var priorEvents = sortedEvents.Where(e => e.Date >= acc.BalanceAsOf && e.Date < current).ToList();
            foreach (var e in priorEvents)
            {
                if (e.AccountId == acc.Id) accountBalances[acc.Id] -= Math.Abs(e.Amount);
                if (e.ToAccountId == acc.Id) accountBalances[acc.Id] += Math.Abs(e.Amount);
            }
        }

        decimal runningBalance = Accounts.Where(a => includedTotalAccounts.Contains(a.Id)).Sum(a => 
        {
            var bal = accountBalances[a.Id];
            return (a.Type == AccountType.Mortgage || a.Type == AccountType.PersonalLoan) ? -bal : bal;
        });

        DateTime lastDate = current;
        var finalProjections = new List<ProjectionItem>();

        foreach (var ev in sortedEvents)
        {
            // Before processing the event, if any IncludeInTotal accounts become effective as of this date,
            // incorporate them into the tracking.
            foreach (var acc in Accounts.Where(a => a.IncludeInTotal && !includedTotalAccounts.Contains(a.Id) && a.BalanceAsOf <= ev.Date))
            {
                includedTotalAccounts.Add(acc.Id);
            }

            // Calculate daily growth
            int days = (ev.Date - lastDate).Days;
            if (days > 0)
            {
                for (int d = 0; d < days; d++)
                {
                    DateTime dayDate = lastDate.AddDays(d);
                    foreach (var acc in Accounts.Where(a => a.AnnualGrowthRate > 0 && a.Type != AccountType.Mortgage))
                    {
                        if (dayDate < accountBalanceDates[acc.Id]) continue;

                        decimal dailyRate = acc.AnnualGrowthRate / 100m / 365m;
                        decimal growth = accountBalances[acc.Id] * dailyRate;
                        accumulatedGrowth[acc.Id] += growth;
                        
                        if (accumulatedGrowth[acc.Id] >= 0.01m || accumulatedGrowth[acc.Id] <= -0.01m)
                        {
                            decimal toAdd = Math.Round(accumulatedGrowth[acc.Id], 2);
                            accountBalances[acc.Id] += toAdd;
                            if (includedTotalAccounts.Contains(acc.Id))
                            {
                                if (acc.Type == AccountType.Mortgage || acc.Type == AccountType.PersonalLoan)
                                {
                                    runningBalance -= toAdd;
                                }
                                else
                                {
                                    runningBalance += toAdd;
                                }
                            }
                            accumulatedGrowth[acc.Id] -= toAdd;
                        }
                    }
                }
            }
            lastDate = ev.Date;

            // Handle Accumulated Growth on Paycheck
            if (ev.Type == ProjectionEngine.ProjectionEventType.Paycheck)
            {
                foreach (var accId in accumulatedGrowth.Keys.ToList())
                {
                    if (accumulatedGrowth[accId] > 0)
                    {
                        var acc = Accounts.First(a => a.Id == accId);
                        
                        // Calculate current total balance from all included effective accounts
                        decimal currentRunningBalance = 0;
                        foreach (var incId in includedTotalAccounts)
                        {
                            var incAcc = Accounts.First(a => a.Id == incId);
                            if (incAcc.Type == AccountType.Mortgage || incAcc.Type == AccountType.PersonalLoan)
                                currentRunningBalance -= accountBalances[incId];
                            else
                                currentRunningBalance += accountBalances[incId];
                        }

                        finalProjections.Add(new ProjectionItem
                        {
                            Date = ev.Date,
                            Description = $"Accumulated Growth: {acc.Name}",
                            Amount = accumulatedGrowth[accId],
                            Balance = currentRunningBalance,
                            IsWarning = currentRunningBalance < 0,
                            AccountBalances = accountBalances.ToDictionary(kvp => accountNames[kvp.Key], kvp => kvp.Value)
                        });
                        accumulatedGrowth[accId] = 0;
                    }
                }
            }

            decimal amount = ev.Amount;
            string description = ev.Description;

            // Skip events occurring before account balance date
            if (ev.AccountId.HasValue && ev.Date < accountBalanceDates[ev.AccountId.Value] && ev.Type != ProjectionEngine.ProjectionEventType.Bucket)
            {
                // We don't process balance impact for accounts whose balance date is in the future relative to this event
                // This satisfies: "no bill should pull from an account with an as of date after the bill date"
                amount = 0; 
                if (ev.Type == ProjectionEngine.ProjectionEventType.Transfer || ev.Type == ProjectionEngine.ProjectionEventType.AdHoc)
                {
                     // If it was a transfer, we might still want it to land in the 'To' account if that account's as-of date HAS passed.
                     // But the source side is ignored.
                }
                else
                {
                    // For normal bills, if as-of date hasn't passed, the bill doesn't impact the balance.
                }
            }
            
            // Similar check for ToAccountId
            if (ev.ToAccountId.HasValue && ev.Date < accountBalanceDates[ev.ToAccountId.Value])
            {
                // If the target account's balance is not yet effective, the deposit/transfer doesn't land.
                // We should probably zero out the landing impact.
                // Since 'amount' might be used for BOTH from and to, we need to be careful.
            }


            if (ev.Type == ProjectionEngine.ProjectionEventType.Bucket && ev.BucketId.HasValue)
            {
                // Find how much has been spent from this bucket in this period so far
                DateTime periodDate = paycheckDates.LastOrDefault(d => d <= ev.Date);
                if (periodDate != DateTime.MinValue)
                {
                    var key = (periodDate, ev.BucketId.Value);
                    decimal spent = bucketSpending.ContainsKey(key) ? bucketSpending[key] : 0;
                    decimal projectedAmount = Math.Abs(amount);
                    amount = -Math.Max(0, projectedAmount - spent);
                }
            }

            if (ev.Type == ProjectionEngine.ProjectionEventType.Interest)
            {
                var acc = Accounts.FirstOrDefault(a => a.Id == ev.AccountId);
                if (acc != null && acc.Type == AccountType.Mortgage && acc.MortgageDetails != null)
                {
                    // Interest accrues on current balance (which is positive principal in this logic)
                    decimal monthlyRate = acc.MortgageDetails.InterestRate / 100 / 12;
                    amount = accountBalances[ev.AccountId!.Value] * monthlyRate;
                    // Interest increases the debt (balance)
                    accountBalances[ev.AccountId.Value] += amount;
                }
            }
            else if (ev.ToAccountId.HasValue)
            {
                // It's a transfer OR a debt payment OR a paycheck deposit
                var toAcc = Accounts.FirstOrDefault(a => a.Id == ev.ToAccountId.Value);
                bool isMortgagePayment = toAcc != null && toAcc.Type == AccountType.Mortgage;
                bool isPersonalLoanPayment = toAcc != null && toAcc.Type == AccountType.PersonalLoan;
                bool isPrincipalOnly = ev.IsPrincipalOnly;
                bool isPaycheck = ev.Type == ProjectionEngine.ProjectionEventType.Paycheck;

                // Robust interest/rebalance detection as per requirements:
                // "If an ad-hoc transaction has a toaccountifd that is an interest accruing account, 
                // then the record is either interest or a rebalance of the account and should be treated as the interest for that period."
                bool isInterestOrRebalance = isMortgagePayment && ev.Type == ProjectionEngine.ProjectionEventType.AdHoc && ev.IsRebalance;

                decimal amountToTransfer = amount;
                
                // Check if 'To' account as-of date has passed
                bool toAccountEffective = !ev.ToAccountId.HasValue || ev.Date >= accountBalanceDates[ev.ToAccountId.Value];
                // Check if 'From' account as-of date has passed
                bool fromAccountEffective = !ev.AccountId.HasValue || ev.Date >= accountBalanceDates[ev.AccountId.Value];

                if (isInterestOrRebalance)
                {
                    // Treated as interest or rebalance - increases the debt balance
                    if (toAccountEffective) accountBalances[ev.ToAccountId.Value] += amount;
                    // Usually interest doesn't have a from account, but if it does, it might be a rebalance from another account.
                    if (ev.AccountId.HasValue && fromAccountEffective) accountBalances[ev.AccountId.Value] -= amount;
                }
                else if (isMortgagePayment && toAcc!.MortgageDetails != null)
                {
                    // Mortgage payment: The portion that reduces the balance is the total payment minus escrow and insurance.
                    // Interest is handled by separate "Interest:" events and should not be subtracted from the payment here.
                    // Principal reduction = Total Payment - Escrow - Mortgage Insurance.
                    
                    decimal principal = amount;
                    if (!isPrincipalOnly)
                    {
                        principal = amount - toAcc.MortgageDetails.Escrow - toAcc.MortgageDetails.MortgageInsurance;
                    }
                    
                    if (principal < 0) principal = 0; 
                    
                    amountToTransfer = principal;

                    if (ev.AccountId.HasValue && fromAccountEffective) accountBalances[ev.AccountId.Value] -= amount; // Full amount out from source
                    if (toAccountEffective) accountBalances[ev.ToAccountId.Value] -= amountToTransfer; // Subtract from debt balance
                }
                else if (isPersonalLoanPayment && isPrincipalOnly)
                {
                    // Ad-hoc principal only payment to a personal loan
                    if (ev.AccountId.HasValue && fromAccountEffective) accountBalances[ev.AccountId.Value] -= amount;
                    if (toAccountEffective) accountBalances[ev.ToAccountId.Value] -= amount;
                    amountToTransfer = amount;
                }
                else if (isPaycheck)
                {
                    // Paycheck deposit
                    if (toAccountEffective) accountBalances[ev.ToAccountId.Value] += amount;
                }
                else
                {
                    // Regular transfer or standard personal loan payment (treated as transfer for now unless amortization is added)
                    if (ev.AccountId.HasValue && fromAccountEffective) accountBalances[ev.AccountId.Value] -= amount;
                    if (toAccountEffective) accountBalances[ev.ToAccountId.Value] += amount;
                }
            }
            else
            {
                // Normal income/expense/growth/interest
                if (ev.AccountId.HasValue)
                {
                    bool fromAccountEffective = ev.Date >= accountBalanceDates[ev.AccountId.Value];
                    if (fromAccountEffective)
                    {
                        accountBalances[ev.AccountId.Value] += amount;
                    }
                }
                else
                {
                    // No account specified (like paycheck or default bill), assumed primary
                    // If no account is specified, it normally implies the primary checking account.
                    // If there's no as-of date to check, we might want to default to 'effective'.
                    // But usually, all accounts have an as-of date.
                    bool effective = true;
                    if (primaryChecking.HasValue)
                    {
                        effective = ev.Date >= accountBalanceDates[primaryChecking.Value];
                    }
                    
                    if (effective)
                    {
                        if (primaryChecking.HasValue)
                        {
                            accountBalances[primaryChecking.Value] += amount;
                        }
                    }
                }
            }

            // Calculate current total balance from all included effective accounts
            decimal currentEventRunningBalance = 0;
            foreach (var incId in includedTotalAccounts)
            {
                var incAcc = Accounts.First(a => a.Id == incId);
                if (incAcc.Type == AccountType.Mortgage || incAcc.Type == AccountType.PersonalLoan)
                    currentEventRunningBalance -= accountBalances[incId];
                else
                    currentEventRunningBalance += accountBalances[incId];
            }

            var item = new ProjectionItem 
            { 
                Date = ev.Date, 
                Description = description, 
                Amount = amount, 
                Balance = currentEventRunningBalance,
                IsWarning = currentEventRunningBalance < 0,
                AccountBalances = accountBalances.ToDictionary(kvp => accountNames[kvp.Key], kvp => kvp.Value)
            };
            finalProjections.Add(item);

            // Update the tracker runningBalance for growth calculations in the next iteration
            runningBalance = currentEventRunningBalance;
        }

        // Handle any remaining growth after the last event until the end date
        int finalDays = (end - lastDate).Days;
        if (finalDays > 0)
        {
            for (int d = 0; d < finalDays; d++)
            {
                DateTime dayDate = lastDate.AddDays(d);
                foreach (var acc in Accounts.Where(a => a.AnnualGrowthRate > 0 && a.Type != AccountType.Mortgage))
                {
                    if (dayDate < accountBalanceDates[acc.Id]) continue;

                    decimal dailyRate = acc.AnnualGrowthRate / 100m / 365m;
                    decimal growth = accountBalances[acc.Id] * dailyRate;
                    accumulatedGrowth[acc.Id] += growth;
                    
                    if (accumulatedGrowth[acc.Id] >= 0.01m || accumulatedGrowth[acc.Id] <= -0.01m)
                    {
                        decimal toAdd = Math.Round(accumulatedGrowth[acc.Id], 2);
                        accountBalances[acc.Id] += toAdd;
                        if (includedTotalAccounts.Contains(acc.Id))
                        {
                            if (acc.Type == AccountType.Mortgage || acc.Type == AccountType.PersonalLoan)
                            {
                                runningBalance -= toAdd;
                            }
                            else
                            {
                                runningBalance += toAdd;
                            }
                        }
                        accumulatedGrowth[acc.Id] -= toAdd;
                    }
                }
            }
        }
        
        finalProjections.Add(new ProjectionItem
        {
            Date = end,
            Description = "End of Projection",
            Amount = 0,
            Balance = runningBalance,
            IsWarning = runningBalance < 0,
            AccountBalances = accountBalances.ToDictionary(kvp => accountNames[kvp.Key], kvp => kvp.Value)
        });

        // Net per period
        for (int i = 0; i < paycheckDates.Count; i++)
        {
            DateTime start = paycheckDates[i];
            DateTime next = (i + 1 < paycheckDates.Count) ? paycheckDates[i + 1] : end;
            var periodEvents = finalProjections.Where(item => item.Date >= start && item.Date < next).ToList();
            if (periodEvents.Any())
            {
                periodEvents.First().PeriodNet = periodEvents.Sum(item => item.Amount);
            }
        }

            Projections = new ObservableCollection<ProjectionItem>(finalProjections);
        }
        finally
        {
            _isCalculatingProjections = false;
        }
    }
}

public class ProjectionItem : ViewModelBase
{
    private DateTime _date;
    private string _description = string.Empty;
    private decimal _amount;
    private decimal _balance;
    private bool _isWarning;
    private decimal? _periodNet;
    private Dictionary<string, decimal> _accountBalances = new();

    public DateTime Date { get => _date; set => SetProperty(ref _date, value); }
    public string Description { get => _description; set => SetProperty(ref _description, value); }
    public decimal Amount 
    { 
        get => _amount; 
        set
        {
            if (SetProperty(ref _amount, value))
            {
                if (Description.StartsWith("Pay:") && Date.Date <= DateTime.Today.Date)
                {
                    // Update or create AdHocTransaction
                    var mainVM = MainViewModel.Instance;
                    if (mainVM != null)
                    {
                        var adHocs = mainVM.CurrentPeriodAdHocTransactions; // This might be the wrong period if we are looking far ahead
                        // Actually, we should use the service to find/create it based on Date and Description
                        var existing = mainVM.GetAdHocForPaycheck(Description, Date);
                        if (existing != null)
                        {
                            existing.Amount = value;
                        }
                        else
                        {
                            // Create new one
                            var cashAccount = mainVM.Accounts.FirstOrDefault(a => a.Name == "Household Cash");
                            var paycheck = mainVM.Paychecks.FirstOrDefault(p => $"Pay: {p.Name}" == Description);

                            // Find the correct period date for this Date
                            DateTime pDate = mainVM.FindPeriodDateFor(Date);

                            var adHoc = new AdHocTransaction
                            {
                                Description = Description,
                                Amount = value,
                                Date = Date,
                                PeriodDate = pDate,
                                ToAccountId = paycheck?.AccountId ?? cashAccount?.Id
                            };
                            mainVM.SaveNewAdHoc(adHoc);
                        }
                        if (mainVM.IsCalculatingProjections) return;
                        mainVM.CalculateProjections();
                    }
                }
            }
        }
    }
    public decimal Balance { get => _balance; set => SetProperty(ref _balance, value); }
    public bool IsWarning { get => _isWarning; set => SetProperty(ref _isWarning, value); }
    public decimal? PeriodNet { get => _periodNet; set => SetProperty(ref _periodNet, value); }
    
    public Dictionary<string, decimal> AccountBalances
    {
        get => _accountBalances;
        set => SetProperty(ref _accountBalances, value);
    }

    public decimal GetAccountBalance(string accountName)
    {
        return _accountBalances.TryGetValue(accountName, out var bal) ? bal : 0;
    }
}
