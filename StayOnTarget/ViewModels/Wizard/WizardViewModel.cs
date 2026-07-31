using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StayOnTarget.Views.Wizard;

namespace StayOnTarget.ViewModels.Wizard;

public partial class WizardViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanGoNext), nameof(CanGoPrevious), nameof(IsLastStep), nameof(CanFinish))]
    private int _currentStepIndex;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanGoNext), nameof(CanGoPrevious), nameof(IsLastStep), nameof(CanFinish))]
    private IWizardStepViewModel _currentStep;

    public ObservableCollection<IWizardStepViewModel> Steps { get; } = new();

    public bool CanGoPrevious => CurrentStepIndex > 0;
    public bool CanGoNext => CurrentStep != null && CurrentStep.IsValid && CurrentStepIndex < Steps.Count - 1;
    public bool IsLastStep => CurrentStepIndex == Steps.Count - 1;
    public bool CanFinish => CurrentStepIndex == Steps.Count - 1 && CurrentStep != null && CurrentStep.IsValid;

    private DatabaseInitializationContext DatabaseInitializationContext { get; }
    
    public WizardViewModel(DatabaseInitializationContext ctx)
    {
        DatabaseInitializationContext = ctx;
        Steps = [
            new DatabaseNameViewModel(DatabaseInitializationContext),
            new AccountSetupViewModel(DatabaseInitializationContext),
            new PaycheckSetupViewModel(DatabaseInitializationContext),
            new BillSetupViewModel(DatabaseInitializationContext),
            new BucketSetupViewModel(DatabaseInitializationContext)
        ];

        CurrentStepIndex = 0;
        CurrentStep = Steps[0];
        CurrentStep.PropertyChanged += OnStepPropertyChanged;
        CurrentStep.OnStepNavigatedTo();
    }

    partial void OnCurrentStepChanging(IWizardStepViewModel value)
    {
        if (CurrentStep != null)
        {
            CurrentStep.PropertyChanged -= OnStepPropertyChanged;
        }
    }

    partial void OnCurrentStepChanged(IWizardStepViewModel value)
    {
        if (value != null)
        {
            value.PropertyChanged += OnStepPropertyChanged;
        }

        value?.OnStepNavigatedTo();
        NextCommand.NotifyCanExecuteChanged();
        PreviousCommand.NotifyCanExecuteChanged();
        FinishCommand.NotifyCanExecuteChanged();
    }

    private void OnStepPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IWizardStepViewModel.IsValid))
        {
            NextCommand.NotifyCanExecuteChanged();
            FinishCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(CanGoNext));
            OnPropertyChanged(nameof(CanFinish));
        }
    }
    // public WizardViewModel(IEnumerable<IWizardStepViewModel> steps)
    // {
    //     foreach (var step in steps)
    //     {
    //         Steps.Add(step);
    //     }
    //
    //     if (Steps.Count > 0)
    //     {
    //         CurrentStepIndex = 0;
    //         CurrentStep = Steps[0];
    //     }
    // }

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private void Next()
    {
        if (CurrentStepIndex < Steps.Count - 1)
        {
            CurrentStepIndex++;
            CurrentStep = Steps[CurrentStepIndex];
        }
    }

    [RelayCommand(CanExecute = nameof(CanGoPrevious))]
    private void Previous()
    {
        if (CurrentStepIndex > 0)
        {
            CurrentStepIndex--;
            CurrentStep = Steps[CurrentStepIndex];
        }
    }

    [RelayCommand(CanExecute = nameof(CanFinish))]
    private void Finish()
    {
        // For the wizard to complete, we need to signal success back to App.xaml.cs
        // This is usually done via a property or by closing the window with a result.
        // Since WizardViewModel doesn't have a direct reference to the window,
        // we can use a callback or an event, but let's see how WizardWindow handles it.
        
        // Before finishing, ensure everything is saved (though we've been saving step by step)
        
        // Signal completion
        CompletionCallback?.Invoke(true);
    }

    public Action<bool>? CompletionCallback { get; set; }
}