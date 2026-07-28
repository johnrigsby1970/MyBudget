using System.Windows;
using StayOnTarget.Models;
using StayOnTarget.Services;
using StayOnTarget.ViewModels;

namespace StayOnTarget;

public partial class AccountAprHistoryWindow : Window {
    private readonly AccountAprHistoryViewModel _viewModel;

    public AccountAprHistoryWindow(Account account, BudgetService budgetService) {
        InitializeComponent();
        _viewModel = new AccountAprHistoryViewModel(account, budgetService);
        HeaderLabel.Text = $"Annual Interest Rates for {account.Name}";
        DataContext = _viewModel;
    }
    
    private async void OkButton_Click(object sender, RoutedEventArgs e) {
        await _viewModel.UpdateAccountAprHistoriesAsync();
        DialogResult = true;
    }
    
    private void CancelButton_Click(object sender, RoutedEventArgs e) {
        DialogResult = false;
    }
}