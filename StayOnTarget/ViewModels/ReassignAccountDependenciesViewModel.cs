using System.Collections.ObjectModel;
using Serilog;
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
            set
            {
                try
                {
                    SetProperty(ref _targetAccountId, value);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error setting TargetAccountId in ReassignItem.");
                    
                }
            }
        }

        public ReassignItem(T item, string description, int? currentAccountId)
        {
            try
            {
                Item = item;
                Description = description;
                _targetAccountId = currentAccountId;
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Critical error initializing ReassignItem.");
                
            }
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
            set
            {
                try
                {
                    SetProperty(ref _targetAccountId, value);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error setting TargetAccountId in ReassignBillItem.");
                    
                }
            }
        }

        private int? _targetToAccountId;
        public int? TargetToAccountId
        {
            get => _targetToAccountId;
            set
            {
                try
                {
                    SetProperty(ref _targetToAccountId, value);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error setting TargetToAccountId in ReassignBillItem.");
                    
                }
            }
        }

        public bool ShowAccountId { get; }
        public bool ShowToAccountId { get; }

        public ReassignBillItem(Bill bill, int sourceAccountId)
        {
            try
            {
                Bill = bill;
                Description = bill.Name;
                ShowAccountId = bill.AccountId == sourceAccountId;
                ShowToAccountId = bill.ToAccountId == sourceAccountId;
                _targetAccountId = bill.AccountId;
                _targetToAccountId = bill.ToAccountId;
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Critical error initializing ReassignBillItem.");
                
            }
        }
    }

    private ObservableCollection<ReassignItem<Paycheck>> _paychecks = new();
    public ObservableCollection<ReassignItem<Paycheck>> Paychecks
    {
        get => _paychecks;
        set
        {
            try
            {
                SetProperty(ref _paychecks, value);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error setting Paychecks collection in ReassignAccountDependenciesViewModel.");
                
            }
        }
    }

    private ObservableCollection<ReassignBillItem> _bills = new();
    public ObservableCollection<ReassignBillItem> Bills
    {
        get => _bills;
        set
        {
            try
            {
                SetProperty(ref _bills, value);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error setting Bills collection in ReassignAccountDependenciesViewModel.");
                
            }
        }
    }

    private ObservableCollection<ReassignItem<BudgetBucket>> _buckets = new();
    public ObservableCollection<ReassignItem<BudgetBucket>> Buckets
    {
        get => _buckets;
        set
        {
            try
            {
                SetProperty(ref _buckets, value);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error setting Buckets collection in ReassignAccountDependenciesViewModel.");
                
            }
        }
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
            try
            {
                if (SetProperty(ref _globalTargetAccount, value))
                {
                    ApplyGlobalReassignment(value?.Id == -1 ? null : value?.Id);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error setting GlobalTargetAccount in ReassignAccountDependenciesViewModel.");
                
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
        try
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
        catch (Exception ex)
        {
            Log.Fatal(ex, "Critical error initializing ReassignAccountDependenciesViewModel.");
            
        }
    }

    private void ApplyGlobalReassignment(int? targetAccountId)
    {
        try
        {
            foreach (var p in Paychecks) p.TargetAccountId = targetAccountId;
            foreach (var b in Bills)
            {
                if (b.ShowAccountId) b.TargetAccountId = targetAccountId;
                if (b.ShowToAccountId) b.TargetToAccountId = targetAccountId;
            }
            foreach (var b in Buckets) b.TargetAccountId = targetAccountId;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error applying global reassignment in ReassignAccountDependenciesViewModel.");
            
        }
    }
}