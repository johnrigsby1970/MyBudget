using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Serilog;
using StayOnTarget.Models;
using StayOnTarget.Services;
using StayOnTarget.ViewModels;

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
            _viewModel.SpinnerMessage = "Reconciling records...";
            _viewModel.IsBusy = true; // Shows the overlay & starts spinner
        
            // Yield briefly to let WPF render the spinner animation
            await Task.Delay(50);

            // Run the heavy database processing on a background thread pool thread
            await Task.Run(async () => {
                await _viewModel.UpdateReconciliationTransactionsAsync();
            });
        
            // If UpdateReconciliationTransactionsAsync modifies observable collections 
            // bound to the UI, make sure those collection changes are dispatched back:
            // e.g., await Dispatcher.InvokeAsync(() => _viewModel.RefreshCollections());

            DialogResult = true;
        }
        catch (Exception ex) {
            Log.Error(ex, "Error during OkButton_Click.");
            
        }
        finally {
            _viewModel.IsBusy = false; // Hides spinner safely
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
        }
        catch (Exception ex) {
            Log.Error(ex, "Error during HandleCheck.");
        }
    }

    public void HandleUnchecked(object sender, RoutedEventArgs e) {
        try {
            _viewModel.Reconcile();
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

    private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e) {
        // If the click did not hit an input control (like another TextBox), move focus to the window
        if (Keyboard.FocusedElement is TextBox textBox)
        {
            // Check if the click occurred outside the active TextBox
            if (e.OriginalSource is FrameworkElement clickedElement && clickedElement != textBox)
            {
                // Explicitly force the Text binding to push its value to the ViewModel
                BindingExpression binding = textBox.GetBindingExpression(TextBox.TextProperty);
                binding?.UpdateSource();

                // Clear focus from the text box
                Keyboard.ClearFocus();
            }
        }
    }

    private void ReconciliationDataGrid_PreviewKeyDown(object sender, KeyEventArgs e) {
        try {
            if (e.Key == Key.Space) {
                var grid = sender as DataGrid;
                if (grid?.SelectedItem != null) {
                    // Access your specific transaction class
                    var transaction = grid.SelectedItem as TransactionViewModel;
                    if (transaction != null) {
                        if (transaction.IsEnabled) {
                            // Toggle the property
                            transaction.IsCleared = !transaction.IsCleared;
                            _viewModel.Reconcile();
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