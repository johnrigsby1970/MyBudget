using System.Collections.ObjectModel;
using StayOnTarget.Models;
using StayOnTarget.Services;
using System.IO;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using CsvHelper;

namespace StayOnTarget.ViewModels;

public class ImportReconciliationViewModel : ViewModelBase {
    private ViewModelBase? _activeOverlay;

    public ViewModelBase? ActiveOverlay {
        get => _activeOverlay;
        set {
            _activeOverlay = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<ImportedTransactionViewModel> ImportedTransactions { get; set; } = new();
    public ObservableCollection<ManualTransactionViewModel> UnreconciledManualTransactions { get; set; } = new();

    private ImportedTransactionViewModel? _selectedImported;

    public ImportedTransactionViewModel? SelectedImported {
        get => _selectedImported;
        set {
            if (SetProperty(ref _selectedImported, value)) {
                FilterManualSuggestions();
                LinkTransactionsCommand.NotifyCanExecuteChanged();
                ImportAsNewCommand.NotifyCanExecuteChanged();
                ClearMatchCommand.NotifyCanExecuteChanged();
            }
        }
    }

    private ManualTransactionViewModel? _selectedManual;

    public ManualTransactionViewModel? SelectedManual {
        get => _selectedManual;
        set {
            if (SetProperty(ref _selectedManual, value)) {
                LinkTransactionsCommand.NotifyCanExecuteChanged();
            }
        }
    }

    private bool _isBusy;

    public bool IsBusy {
        get => _isBusy;
        set {
            if (SetProperty(ref _isBusy, value)) { }
        }
    }

    private bool? _lastImportAsQfx;

    public bool? LastImportAsQfx {
        get => _lastImportAsQfx;
        set => SetProperty(ref _lastImportAsQfx, value);
    }

    private string? _lastFileName;

    public string? LastFileName {
        get => _lastFileName;
        set => SetProperty(ref _lastFileName, value);
    }

    public IRelayCommand LinkTransactionsCommand { get; }
    public IRelayCommand ImportAsNewCommand { get; }

    public IRelayCommand ClearMatchCommand { get; }

    private readonly BudgetService _budgetService;
    private Account _account;

    public IRelayCommand SaveCommand { get; }
    public IAsyncRelayCommand ImportFileCommand { get; }

    private CsvImportMappingViewModel? _csvMapping;

    public CsvImportMappingViewModel? CsvMapping {
        get => _csvMapping;
        set {
            if (_csvMapping != null)
                _csvMapping.PropertyChanged -= OnCsvMappingPropertyChanged;
            if (SetProperty(ref _csvMapping, value)) {
                if (_csvMapping != null)
                    _csvMapping.PropertyChanged += OnCsvMappingPropertyChanged;
                ConfirmCsvImportCommand.NotifyCanExecuteChanged();
            }
        }
    }

    private void OnCsvMappingPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) {
        if (e.PropertyName == nameof(CsvImportMappingViewModel.CanImport))
            ConfirmCsvImportCommand.NotifyCanExecuteChanged();
    }

    private bool _isMappingVisible;

    public bool IsMappingVisible {
        get => _isMappingVisible;
        set => SetProperty(ref _isMappingVisible, value);
    }

    private bool _isNewTransactionFormVisible;

    public bool IsNewTransactionFormVisible {
        get => _isNewTransactionFormVisible;
        set => SetProperty(ref _isNewTransactionFormVisible, value);
    }


    public IAsyncRelayCommand ConfirmCsvImportCommand { get; }
    public IRelayCommand CancelCsvImportCommand { get; }

    public ObservableCollection<Bill> BillsWithNone { get; } = new();
    public ObservableCollection<BudgetBucket> BucketsWithNone { get; } = new();
    public IRelayCommand ToggleSelectionCommand { get; }

    public ImportReconciliationViewModel(Account account, BudgetService budgetService) {
        _account = account;
        _budgetService = budgetService;

        ImportFileCommand = new AsyncRelayCommand(PromptForFileAsync);

        ConfirmCsvImportCommand = new AsyncRelayCommand(ConfirmCsvImportAsync, () => CsvMapping?.CanImport == true);
        CancelCsvImportCommand = new RelayCommand(() => { IsMappingVisible = false; });

        SaveCommand = new RelayCommand(SaveAsync);

        ToggleSelectionCommand = new RelayCommand(() => {
            bool allSelected = ImportedTransactions.All(x => x.IsSelected);
            bool allUnselected = ImportedTransactions.All(x => !x.IsSelected);

            if (allSelected) {
                foreach (var t in ImportedTransactions) t.IsSelected = false;
            }
            else if (allUnselected) {
                foreach (var t in ImportedTransactions) t.IsSelected = true;
            }
            else {
                foreach (var t in ImportedTransactions) {
                    if (!t.IsSelected) t.IsSelected = true;
                }
            }
        });

        // Use a lambda to capture the parameter (param) and call your method
        LinkTransactionsCommand = new RelayCommand(
            LinkTransactions,
            () => SelectedImported != null && !SelectedImported.IsReconciled && SelectedManual != null &&
                  !SelectedManual.IsMatched
        );

        ImportAsNewCommand = new RelayCommand(
            ImportAsNew,
            () => SelectedImported != null && !SelectedImported.IsReconciled
        );

        ClearMatchCommand = new RelayCommand(
            ClearMatch,
            () => SelectedImported != null && SelectedImported.IsReconciled
        );

        InitializeDataCommand = new AsyncRelayCommand(LoadDataAsync);
    }

    public IAsyncRelayCommand InitializeDataCommand { get; }

    private async void SaveAsync() {
        if (string.IsNullOrEmpty(LastFileName)) {
            return;
        }

        try {
            var fixDate = false;

            // var differencesInDates = ImportedTransactions.Where(x =>
            //     x.IsSelected &&
            //     x.IsReconciled &&
            //     x.MatchedManualTransactionDate != null && x.Date.HasValue &&
            //     DateOnly.FromDateTime(x.MatchedManualTransactionDate.Value.Date) !=
            //     DateOnly.FromDateTime(x.Date.Value));
            //
            // if (differencesInDates.Any()) {
            //     MessageBoxResult messageBoxResult = MessageBox.Show(
            //         $"Some dates are different between existing transactions and those found at your bank. Do you want to set the transaction dates to match your bank?",
            //         "Date Change Confirmation", MessageBoxButton.YesNo);
            //
            //     if (messageBoxResult == MessageBoxResult.Yes) {
            //         fixDate = true;
            //     }
            // }

            IsBusy = true;

            // Yield back to UI thread to allow WPF to render the LoadingOverlay control
            await Task.Delay(50);

            // Handled matched transactions
            foreach (var match in ImportedTransactions.Where(x =>
                         x.IsSelected &&
                         x.IsReconciled && !string.IsNullOrEmpty(x.MatchedManualTransactionId) &&
                         x.MatchedManualTransactionDate != null && !string.IsNullOrEmpty(x.MatchedManualFitId))) {
                if (string.IsNullOrEmpty(match.BankId)) continue;
                if (string.IsNullOrEmpty(match.Payee)) continue;
                // Track each background database call
                await Task.Delay(10);
                await _budgetService.UpdateTransactionForBankFitIdAsync(
                    _account.Id,
                    match.MatchedManualTransactionId!,
                    match.MatchedManualFitId!,
                    match.BankId,
                    fixDate && match.Date.HasValue ? match.Date.Value : match.MatchedManualTransactionDate!.Value,
                    match.Payee
                );
            }

            // Handle new transactions that are checked but not matched
            foreach (var newItem in ImportedTransactions.Where(x => x.IsSelected && !x.IsReconciled)) {
                if (string.IsNullOrEmpty(newItem.Payee)) continue;
                if (string.IsNullOrEmpty(newItem.BankId)) continue;
                var t = new Transaction {
                    AccountId = newItem.Amount > 0 ? null : _account.Id,
                    ToAccountId = newItem.Amount > 0 ? _account.Id : null,
                    Amount = newItem.Amount,
                    TransactionDate = newItem.Date ?? DateTime.Now,
                    Description = newItem.Payee,
                    FitId = newItem.BankId,
                    BillId = newItem.BillId == 0 ? null : newItem.BillId,
                    BucketId = newItem.BucketId == 0 ? null : newItem.BucketId
                };
                await Task.Delay(10);
                await _budgetService.UpsertTransactionAsync(t);
            }
            
            await Task.Delay(50);
            
            // 4. Now these run sequentially on the UI thread with accurate database states
            await LoadDataAsync();

            if (LastImportAsQfx != null && LastImportAsQfx.Value) {
                await ParseAndPopulateQfxAsync(LastFileName);
            }

            if (LastImportAsQfx != null && !LastImportAsQfx.Value) {
                await ParseAndPopulateCsv(LastFileName);
            }
        }
        finally {
            IsBusy = false;
        }
    }

    private async Task PromptForFileAsync() {
        var openFileDialog = new Microsoft.Win32.OpenFileDialog {
            Filter = "Transaction Files (*.ofx, *.qfx, *.csv)|*.ofx;*.qfx;*.csv|" +
                     "CSV Files (*.csv, *.txt)|*.csv;*.txt|" +
                     "QFX Files (*.qfx)|*.qfx|" +
                     "OFX Files (*.ofx)|*.ofx",
            Title = "Select Transaction File"
        };

        if (openFileDialog.ShowDialog() == true) {
            LastFileName = openFileDialog.FileName;
            if (LastFileName.Contains(".qfx", StringComparison.OrdinalIgnoreCase) ||
                LastFileName.Contains(".ofx", StringComparison.OrdinalIgnoreCase)) {
                await ParseAndPopulateQfxAsync(LastFileName);
            }
            else {
                var mappingPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"mapping_{_account.Id}.json");
                CsvMapping = new CsvImportMappingViewModel(LastFileName, mappingPath);

                IsMappingVisible = true;
            }
        }
    }

