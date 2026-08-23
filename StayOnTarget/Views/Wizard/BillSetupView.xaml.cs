using System.Windows.Controls;
using Serilog;

namespace StayOnTarget.Views.Wizard;

public partial class BillSetupView : UserControl
{
    public BillSetupView()
    {
        try
        {
            InitializeComponent();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Critical error initializing BillSetupView.");
            
        }
    }
}