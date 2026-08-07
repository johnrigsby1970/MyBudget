using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using StayOnTarget.Models;
using StayOnTarget.Views.Wizard;

namespace StayOnTarget.ViewModels.Wizard;

public partial class BucketSetupViewModel : ViewModelBase, IWizardStepViewModel
{
    public string StepTitle { get; }
    public int StepIndex { get; }
    public bool IsValid => true;
    private DatabaseInitializationContext DatabaseInitializationContext { get; }

    public ObservableCollection<BudgetBucket> Buckets => DatabaseInitializationContext.Buckets;
    public ObservableCollection<Account> Accounts => DatabaseInitializationContext.Accounts;
    public ObservableCollection<Paycheck> Paychecks => DatabaseInitializationContext.Paychecks;

    private BudgetBucket _editingBucket = new()
    {
        Name = "New Envelope",
        ExpectedAmount = 0
    };

    public BudgetBucket EditingBucket
    {
        get => _editingBucket;
        set => SetProperty(ref _editingBucket, value);
    }

    public BucketSetupViewModel(DatabaseInitializationContext ctx)
    {
        DatabaseInitializationContext = ctx;
        StepTitle = "Envelopes";
        StepIndex = 4;
    }

    public void OnStepNavigatedTo()
    {
        if (EditingBucket.AccountId == 0 && Accounts.Any())
        {
            EditingBucket.AccountId = Accounts.First().Id;
            OnPropertyChanged(nameof(EditingBucket));
        }

        OnPropertyChanged(nameof(Accounts));
        OnPropertyChanged(nameof(Paychecks));
        OnPropertyChanged(nameof(Buckets));
        OnPropertyChanged(nameof(IsValid));
    }

    [RelayCommand]
    private async Task AddBucketAsync()
    {
        if (string.IsNullOrWhiteSpace(EditingBucket.Name)) return;
        if (DatabaseInitializationContext.BudgetService == null) return;

        var bucket = new BudgetBucket
        {
            Name = EditingBucket.Name,
            ExpectedAmount = EditingBucket.ExpectedAmount,
            AccountId = EditingBucket.AccountId
        };

        await DatabaseInitializationContext.BudgetService.UpsertBucketAsync(bucket, null);
        
        var allBuckets = await DatabaseInitializationContext.BudgetService.GetAllBucketsAsync();
        Buckets.Clear();
        foreach (var b in allBuckets)
        {
            Buckets.Add(b);
        }

        // Reset
        EditingBucket = new BudgetBucket
        {
            Name = "",
            ExpectedAmount = 0,
            AccountId = Accounts.FirstOrDefault()?.Id
        };
    }

    [RelayCommand]
    private async Task DeleteBucketAsync(BudgetBucket? bucket)
    {
        if (bucket == null || DatabaseInitializationContext.BudgetService == null) return;
        
        await DatabaseInitializationContext.BudgetService.DeleteBucketAsync(bucket.Id);
        Buckets.Remove(bucket);
    }
}