    // private void PromptAndLoadQfx() {
    //     var openFileDialog = new Microsoft.Win32.OpenFileDialog {
    //         Filter = "QFX Files (*.qfx)|*.qfx|OFX Files (*.ofx)|*.ofx",
    //         Title = "Select Bank QFX File"
    //     };
    //
    //     if (openFileDialog.ShowDialog() == true) {
    //         LastFileName = openFileDialog.FileName;
    //         ParseAndPopulateQfx(LastFileName);
    //     }
    // }
    //
    // private void PromptAndLoadCsv() {
    //     var openFileDialog = new Microsoft.Win32.OpenFileDialog {
    //         Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
    //         Title = "Select Bank CSV File"
    //     };
    //
    //     if (openFileDialog.ShowDialog() == true) {
    //         LastFileName = openFileDialog.FileName;
    //         var mappingPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"mapping_{_account.Id}.json");
    //         CsvMapping = new CsvImportMappingViewModel(LastFileName, mappingPath);
    //
    //         IsMappingVisible = true;
    //     }
    // }

    private async Task ConfirmCsvImportAsync() {
        if (CsvMapping == null || !CsvMapping.CanImport) return;

        var mappingPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"mapping_{_account.Id}.json");
        CsvMapping.SaveMapping(mappingPath);

