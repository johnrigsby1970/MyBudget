using System.Windows.Controls;
using Serilog;

namespace StayOnTarget.Views.Wizard;

public partial class DatabaseNameView : UserControl
{
    public DatabaseNameView()
    {
        try
        {
            InitializeComponent();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Critical error initializing DatabaseNameView.");
            
        }
    }
}