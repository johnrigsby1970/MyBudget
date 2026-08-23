using System.Windows;
using Serilog;

namespace StayOnTarget;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        try
        {
            InitializeComponent();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Critical error initializing AboutWindow.");
            
            MessageBox.Show($"Failed to open About window: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Close();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error during Close_Click in AboutWindow.");
            
        }
    }
}