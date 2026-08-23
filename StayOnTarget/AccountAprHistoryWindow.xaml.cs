using System.Windows;
using Serilog;
using StayOnTarget.Models;
using StayOnTarget.Services;
using StayOnTarget.ViewModels;

namespace StayOnTarget;

public partial class AccountAprHistoryWindow : Window {
    private readonly AccountAprHistoryViewModel _viewModel;

    public AccountAprHistoryWindow(Account account, BudgetService budgetService) {
        try {
            InitializeComponent();
            _viewModel = new AccountAprHistoryViewModel(account, budgetService);
            HeaderLabel.Text = $"Annual Interest Rates for {account.Name}";
            DataContext = _viewModel;
        }
        catch (Exception ex) {
            Log.Fatal(ex, "Critical error initializing AccountAprHistoryWindow for account {AccountName}.", account?.Name);
            
            MessageBox.Show($"Failed to load APR history window: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    private async void OkButton_Click(object sender, RoutedEventArgs e) {
        try {
            await _viewModel.UpdateAccountAprHistoriesAsync();
            DialogResult = true;
        }
        catch (Exception ex) {
            Log.Error(ex, "Error during OkButton_Click in AccountAprHistoryWindow.");
            
            MessageBox.Show($"Failed to save APR history changes: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    private void CancelButton_Click(object sender, RoutedEventArgs e) {
        try {
            DialogResult = false;
        }
        catch (Exception ex) {
            Log.Error(ex, "Error during CancelButton_Click in AccountAprHistoryWindow.");
            
        }
    }
}