using System.Collections.ObjectModel;
using StayOnTarget.Models;
using StayOnTarget.Services;
using StayOnTarget.ViewModels;

namespace StayOnTarget.Views.Wizard;

public class DatabaseInitializationContext : ViewModelBase
{
    public BudgetService? BudgetService { get; set; }
    public ObservableCollection<Account> Accounts { get; } = new();
    
    private ObservableCollection<Account> _activeAccountsWithNone = new();
    
    public ObservableCollection<Account> ActiveAccountsWithNone {
        get => _activeAccountsWithNone;
        set => SetProperty(ref _activeAccountsWithNone, value);
    }

    
    public ObservableCollection<Paycheck> Paychecks { get; } = new();
    public ObservableCollection<Bill> Bills { get; } = new();
    public ObservableCollection<BudgetBucket> Buckets { get; } = new();
}