using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using StayOnTarget.Models;
using StayOnTarget.Views.Wizard;

namespace StayOnTarget.ViewModels.Wizard;

public partial class BillSetupViewModel : ViewModelBase, IWizardStepViewModel
{
    public string StepTitle { get; }
    public int StepIndex { get; }
    public bool IsValid => true;
    
    public ObservableCollection<Account> Accounts => DatabaseInitializationContext.Accounts;
    public ObservableCollection<Account> ActiveAccountsWithNone => DatabaseInitializationContext.ActiveAccountsWithNone;
    
    public ObservableCollection<Bill> Bills => DatabaseInitializationContext.Bills;
    
    private DatabaseInitializationContext DatabaseInitializationContext { get; }
    
    public IEnumerable<Frequency> BillFrequencies { get; } = new[] { Frequency.Monthly, Frequency.Yearly };

    private Bill _editingBill = new()
    {
        Name = "",
        Frequency = Frequency.Monthly,
        DueDay = 1,
        IsActive = true
    };

    public Bill EditingBill
    {
        get => _editingBill;
        set => SetProperty(ref _editingBill, value);
    }

    public BillSetupViewModel(DatabaseInitializationContext ctx)
    {
        DatabaseInitializationContext = ctx;
        StepTitle = "Bills";
        StepIndex = 3;
    }

    public void OnStepNavigatedTo()
    {
        if (EditingBill.AccountId == 0 && Accounts.Any())
        {
            EditingBill.AccountId = Accounts.First().Id;
            OnPropertyChanged(nameof(EditingBill));
        }

        OnPropertyChanged(nameof(Accounts));
        OnPropertyChanged(nameof(ActiveAccountsWithNone));
        OnPropertyChanged(nameof(Bills));
        OnPropertyChanged(nameof(IsValid));
    }

    [RelayCommand]
    private async Task AddBillAsync()
    {
        if (string.IsNullOrWhiteSpace(EditingBill.Name)) return;
        if (DatabaseInitializationContext.BudgetService == null) return;

        var bill = new Bill
        {
            Name = EditingBill.Name,
            ExpectedAmount = EditingBill.ExpectedAmount,
            Frequency = EditingBill.Frequency,
            DueDay = EditingBill.DueDay,
            AccountId = EditingBill.AccountId,
            ToAccountId = EditingBill.ToAccountId,
            Category = EditingBill.Category,
            IsActive = true
        };

        await DatabaseInitializationContext.BudgetService.UpsertBillAsync(bill);
        
        var allBills = await DatabaseInitializationContext.BudgetService.GetAllBillsAsync();
        Bills.Clear();
        foreach (var b in allBills)
        {
            Bills.Add(b);
        }

        // Reset
        EditingBill = new Bill
        {
            Name = "",
            Frequency = Frequency.Monthly,
            DueDay = 1,
            AccountId = Accounts.FirstOrDefault()?.Id
        };
    }

    [RelayCommand]
    private async Task DeleteBillAsync(Bill? bill)
    {
        if (bill == null || DatabaseInitializationContext.BudgetService == null) return;
        
        await DatabaseInitializationContext.BudgetService.DeleteBillAsync(bill.Id);
        Bills.Remove(bill);
    }
}