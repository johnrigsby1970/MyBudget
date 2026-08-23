using System.Windows;
using Serilog;
using StayOnTarget.ViewModels;

namespace StayOnTarget
{
    public partial class ExportTransactionsDialog : Window
    {
        public ExportTransactionsDialog(ExportTransactionsViewModel viewModel)
        {
            try
            {
                InitializeComponent();
                DataContext = viewModel;
                viewModel.RequestClose += (s, e) =>
                {
                    try
                    {
                        DialogResult = true;
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error setting DialogResult during RequestClose in ExportTransactionsDialog.");
                        
                    }
                };
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Critical error initializing ExportTransactionsDialog.");
                
                MessageBox.Show($"Failed to open export dialog: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}