        await ParseAndPopulateCsv(CsvMapping.FilePath);
        IsMappingVisible = false;
    }

    private async Task ParseAndPopulateCsv(string filePath) {
        if (!File.Exists(filePath) || CsvMapping == null) return;
        LastImportAsQfx = false;

        ImportedTransactions.Clear();
        var processedBankIds = await _budgetService.GetAlreadyImportedBankIdsAsync(_account.Id);

        using (var reader = new StreamReader(filePath))
        using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture)) {
            await csv.ReadAsync();
            csv.ReadHeader();
            while (await csv.ReadAsync()) {
                string bankId = csv.GetField(CsvMapping.BankIdHeader!) ?? Guid.NewGuid().ToString();

                if (processedBankIds.Contains(bankId)) {
                    var rec = UnreconciledManualTransactions.SingleOrDefault(x => x.FitId == bankId);
                    if (rec != null) {
                        UnreconciledManualTransactions.Remove(rec);
                    }

                    continue;
                }

                string rawDate = csv.GetField(CsvMapping.DateHeader!) ?? "";
                string rawAmount = csv.GetField(CsvMapping.AmountHeader!) ?? "";
                string payee = csv.GetField(CsvMapping.PayeeHeader!) ?? "";

                DateTime date = DateTime.Today;
                DateTime.TryParse(rawDate, CultureInfo.CurrentCulture, DateTimeStyles.None, out date);
                if (date == DateTime.MinValue) {
                    continue;
                }

                decimal.TryParse(rawAmount, NumberStyles.Any, CultureInfo.InvariantCulture, out var amount);

                ImportedTransactions.Add(new ImportedTransactionViewModel {
                    BankId = bankId,
                    Date = date,
                    Amount = amount,
                    Payee = payee.Trim(),
                    Status = "Unmatched"
                });
            }
        }

        AutoMatchTransactions();
    }

    private async Task ParseAndPopulateQfxAsync(string filePath) {
        if (!File.Exists(filePath)) return;

        LastImportAsQfx = true;
        ImportedTransactions.Clear();

        string content = await File.ReadAllTextAsync(filePath);

        // FIX 1: Pre-process the SGML into clean chunks. 
        // This normalizes both closed </STMTTRN> and unclosed SGML transaction blocks.
        var txBlocks = new List<string>();
        var matches = Regex.Matches(content, @"<STMTTRN>(.*?)(?=</STMTTRN>|<STMTTRN>|</STMTRS>)",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);
        foreach (Match m in matches) {
            txBlocks.Add(m.Groups[1].Value);
        }

        // Fetch existing bank IDs from your DB to skip duplicates
        var processedBankIds = await _budgetService.GetAlreadyImportedBankIdsAsync(_account.Id);

        // Maintain a list of manual transactions to remove safely after the loop
        var transactionsToRemove = new List<ManualTransactionViewModel>();

        foreach (string txBlock in txBlocks) {
            string bankId = GetQfxTagValue(txBlock, "FITID");
            if (string.IsNullOrWhiteSpace(bankId)) continue;

            // Skip if this exact transaction was already committed to the DB
            if (processedBankIds.Contains(bankId)) {
                var rec = UnreconciledManualTransactions.SingleOrDefault(x => x.FitId == bankId);
                if (rec != null) {
                    transactionsToRemove.Add(rec);
                }

                continue;
            }

            string rawDate = GetQfxTagValue(txBlock, "DTPOSTED");
            string rawAmount = GetQfxTagValue(txBlock, "TRNAMT");
            string payee = GetQfxTagValue(txBlock, "NAME");

            // Parse Date safely
            DateTime date = DateTime.Today;
            if (rawDate.Length >= 8 && DateTime.TryParseExact(rawDate.Substring(0, 8), "yyyyMMdd",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedDate)) {
                date = parsedDate;
            }

            // Parse Amount safely
            decimal.TryParse(rawAmount, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal amount);

            ImportedTransactions.Add(new ImportedTransactionViewModel {
                BankId = bankId,
                Date = date,
                Amount = amount,
                Payee = payee, // Already sanitized by updated GetQfxTagValue
                Status = "Unmatched"
            });
        }

        // FIX 3: Safely mutate the UI tracking collection outside of the processing loop
        foreach (var rec in transactionsToRemove) {
            UnreconciledManualTransactions.Remove(rec);
        }

        // Auto-match pass
        AutoMatchTransactions();
    }

    private string GetQfxTagValue(string block, string tag) {
        // FIX 2: Updated regex to capture content up to a closing tag, a new open tag, OR a newline/carriage return.
        // This safely handles both standard XML <TAG>value</TAG> and SGML <TAG>value
        var match = Regex.Match(block, $@"<{tag}>([^<\r\n]+)", RegexOptions.IgnoreCase);
        if (!match.Success) return string.Empty;

        string value = match.Groups[1].Value;

        // Clean up any remaining SGML fragments or artifacts safely
        if (value.Contains("</")) {
            value = value.Split(new[] { "</" }, StringSplitOptions.None)[0];
        }

        return value.Trim();
    }

    // private async Task ParseAndPopulateQfxAsync(string filePath) {
    //     if (!File.Exists(filePath)) return;
    //     LastImportAsQfx = true;
    //     ImportedTransactions.Clear();
    //     string content = await File.ReadAllTextAsync(filePath);
    //
    //     // Get all transaction blocks
    //     var txMatches = Regex.Matches(content, @"<STMTTRN>(.*?)</STMTTRN>", RegexOptions.Singleline);
    //
    //     // Fetch existing bank IDs from your DB to skip duplicates
    //     // (Assuming your BudgetService/Database has a way to check already processed bank IDs)
    //     var processedBankIds = await _budgetService.GetAlreadyImportedBankIdsAsync(_account.Id);
    //
    //     foreach (Match txMatch in txMatches) {
    //         string txBlock = txMatch.Groups[1].Value;
    //
    //         string bankId = GetQfxTagValue(txBlock, "FITID");
    //
    //         // Skip if this exact transaction was already committed to the DB in a prior import
    //
    //         if (processedBankIds.Contains(bankId)) {
    //             //its already m,apped to a bank FitId, rtemove it so the list doesnt allow two records to be made to match
    //             var rec = UnreconciledManualTransactions.SingleOrDefault(x => x.FitId == bankId);
    //             if (rec != null) {
    //                 UnreconciledManualTransactions.Remove(rec);
    //             }
    //
    //             continue;
    //         }
    //
    //         string rawDate = GetQfxTagValue(txBlock, "DTPOSTED"); // Format typically: YYYYMMDDHHMMSS
    //         string rawAmount = GetQfxTagValue(txBlock, "TRNAMT");
    //         string payee = GetQfxTagValue(txBlock, "NAME");
    //
    //         // Parse Date safely
    //         DateTime date = DateTime.Today;
    //         if (rawDate.Length >= 8 && DateTime.TryParseExact(rawDate.Substring(0, 8), "yyyyMMdd",
    //                 CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedDate)) {
    //             date = parsedDate;
    //         }
    //
    //         // Parse Amount safely
    //         decimal.TryParse(rawAmount, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal amount);
    //
    //         ImportedTransactions.Add(new ImportedTransactionViewModel {
    //             BankId = bankId,
    //             Date = date,
    //             Amount = amount,
    //             Payee = payee?.Trim()??"",
    //             Status = "Unmatched"
    //         });
    //     }
    //
    //     // Auto-match pass
    //     AutoMatchTransactions();
    // }
    //
    // // Helper to extract values from unclosed SGML tags common in QFX/OFX files
    // private string GetQfxTagValue(string block, string tag) {
    //     var match = Regex.Match(block, $@"<{tag}>([^<\r\n]+)");
    //     return match.Success ? match.Value.Replace($"<{tag}>", "").Trim() : string.Empty;
    // }

    // private void AutoMatchTransactions() {
    //     //In different levels of accuracy, try to find a transaction that is already in the system that matches. The more accurate match wins
    //     foreach (var imported in ImportedTransactions.Where(x => x.Date != null)) {
    //         if (imported.Date == null || imported.Date == DateTime.MinValue) {
    //             //it is a pending transaction at the bank (BoA as an example)
    //             continue;
    //         }
    //
    //         //same amount, same date, very close name
    //         var exactMatch = UnreconciledManualTransactions.FirstOrDefault(m =>
    //             Math.Abs(m.Amount) == Math.Abs(imported.Amount) &&
    //             Math.Abs((m.TransactionDate - imported.Date.Value).TotalDays) == 0 && TransactionMatcher.IsMatch(imported.Payee??"", m.Description));
    //         if (exactMatch != null) {
    //             imported.IsReconciled = true;
    //             imported.Status = $"Auto-Matched ({exactMatch.Description})";
    //             imported.MatchedManualFitId = exactMatch.FitId;
    //             imported.MatchedManualTransactionDate = exactMatch.TransactionDate;
    //             imported.MatchedManualTransactionId = exactMatch.TransactionId;
    //
    //             exactMatch.IsMatched = true;
    //             // Set selection defaults to help the user review
    //             SelectedImported = imported;
    //             SelectedManual = exactMatch;
    //             continue;
    //         }
    //         
    //         //same amount, close date, very close name
    //         var closerMatch = UnreconciledManualTransactions.FirstOrDefault(m =>
    //             Math.Abs(m.Amount) == Math.Abs(imported.Amount) &&
    //             Math.Abs((m.TransactionDate - imported.Date.Value).TotalDays) <= 4 && TransactionMatcher.IsMatch(imported.Payee??"", m.Description));
    //         if (closerMatch != null) {
    //             imported.IsReconciled = true;
    //             imported.Status = $"Auto-Matched ({closerMatch.Description})";
    //             imported.MatchedManualFitId = closerMatch.FitId;
    //             imported.MatchedManualTransactionDate = closerMatch.TransactionDate;
    //             imported.MatchedManualTransactionId = closerMatch.TransactionId;
    //
    //             closerMatch.IsMatched = true;
    //             // Set selection defaults to help the user review
    //             SelectedImported = imported;
    //             SelectedManual = closerMatch;
    //             continue;
    //         }
    //         
    //         //same amount, same date, name can be different
    //         var closeMatch = UnreconciledManualTransactions.FirstOrDefault(m =>
    //             Math.Abs(m.Amount) == Math.Abs(imported.Amount) &&
    //                                            Math.Abs((m.TransactionDate - imported.Date.Value).TotalDays) == 0);
    //         if (closeMatch != null) {
    //             imported.IsReconciled = true;
    //             imported.Status = $"Auto-Matched ({closeMatch.Description})";
    //             imported.MatchedManualFitId = closeMatch.FitId;
    //             imported.MatchedManualTransactionDate = closeMatch.TransactionDate;
    //             imported.MatchedManualTransactionId = closeMatch.TransactionId;
    //             imported.BillId = closeMatch.BillId;
    //             imported.BucketId = closeMatch.BucketId;
    //             imported.IsSelected = true;
    //
    //             closeMatch.IsMatched = true;
    //             // Set selection defaults to help the user review
    //             SelectedImported = imported;
    //             SelectedManual = closeMatch;
    //             continue;
    //         }
    //         
    //         // Look for a manual entry with the exact amount and a date within a 4-day window
    //         if (UnreconciledManualTransactions.Count(m =>
    //                 Math.Abs(m.Amount) ==  Math.Abs(imported.Amount) &&
    //                                                 Math.Abs((m.TransactionDate - imported.Date.Value).TotalDays) <= 4) > 1) {
    //             continue;
    //         }
    //
    //         //same amount, close date, name can be different
    //         var match = UnreconciledManualTransactions.FirstOrDefault(m =>
    //             m.Amount == imported.Amount &&
    //             Math.Abs((m.TransactionDate - imported.Date.Value).TotalDays) <= 4);
    //
    //         if (match != null) {
    //             imported.IsReconciled = true;
    //             imported.Status = $"Auto-Matched ({match.Description})";
    //             imported.MatchedManualFitId = match.FitId;
    //             imported.MatchedManualTransactionDate = match.TransactionDate;
    //             imported.MatchedManualTransactionId = match.TransactionId;
    //             imported.BillId = match.BillId;
    //             imported.BucketId = match.BucketId;
    //             imported.IsSelected = true;
    //
    //             match.IsMatched = true;
    //             // Set selection defaults to help the user review
    //             SelectedImported = imported;
    //             SelectedManual = match;
    //         }
    //     }
    // }

    private void AutoMatchTransactions() {
        // FIX 1: Track matches using string HashSet to align with your string? TransactionId type
        var matchedManualIds = new HashSet<string>();

        foreach (var imported in ImportedTransactions.Where(x => x.Date != null)) {
            if (imported.Date == null || imported.Date == DateTime.MinValue) {
                continue; // Ignore pending items
            }

            // TIER 1: Exact amount, exact date, close name match
            var exactMatch = UnreconciledManualTransactions.FirstOrDefault(m =>
                !string.IsNullOrEmpty(m.TransactionId) &&
                !matchedManualIds.Contains(m.TransactionId) &&
                Math.Abs(m.Amount) == Math.Abs(imported.Amount) &&
                Math.Abs((m.TransactionDate - imported.Date.Value).TotalDays) == 0 &&
                TransactionMatcher.IsMatch(imported.Payee ?? "", m.Description));

            if (exactMatch != null) {
                ApplyMatch(imported, exactMatch, $"Auto-Matched ({exactMatch.Description})");
                matchedManualIds.Add(exactMatch.TransactionId!);
                continue;
            }

            // TIER 2: Exact amount, close date (±4 days), close name match
            var closerMatch = UnreconciledManualTransactions.FirstOrDefault(m =>
                !string.IsNullOrEmpty(m.TransactionId) &&
                !matchedManualIds.Contains(m.TransactionId) &&
                Math.Abs(m.Amount) == Math.Abs(imported.Amount) &&
                Math.Abs((m.TransactionDate - imported.Date.Value).TotalDays) <= 4 &&
                TransactionMatcher.IsMatch(imported.Payee ?? "", m.Description));

            if (closerMatch != null) {
                ApplyMatch(imported, closerMatch, $"Auto-Matched ({closerMatch.Description})");
                matchedManualIds.Add(closerMatch.TransactionId!);
                continue;
            }

            // TIER 3: Exact amount, exact date, names are completely different
            var closeMatch = UnreconciledManualTransactions.FirstOrDefault(m =>
                !string.IsNullOrEmpty(m.TransactionId) &&
                !matchedManualIds.Contains(m.TransactionId) &&
                Math.Abs(m.Amount) == Math.Abs(imported.Amount) &&
                Math.Abs((m.TransactionDate - imported.Date.Value).TotalDays) == 0);

            if (closeMatch != null) {
                ApplyMatch(imported, closeMatch, $"Auto-Matched ({closeMatch.Description})");
                imported.BillId = closeMatch.BillId;
                imported.BucketId = closeMatch.BucketId;
                imported.IsSelected = true;
                matchedManualIds.Add(closeMatch.TransactionId!);
                continue;
            }

            // TIER 4 Guard: Skip if multiple entries sit ambiguously inside the 4-day window
            int ambiguousCount = UnreconciledManualTransactions.Count(m =>
                !string.IsNullOrEmpty(m.TransactionId) &&
                !matchedManualIds.Contains(m.TransactionId) &&
                Math.Abs(m.Amount) == Math.Abs(imported.Amount) &&
                Math.Abs((m.TransactionDate - imported.Date.Value).TotalDays) <= 4);

            if (ambiguousCount > 1) {
                continue;
            }

            // TIER 4: Exact amount, close date (±4 days), names are completely different
            var match = UnreconciledManualTransactions.FirstOrDefault(m =>
                !string.IsNullOrEmpty(m.TransactionId) &&
                !matchedManualIds.Contains(m.TransactionId) &&
                Math.Abs(m.Amount) == Math.Abs(imported.Amount) &&
                Math.Abs((m.TransactionDate - imported.Date.Value).TotalDays) <= 4);

            if (match != null) {
                ApplyMatch(imported, match, $"Auto-Matched ({match.Description})");
                imported.BillId = match.BillId;
                imported.BucketId = match.BucketId;
                imported.IsSelected = true;
                matchedManualIds.Add(match.TransactionId!);
            }
        }

        // Set UI Selection defaults cleanly outside the loop execution window
        var firstUnmatchedImport = ImportedTransactions.FirstOrDefault(x => !x.IsReconciled);
        if (firstUnmatchedImport != null) {
            SelectedImported = firstUnmatchedImport;
            SelectedManual = UnreconciledManualTransactions.FirstOrDefault(m =>
                !m.IsMatched && Math.Abs(m.Amount) == Math.Abs(firstUnmatchedImport.Amount));
        }
    }

