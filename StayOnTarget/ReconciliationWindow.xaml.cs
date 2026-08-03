using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Serilog;
using StayOnTarget.Models;
using StayOnTarget.Services;
using StayOnTarget.ViewModels;
using StayOnTarget.Views;

namespace StayOnTarget;

public partial class ReconciliationWindow : Window {
    private readonly ReconciliationViewModel _viewModel;

    private bool IsBusy { get; set; }
    public ReconciliationWindow(Account account, BudgetService budgetService) {
        InitializeComponent();
        _viewModel = new ReconciliationViewModel(account, budgetService);
        HeaderLabel.Text = $"Reconciliation for {account.Name}";
        DataContext = _viewModel;
    }

    private async void OkButton_Click(object sender, RoutedEventArgs e) {
        try {
            //MessageBoxResult messageBoxResult = MessageBox.Show(
            //    $"I certify that there are no pending transactions on this account prior to {_viewModel.NewReconciledDate:MM/dd/yyyy} and that the balance is {_viewModel.NewReconciledBalance}?",
            //    "Delete Confirmation", MessageBoxButton.YesNo);

           // if (messageBoxResult == MessageBoxResult.Yes) {
                _viewModel.SpinnerMessage = "Reconciling records...";
                _viewModel.IsBusy = true; // Shows the overlay & starts spinner
                // Yield back to UI thread to allow WPF to render the LoadingOverlay control
                await Task.Delay(50);
                await Task.Run(async () => {
                    await _viewModel.UpdateReconciliationTransactionsAsync();
                });
                
                _viewModel.IsBusy = false; // Hides spinner
                DialogResult = true;
           // }
            //else {
           //     MessageBox.Show("Reconciliation cancelled.");
           // }
        }
        catch (Exception ex) {
            Log.Error(ex, "Error during OkButton_Click.");
        }
        finally {
            _viewModel.IsBusy = false; // Hides spinner
        }
    }

    public async void HandleImportAccount_Click(object sender, RoutedEventArgs e) {
        try {
            await _viewModel.ImportAccount();
        }
        catch (Exception ex) {
            Log.Error(ex, "Error during HandleImportAccount_Click.");
        }
    }

    public void HandleCheck(object sender, RoutedEventArgs e) {
        try {
            _viewModel.Reconcile();
            _viewModel.UpdateTransactionEnabledState();
        }
        catch (Exception ex) {
            Log.Error(ex, "Error during HandleCheck.");
        }
    }

    public void HandleUnchecked(object sender, RoutedEventArgs e) {
        try {
            _viewModel.Reconcile();
            _viewModel.UpdateTransactionEnabledState();
        }
        catch (Exception ex) {
            Log.Error(ex, "Error during HandleUnchecked.");
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) {
        try {
            DialogResult = false;
        }
        catch (Exception ex) {
            Log.Error(ex, "Error during CancelButton_Click.");
        }
    }

    private void ReconciliationDataGrid_PreviewKeyDown(object sender, KeyEventArgs e) {
        try {
            if (e.Key == Key.Space) {
                var grid = sender as DataGrid;
                if (grid?.SelectedItem != null) {
                    // Access your specific transaction class
                    var transaction = grid.SelectedItem as ReconciliationTransaction;
                    if (transaction != null) {
                        if (transaction.IsEnabled) {
                            // Toggle the property
                            transaction.IsReconciled = !transaction.IsReconciled;
                            _viewModel.Reconcile();
                            _viewModel.UpdateTransactionEnabledState();
                        }

                        // Mark event as handled so the grid doesn't scroll
                        e.Handled = true;
                    }
                }
            }
        }
        catch (Exception ex) {
            Log.Error(ex, "Error during ReconciliationDataGrid_PreviewKeyDown.");
        }
    }
}