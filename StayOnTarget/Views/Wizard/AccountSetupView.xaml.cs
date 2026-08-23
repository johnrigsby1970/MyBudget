using System.Windows.Controls;
using Serilog;

namespace StayOnTarget.Views.Wizard;

public partial class AccountSetupView : UserControl
{
    public AccountSetupView()
    {
        try
        {
            InitializeComponent();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Critical error initializing AccountSetupView.");
            
        }
    }
}