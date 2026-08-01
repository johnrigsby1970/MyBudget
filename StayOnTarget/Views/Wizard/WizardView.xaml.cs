using StayOnTarget.ViewModels.Wizard;

namespace StayOnTarget.Views.Wizard;

public partial class WizardView {
    public WizardView()
    {
        InitializeComponent();
    }

    public WizardView(WizardViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel; // Sets the DataContext directly
    }
}