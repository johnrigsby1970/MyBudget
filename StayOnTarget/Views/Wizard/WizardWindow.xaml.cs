using System.Windows;
using Serilog;
using StayOnTarget.ViewModels.Wizard;

namespace StayOnTarget.Views.Wizard;

public partial class WizardWindow : Window
{
    public WizardWindow(WizardViewModel viewModel)
    {
        try
        {
            InitializeComponent();
            DataContext = viewModel;

            viewModel.CompletionCallback = (success) =>
            {
                try
                {
                    if (success)
                    {
                        DialogResult = true;
                        Close();
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error during WizardWindow completion callback handling.");
                    
                }
            };
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Critical error initializing WizardWindow.");
            MessageBox.Show($"Failed to launch setup wizard: {ex.Message}", "Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}