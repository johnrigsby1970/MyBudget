using System.Collections.ObjectModel;
using StayOnTarget.Models;

namespace StayOnTarget.ViewModels;

public class ReassignAccountDependenciesViewModel : ViewModelBase
{
    public class ReassignItem<T> : ViewModelBase
    {
        public T Item { get; }
        public string Description { get; }
        private int? _targetAccountId;

        public int? TargetAccountId
        {
            get => _targetAccountId;
            set => SetProperty(ref _targetAccountId, value);
        }

        public ReassignItem(T item, string description, int? currentAccountId)
        {
            Item = item;
            Description = description;
            _targetAccountId = currentAccountId;
        }
    }

    public class ReassignBillItem : ViewModelBase
    {
        public Bill Bill { get; }
        public string Description { get; }
        
        private int? _targetAccountId;
        public int? TargetAccountId
        {
            get => _targetAccountId;
            set => SetProperty(ref _targetAccountId, value);
        }

        private int? _targetToAccountId;
        public int? TargetToAccountId
        {
            get => _targetToAccountId;
            set => SetProperty(ref _targetToAccountId, value);
        }

        public bool ShowAccountId { get; }
        public bool ShowToAccountId { get; }

        public ReassignBillItem(Bill bill, int sourceAccountId)
        {
            Bill = bill;
            Description = bill.Name;
            ShowAccountId = bill.AccountId == sourceAccountId;
            ShowToAccountId = bill.ToAccountId == sourceAccountId;
            _targetAccountId = bill.AccountId;
            _targetToAccountId = bill.ToAccountId;
        }
    }

    private ObservableCollection<ReassignItem<Paycheck>> _paychecks = new();
    public ObservableCollection<ReassignItem<Paycheck>> Paychecks
    {
        get => _paychecks;
        set => SetProperty(ref _paychecks, value);
    }

    private ObservableCollection<ReassignBillItem> _bills = new();
    public ObservableCollection<ReassignBillItem> Bills
    {
        get => _bills;
        set => SetProperty(ref _bills, value);
    }

    private ObservableCollection<ReassignItem<BudgetBucket>> _buckets = new();
    public ObservableCollection<ReassignItem<BudgetBucket>> Buckets
    {
        get => _buckets;
        set => SetProperty(ref _buckets, value);
    }

    public List<Account> AvailableAccounts { get; }
    public List<Account> AvailableAccountsWithNone { get; }

    private static readonly Account NoneAccount = new Account { Id = -1, Name = "None / Unassigned" };

    private Account? _globalTargetAccount;
    public Account? GlobalTargetAccount
    {
        get => _globalTargetAccount;
        set
        {
            if (SetProperty(ref _globalTargetAccount, value))
            {
                ApplyGlobalReassignment(value?.Id == -1 ? null : value?.Id);
            }
        }
    }

    public ReassignAccountDependenciesViewModel(
        IEnumerable<Paycheck> paychecks,
        IEnumerable<Bill> bills,
        IEnumerable<BudgetBucket> buckets,
        IEnumerable<Account> availableAccounts,
        int sourceAccountId)
    {
        AvailableAccounts = availableAccounts.ToList();
        AvailableAccountsWithNone = new List<Account> { NoneAccount }.Concat(AvailableAccounts).ToList();

        foreach (var p in paychecks)
            Paychecks.Add(new ReassignItem<Paycheck>(p, p.Name, p.AccountId));

        foreach (var b in bills)
            Bills.Add(new ReassignBillItem(b, sourceAccountId));

        foreach (var b in buckets)
            Buckets.Add(new ReassignItem<BudgetBucket>(b, b.Name, b.AccountId));

        _globalTargetAccount = NoneAccount;
        ApplyGlobalReassignment(null);
    }

    private void ApplyGlobalReassignment(int? targetAccountId)
    {
        foreach (var p in Paychecks) p.TargetAccountId = targetAccountId;
        foreach (var b in Bills)
        {
            if (b.ShowAccountId) b.TargetAccountId = targetAccountId;
            if (b.ShowToAccountId) b.TargetToAccountId = targetAccountId;
        }
        foreach (var b in Buckets) b.TargetAccountId = targetAccountId;
    }

}
