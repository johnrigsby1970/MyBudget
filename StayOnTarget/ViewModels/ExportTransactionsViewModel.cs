using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using StayOnTarget.Models;
using StayOnTarget.Services;

namespace StayOnTarget.ViewModels;

public class ExportTransactionsViewModel : ViewModelBase
{
    private readonly BudgetService _budgetService;
    private bool _includeArchivedAccounts;
    private DateFilterOption _selectedDateFilter = DateFilterOption.AllDates;
    private DateTime _fromDate = DateTime.Today.AddMonths(-1);
    private DateTime _toDate = DateTime.Today;
    private bool _includeBuckets = true;
    private bool _includeStatus = true;
    private bool _includeMemos = true;
    private string _exportFilePath;
    private CsvExportPreset _selectedPreset = CsvExportPreset.Standard;

    public ExportTransactionsViewModel(BudgetService budgetService)
    {
        _budgetService = budgetService;
        Accounts = new ObservableCollection<SelectableAccount>();
        _exportFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), $"Transactions_Export_{DateTime.Now:yyyy-MM-dd}.csv");

        SelectAllCommand = new RelayCommand(SelectAll);
        ClearSelectionCommand = new RelayCommand(ClearSelection);
        BrowseCommand = new RelayCommand(Browse);
        ExportCommand = new AsyncRelayCommand(ExportAsync, () => CanExport());
        CancelCommand = new RelayCommand<Window>(Cancel);

        LoadAccountsAsync();
    }

    public ObservableCollection<SelectableAccount> Accounts { get; }

    public bool IncludeArchivedAccounts
    {
        get => _includeArchivedAccounts;
        set
        {
            if (SetProperty(ref _includeArchivedAccounts, value))
            {
                LoadAccountsAsync();
            }
        }
    }

    public DateFilterOption SelectedDateFilter
    {
        get => _selectedDateFilter;
        set
        {
            if (SetProperty(ref _selectedDateFilter, value))
            {
                OnPropertyChanged(nameof(IsCustomDateRange));
                UpdateDatesFromFilter();
            }
        }
    }

    public bool IsCustomDateRange => SelectedDateFilter == DateFilterOption.Custom;

    public DateTime FromDate
    {
        get => _fromDate;
        set => SetProperty(ref _fromDate, value);
    }

    public DateTime ToDate
    {
        get => _toDate;
        set => SetProperty(ref _toDate, value);
    }

    public bool IncludeBuckets
    {
        get => _includeBuckets;
        set => SetProperty(ref _includeBuckets, value);
    }

    public bool IncludeStatus
    {
        get => _includeStatus;
        set => SetProperty(ref _includeStatus, value);
    }

    public bool IncludeMemos
    {
        get => _includeMemos;
        set => SetProperty(ref _includeMemos, value);
    }

    public string ExportFilePath
    {
        get => _exportFilePath;
        set
        {
            if (SetProperty(ref _exportFilePath, value))
            {
                ExportCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public CsvExportPreset SelectedPreset
    {
        get => _selectedPreset;
        set
        {
            if (SetProperty(ref _selectedPreset, value))
            {
                UpdateDefaultFileName();
                OnPropertyChanged(nameof(IsQuickenPreset));
            }
        }
    }

    public bool IsQuickenPreset => SelectedPreset == CsvExportPreset.Quicken;

    private void UpdateDefaultFileName()
    {
        string directory = Path.GetDirectoryName(ExportFilePath) ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string dateStr = DateTime.Now.ToString("yyyy-MM-dd");
        string fileName = SelectedPreset == CsvExportPreset.Quicken 
            ? $"Quicken_Export_{dateStr}.csv" 
            : $"Transactions_Export_{dateStr}.csv";
        ExportFilePath = Path.Combine(directory, fileName);
    }

    public IRelayCommand SelectAllCommand { get; }
    public IRelayCommand ClearSelectionCommand { get; }
    public IRelayCommand BrowseCommand { get; }
    public IAsyncRelayCommand ExportCommand { get; }
    public IRelayCommand<Window> CancelCommand { get; }

    private async void LoadAccountsAsync()
    {
        var accounts = await _budgetService.GetAllAccountsAsync(IncludeArchivedAccounts);
        Accounts.Clear();
        foreach (var account in accounts)
        {
            Accounts.Add(new SelectableAccount(account) { IsSelected = true });
        }
    }

    private void SelectAll()
    {
        foreach (var account in Accounts)
        {
            account.IsSelected = true;
        }
    }

    private void ClearSelection()
    {
        foreach (var account in Accounts)
        {
            account.IsSelected = false;
        }
    }

    private void UpdateDatesFromFilter()
    {
        var today = DateTime.Today;
        switch (SelectedDateFilter)
        {
            case DateFilterOption.YearToDate:
                FromDate = new DateTime(today.Year, 1, 1);
                ToDate = today;
                break;
            case DateFilterOption.CurrentMonth:
                FromDate = new DateTime(today.Year, today.Month, 1);
                ToDate = today;
                break;
            case DateFilterOption.AllDates:
                // No need to set specific dates, the export logic will handle AllDates
                break;
        }
    }

    private void Browse()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            InitialDirectory = Path.GetDirectoryName(ExportFilePath),
            FileName = Path.GetFileName(ExportFilePath)
        };

        if (dialog.ShowDialog() == true)
        {
            ExportFilePath = dialog.FileName;
        }
    }

    private bool CanExport()
    {
        return !string.IsNullOrWhiteSpace(ExportFilePath);
    }

    private async Task ExportAsync()
    {
        var selectedAccountIds = Accounts.Where(a => a.IsSelected).Select(a => a.Account.Id).ToList();
        if (!selectedAccountIds.Any())
        {
            MessageBox.Show("Please select at least one account.", "Export Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var allTransactions = await _budgetService.GetRawTransactionsAsync();
            var filteredTransactions = allTransactions
                .Where(t => t.AccountId.HasValue && selectedAccountIds.Contains(t.AccountId.Value));

            if (SelectedDateFilter != DateFilterOption.AllDates)
            {
                filteredTransactions = filteredTransactions
                    .Where(t => t.TransactionDate.Date >= FromDate.Date && t.TransactionDate.Date <= ToDate.Date);
            }

            var sortedTransactions = filteredTransactions.OrderBy(t => t.TransactionDate).ToList();

            using (var writer = new StreamWriter(ExportFilePath))
            {
                if (SelectedPreset == CsvExportPreset.Quicken)
                {
                    // Quicken header: Date,Payee,FI Payee,Amount,Category,Account,Tag,Memo,Chknum,Debit/Credit
                    var headers = new List<string> { "Date", "Payee", "FI Payee", "Amount", "Category", "Account", "Tag", "Memo", "Chknum", "Debit/Credit" };
                    await writer.WriteLineAsync(string.Join(",", headers.Select(EscapeCsvField)));

                    foreach (var t in sortedTransactions)
                    {
                        var fields = new List<string>
                        {
                            t.TransactionDate.ToString("MM/dd/yyyy"), // Quicken specific date format
                            t.Description,
                            string.Empty, // FI Payee
                            t.Amount.ToString("0.00"),
                            t.BucketName ?? string.Empty, // Category
                            t.AccountName ?? string.Empty, // Account
                            string.Empty, // Tag
                            t.Memo ?? string.Empty, // Memo
                            string.Empty, // Chknum
                            string.Empty  // Debit/Credit
                        };
                        await writer.WriteLineAsync(string.Join(",", fields.Select(EscapeCsvField)));
                    }
                }
                else
                {
                    // Standard header
                    var headers = new List<string> { "Date", "Account", "Payee", "Amount" };
                    if (IncludeBuckets) headers.Add("Bucket");
                    if (IncludeStatus) headers.Add("Status");
                    if (IncludeMemos) headers.Add("Memo");
                    headers.Add("FITID");

                    await writer.WriteLineAsync(string.Join(",", headers.Select(EscapeCsvField)));

                    foreach (var t in sortedTransactions)
                    {
                        var fields = new List<string>
                        {
                            t.TransactionDate.ToString("yyyy-MM-dd"),
                            t.AccountName ?? string.Empty,
                            t.Description,
                            t.Amount.ToString("0.00")
                        };

                        if (IncludeBuckets) fields.Add(t.BucketName ?? string.Empty);
                        if (IncludeStatus)
                            fields.Add(t.AccountId.HasValue
                                ? t.FromAccountReconciledId.HasValue ? "Reconciled" : "Cleared"
                                : t.ToAccountReconciledId.HasValue ? "Reconciled" : "Cleared");
                        if (IncludeMemos) fields.Add(t.Memo ?? string.Empty);
                        fields.Add(t.FitId ?? string.Empty);

                        await writer.WriteLineAsync(string.Join(",", fields.Select(EscapeCsvField)));
                    }
                }
            }

            MessageBox.Show($"Successfully exported to {ExportFilePath}", "Export Successful", MessageBoxButton.OK, MessageBoxImage.Information);
            
            // Close the dialog - this is usually handled by the view, but we can use a property or event
            RequestClose?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error exporting transactions: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Cancel(Window? window)
    {
        window?.Close();
    }

    private string EscapeCsvField(string field)
    {
        if (string.IsNullOrEmpty(field)) return string.Empty;
        if (field.Contains(",") || field.Contains("\"") || field.Contains("\n") || field.Contains("\r"))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }
        return field;
    }

    public event EventHandler? RequestClose;
}

public class SelectableAccount : ViewModelBase
{
    private bool _isSelected;
    public SelectableAccount(Account account)
    {
        Account = account;
    }
    public Account Account { get; }
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}

public enum DateFilterOption
{
    [Display(Name = "All Dates")]
    AllDates,
    [Display(Name = "Year To Date")]
    YearToDate, 
    [Display(Name = "Current Month")]
    CurrentMonth,
    Custom
}

public enum CsvExportPreset
{
    Standard,
    Quicken
}
