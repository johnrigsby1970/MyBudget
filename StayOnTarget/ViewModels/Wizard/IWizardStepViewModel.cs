using System.ComponentModel;

namespace StayOnTarget.ViewModels.Wizard;

public interface IWizardStepViewModel : INotifyPropertyChanged
{
    string StepTitle { get; }
    int StepIndex { get; }
    bool IsValid { get; }
    void OnStepNavigatedTo();
}