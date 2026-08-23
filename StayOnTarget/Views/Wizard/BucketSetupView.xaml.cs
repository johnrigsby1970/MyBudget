using System.Windows.Controls;
using Serilog;

namespace StayOnTarget.Views.Wizard;

public partial class BucketSetupView : UserControl
{
    public BucketSetupView()
    {
        try
        {
            InitializeComponent();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Critical error initializing BucketSetupView.");
            
        }
    }
}