// Clean helper parameterized explicitly to match your class definitions
    private void ApplyMatch(ImportedTransactionViewModel imported, ManualTransactionViewModel manual,
        string statusText) {
        imported.IsReconciled = true;
        imported.Status = statusText;
        imported.MatchedManualFitId = manual.FitId;
        imported.MatchedManualTransactionDate = manual.TransactionDate;
        imported.MatchedManualTransactionId = manual.TransactionId;
        manual.IsMatched = true; // Will safely fire notification 
    }

    private void FilterManualSuggestions() {
        // Optional: Filter or highlight the Manual list here based on SelectedImported's Amount/Date
    }

    // Update the methods to handle the object parameter:
    private void LinkTransactions() {
        if (SelectedImported == null || SelectedManual == null) return;

        SelectedImported.IsReconciled = true;
        SelectedImported.Status = $"Matched to Manual ({SelectedManual.Description} {SelectedManual.Amount:C})";
        SelectedImported.MatchedManualFitId = SelectedManual.FitId;
        SelectedImported.MatchedManualTransactionDate = SelectedManual.TransactionDate;
        SelectedImported.MatchedManualTransactionId = SelectedManual.TransactionId;
        SelectedImported.BillId = SelectedManual.BillId;
        SelectedImported.BucketId = SelectedManual.BucketId;
        SelectedImported.IsSelected = true;

        SelectedManual.IsMatched = true;
        OnPropertyChanged(nameof(SelectedManual));
        OnPropertyChanged(nameof(SelectedImported));
        OnPropertyChanged(nameof(UnreconciledManualTransactions));
    }

    private void ClearMatch() {
        if (SelectedImported == null) return;

        var manual =
            UnreconciledManualTransactions.SingleOrDefault(x => x.FitId == SelectedImported.MatchedManualFitId);

        if (manual != null) {
            SelectedImported.IsReconciled = false;
            SelectedImported.Status = $"Match removed";
            SelectedImported.MatchedManualFitId = null;
            SelectedImported.MatchedManualTransactionDate = null;
            SelectedImported.MatchedManualTransactionId = null;
            SelectedImported.BillId = null;
            SelectedImported.BucketId = null;
            SelectedImported.IsSelected = false;

            manual.IsMatched = false;
        }
    }

    #region Import as new

    private void ImportAsNew() {
        if (SelectedImported == null || SelectedImported.Date == null) return;

        // 1. Guard Check BEFORE creating the ViewModel or Window
        if (string.IsNullOrWhiteSpace(SelectedImported?.Payee) ||
            SelectedImported.Date == null ||
            string.IsNullOrWhiteSpace(SelectedImported.BankId)) {
            MessageBox.Show(
                "The transaction lacks required fields: payee, transaction date, and a bank transaction id.",
                "Missing Information", MessageBoxButton.OK, MessageBoxImage.Warning);
            return; // Exit cleanly without opening the dialog window!
        }

        ActiveOverlay = new NewTransactionViewModel(_account, _budgetService, SelectedImported, (childVm, isSaved) => {
            // This code executes when the child calls _closeCallback(...)
            if (isSaved) {
                ImportedTransactions.Remove(SelectedImported);
            }
            else {
                // User canceled, no actions needed on parent data
            }

            // CLOSE THE DIALOG: Setting this to null makes the ContentControl disappear
            ActiveOverlay = null;
        });
    }

    #endregion

    private async Task LoadDataAsync() {
        // Mocking manual records currently in your DB
        var unreconciledTransactions = await _budgetService.GetAllUnreconciledTransactionsAsync();
        unreconciledTransactions = unreconciledTransactions
            .Where(x => x.AccountId == _account.Id || x.ToAccountId == _account.Id).ToList();

        UnreconciledManualTransactions.Clear();
        foreach (var transaction in unreconciledTransactions) {
            UnreconciledManualTransactions.Add(new ManualTransactionViewModel {
                FitId = transaction.FitId, TransactionDate = transaction.TransactionDate,
                Amount = transaction.Amount,
                Description = transaction.Description, TransactionId = transaction.TransactionId.ToString(),
                BillId = transaction.BillId,
                BucketId = transaction.BucketId
            });
        }

        BillsWithNone.Clear();
        BillsWithNone.Add(new Bill { Id = 0, Name = "None" });
        foreach (var bill in await _budgetService.GetAllBillsAsync()) {
            BillsWithNone.Add(bill);
        }

        BucketsWithNone.Clear();
        BucketsWithNone.Add(new BudgetBucket { Id = 0, Name = "None" });
        foreach (var bucket in await _budgetService.GetAllBucketsAsync()) {
            BucketsWithNone.Add(bucket);
        }
    }
}