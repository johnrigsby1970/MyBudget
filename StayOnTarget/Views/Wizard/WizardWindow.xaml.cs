using System.Windows;
using StayOnTarget.ViewModels.Wizard;

namespace StayOnTarget.Views.Wizard;

public partial class WizardWindow : Window
{
    public WizardWindow(WizardViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        viewModel.CompletionCallback = (success) =>
        {
            if (success)
            {
                DialogResult = true;
                Close();
            }
        };
    }
}