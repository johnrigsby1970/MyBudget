using System.Windows.Controls;
using Serilog;

namespace StayOnTarget.Views.Wizard;

public partial class PaycheckSetupView : UserControl
{
    public PaycheckSetupView()
    {
        try
        {
            InitializeComponent();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Critical error initializing PaycheckSetupView.");
            
        }
    }
}