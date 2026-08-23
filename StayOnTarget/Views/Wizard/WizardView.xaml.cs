using System.Windows.Controls;
using Serilog;
using StayOnTarget.ViewModels.Wizard;

namespace StayOnTarget.Views.Wizard;

public partial class WizardView : UserControl {
    public WizardView()
    {
        try
        {
            InitializeComponent();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Critical error initializing parameterless WizardView.");
            
        }
    }

    public WizardView(WizardViewModel viewModel)
    {
        try
        {
            InitializeComponent();
            DataContext = viewModel; // Sets the DataContext directly
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Critical error initializing WizardView with ViewModel.");
            
        }
    }
}