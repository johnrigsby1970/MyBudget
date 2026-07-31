using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using StayOnTarget.Models;
using StayOnTarget.Views.Wizard;

namespace StayOnTarget.ViewModels.Wizard;

public partial class PaycheckSetupViewModel : ViewModelBase, IWizardStepViewModel
{
    public string StepTitle { get; }
    public int StepIndex { get; }
    public bool IsValid => true; // Paycheck is not required
    private DatabaseInitializationContext DatabaseInitializationContext { get; }

    public ObservableCollection<Paycheck> Paychecks => DatabaseInitializationContext.Paychecks;
    public ObservableCollection<Account> Accounts => DatabaseInitializationContext.Accounts;
    public Frequency[] Frequencies => (Frequency[])Enum.GetValues(typeof(Frequency));

    private Paycheck _editingPaycheck = new()
    {
        Name = "Primary Paycheck",
        Frequency = Frequency.BiWeekly,
        StartDate = DateTime.Today
    };

    public Paycheck EditingPaycheck
    {
        get => _editingPaycheck;
        set => SetProperty(ref _editingPaycheck, value);
    }

    public PaycheckSetupViewModel(DatabaseInitializationContext ctx)
    {
        DatabaseInitializationContext = ctx;
        StepTitle = "Paycheck Setup";
        StepIndex = 2;
    }

    public void OnStepNavigatedTo()
    {
        // Default to the first account if available and none selected
        if (EditingPaycheck.AccountId == 0 && Accounts.Any())
        {
            EditingPaycheck.AccountId = Accounts.First().Id;
            OnPropertyChanged(nameof(EditingPaycheck));
        }
        
        OnPropertyChanged(nameof(Accounts));
        OnPropertyChanged(nameof(Paychecks));
        OnPropertyChanged(nameof(IsValid));
    }

    [RelayCommand]
    private async Task AddPaycheckAsync()
    {
        if (string.IsNullOrWhiteSpace(EditingPaycheck.Name)) return;
        if (DatabaseInitializationContext.BudgetService == null) return;

        var paycheck = new Paycheck
        {
            Name = EditingPaycheck.Name,
            ExpectedAmount = EditingPaycheck.ExpectedAmount,
            Frequency = EditingPaycheck.Frequency,
            StartDate = EditingPaycheck.StartDate,
            AccountId = EditingPaycheck.AccountId
        };

        await DatabaseInitializationContext.BudgetService.UpsertPaycheckAsync(paycheck);
        
        // Since UpsertPaycheckAsync doesn't return the ID, we might need to fetch it or rely on the fact 
        // that for simple wizard setup, we just need them in the list.
        // But let's fetch all to be sure we have the IDs for deletion.
        var allPaychecks = await DatabaseInitializationContext.BudgetService.GetAllPaychecksAsync();
        Paychecks.Clear();
        foreach (var p in allPaychecks)
        {
            Paychecks.Add(p);
        }

        // Reset
        EditingPaycheck = new Paycheck
        {
            Name = "",
            Frequency = Frequency.BiWeekly,
            StartDate = DateTime.Today,
            AccountId = Accounts.FirstOrDefault()?.Id
        };
    }

    [RelayCommand]
    private async Task DeletePaycheckAsync(Paycheck? paycheck)
    {
        if (paycheck == null || DatabaseInitializationContext.BudgetService == null) return;
        
        await DatabaseInitializationContext.BudgetService.DeletePaycheckAsync(paycheck.Id);
        Paychecks.Remove(paycheck);
    }
}