using System.Collections.ObjectModel;
using System.ComponentModel;
using StayOnTarget.Models;
using StayOnTarget.Services;
using System.IO;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using CsvHelper;
using StayOnTarget.Helpers;

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

    public RangeObservableCollection<ImportedTransactionViewModel> ImportedTransactions { get; set; } = new();
    public RangeObservableCollection<ManualTransactionViewModel> UnreconciledManualTransactions { get; set; } = new();

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

    public RangeObservableCollection<Bill> BillsWithNone { get; } = new();
    public RangeObservableCollection<BudgetBucket> BucketsWithNone { get; } = new();

    public RangeObservableCollection<SubCategory> SubCategoriesWithNone { get; } = new();

    //public IRelayCommand ToggleSelectionCommand { get; }

    private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e) {
        if (e.PropertyName == nameof(ImportedTransactionViewModel.IsSelected)) {
            UpdateIsAllSelectedState();
        }
    }

    private bool? _isAllSelected;

    public bool? IsAllSelected {
        get => _isAllSelected;
        set {
            // Resolves null clicks (from indeterminate state) to false so it unchecks all
            bool targetState = value ?? false;

            if (_isAllSelected != targetState) {
                _isAllSelected = targetState;
                OnPropertyChanged(nameof(IsAllSelected));

                // Bulk toggle all rows
                foreach (var item in ImportedTransactions) {
                    item.PropertyChanged -= Item_PropertyChanged;
                    item.IsSelected = targetState;
                    item.PropertyChanged += Item_PropertyChanged;
                }
            }
        }
    }

    private void UpdateIsAllSelectedState() {
        if (ImportedTransactions == null || !ImportedTransactions.Any()) {
            _isAllSelected = false;
        }
        else if (ImportedTransactions.All(x => x.IsSelected)) {
            _isAllSelected = true; // Fully checked
        }
        else if (ImportedTransactions.All(x => !x.IsSelected)) {
            _isAllSelected = false; // Fully unchecked
        }
        else {
            _isAllSelected = null; // Partial selection (Square dash)
        }

        OnPropertyChanged(nameof(IsAllSelected));
    }

    public ImportReconciliationViewModel(Account account, BudgetService budgetService) {
        _account = account;
        _budgetService = budgetService;

        ImportFileCommand = new AsyncRelayCommand(PromptForFileAsync);

        ConfirmCsvImportCommand = new AsyncRelayCommand(ConfirmCsvImportAsync, () => CsvMapping?.CanImport == true);
        CancelCsvImportCommand = new RelayCommand(() => { IsMappingVisible = false; });

        SaveCommand = new RelayCommand(SaveAsync);

        // Track changes to update header state automatically
        // Single unified CollectionChanged handler
        ImportedTransactions.CollectionChanged += (s, e) => {
            if (e.NewItems != null) {
                foreach (ImportedTransactionViewModel item in e.NewItems) {
                    // Assign subcategory lookup delegate
                    item.GetDefaultBucketForSubCategory = (subCatId) => {
                        return SubCategoriesWithNone
                            .FirstOrDefault(sub => sub.Id == subCatId)?
                            .DefaultBucketId;
                    };

                    item.PropertyChanged += Item_PropertyChanged;
                }
            }

            if (e.OldItems != null) {
                foreach (ImportedTransactionViewModel item in e.OldItems) {
                    item.PropertyChanged -= Item_PropertyChanged;
                }
            }

            UpdateIsAllSelectedState();
        };

        // Use a lambda to capture the parameter (param) and call your method
        LinkTransactionsCommand = new RelayCommand(
            LinkTransactions,
            () => SelectedImported != null && !SelectedImported.IsMatched && SelectedManual != null &&
                  !SelectedManual.IsMatched
        );

        ImportAsNewCommand = new RelayCommand(
            ImportAsNew,
            () => SelectedImported != null && !SelectedImported.IsMatched
        );

        ClearMatchCommand = new RelayCommand(
            ClearMatch,
            () => SelectedImported != null && SelectedImported.IsMatched
        );

        InitializeDataCommand = new AsyncRelayCommand(LoadDataAsync);
    }

    public IAsyncRelayCommand InitializeDataCommand { get; }

    // private async void SaveAsync() {
    //     if (string.IsNullOrEmpty(LastFileName)) {
    //         return;
    //     }
    //
    //     try {
    //         var fixDate = false;
    //
    //         IsBusy = true;
    //
    //         // Yield back to UI thread to allow WPF to render the LoadingOverlay control
    //         await Task.Delay(50);
    //
    //         // Handled matched transactions
    //         foreach (var match in ImportedTransactions.Where(x =>
    //                      x.IsSelected &&
    //                      x.IsMatched && !string.IsNullOrEmpty(x.MatchedManualTransactionId) &&
    //                      x.MatchedManualTransactionDate != null && !string.IsNullOrEmpty(x.MatchedManualFitId))) {
    //             if (string.IsNullOrEmpty(match.BankId)) continue;
    //             if (string.IsNullOrEmpty(match.Payee)) continue;
    //             // Track each background database call
    //             await Task.Delay(10);
    //             if (!match.Id.HasValue) {
    //                 //if the id is null, it didn't actually match
    //                 continue;
    //             }
    //             await _budgetService.UpdateTransactionForBankFitIdAsync(
    //                 _account.Id,
    //                 match.MatchedManualTransactionId!,
    //                 match.BankId,
    //                 match.IsCleared,
    //                 match.Id.Value
    //             );
    //         }
    //
    //         // Handle new transactions that are checked but not matched
    //         foreach (var newItem in ImportedTransactions.Where(x => x.IsSelected && !x.IsMatched)) {
    //             if (string.IsNullOrEmpty(newItem.Payee)) continue;
    //             if (string.IsNullOrEmpty(newItem.BankId)) continue;
    //             var t = new Transaction {
    //                 AccountId = newItem.Amount > 0 ? null : _account.Id,
    //                 ToAccountId = newItem.Amount > 0 ? _account.Id : null,
    //                 Amount = newItem.Amount,
    //                 TransactionDate = newItem.Date ?? DateTime.Now,
    //                 Description = newItem.Payee,
    //                 FitId = newItem.BankId,
    //                 BillId = newItem.BillId == 0 ? null : newItem.BillId,
    //                 BucketId = newItem.BucketId == 0 ? null : newItem.BucketId,
    //                 SubCategoryId = newItem.SubCategoryId == 0 ? null : newItem.SubCategoryId,
    //                 FromAccountIsCleared = newItem.Amount > 0 ? null : newItem.IsCleared,
    //                 ToAccountIsCleared = newItem.Amount > 0 ? newItem.IsCleared : null,
    //             };
    //             await Task.Delay(10);
    //             await _budgetService.UpsertTransactionAsync(t);
    //         }
    //
    //         await Task.Delay(50);
    //
    //         // 4. Now these run sequentially on the UI thread with accurate database states
    //         await LoadDataAsync();
    //
    //         if (LastImportAsQfx != null && LastImportAsQfx.Value) {
    //             await ParseAndPopulateQfxAsync(LastFileName);
    //         }
    //
    //         if (LastImportAsQfx != null && !LastImportAsQfx.Value) {
    //             await ParseAndPopulateCsv(LastFileName);
    //         }
    //     }
    //     finally {
    //         IsBusy = false;
    //     }
    // }

    private async void SaveAsync() {
        if (!ImportedTransactions.Any()) return;

        try {
            IsBusy = true;
            await Task.Delay(50); // Yield for UI loader animation

            // Collect items that will be processed during this save batch
            var matchedToSave = ImportedTransactions
                .Where(x => x.IsSelected &&
                            x.IsMatched &&
                            !string.IsNullOrEmpty(x.MatchedManualTransactionId) &&
                            x.Id.HasValue &&
                            !string.IsNullOrEmpty(x.BankId) &&
                            !string.IsNullOrEmpty(x.Payee))
                .ToList();

            var newToSave = ImportedTransactions
                .Where(x => x.IsSelected &&
                            !x.IsMatched &&
                            !string.IsNullOrEmpty(x.BankId) &&
                            !string.IsNullOrEmpty(x.Payee))
                .ToList();

            // 1. Process matched transactions
            foreach (var match in matchedToSave) {
                await _budgetService.UpdateTransactionForBankFitIdAsync(
                    _account.Id,
                    match.MatchedManualTransactionId!,
                    match.BankId!,
                    match.IsCleared,
                    match.Id!.Value
                );
            }

            // 2. Process brand new transactions
            foreach (var newItem in newToSave) {
                var t = new Transaction {
                    AccountId = newItem.Amount > 0 ? null : _account.Id,
                    ToAccountId = newItem.Amount > 0 ? _account.Id : null,
                    Amount = newItem.Amount,
                    TransactionDate = newItem.Date ?? DateTime.Now,
                    Description = newItem.Payee!,
                    FitId = newItem.BankId!,
                    BillId = newItem.BillId == 0 ? null : newItem.BillId,
                    BucketId = newItem.BucketId == 0 ? null : newItem.BucketId,
                    SubCategoryId = newItem.SubCategoryId == 0 ? null : newItem.SubCategoryId,
                    FromAccountIsCleared = newItem.Amount > 0 ? null : newItem.IsCleared,
                    ToAccountIsCleared = newItem.Amount > 0 ? newItem.IsCleared : null,
                };

                await _budgetService.UpsertTransactionAsync(t);
            }

            // 3. Reload fresh database lookup state (UnreconciledManualTransactions, Bills, Buckets)
            await LoadDataAsync();

            // 4. In-Memory Pruning: Remove saved items directly from UI collection without disk parsing
            var savedItems = matchedToSave.Concat(newToSave).ToList();
            foreach (var item in savedItems) {
                ImportedTransactions.Remove(item);
            }

            // 5. Re-run auto-match for remaining stragglers against updated unreconciled database state
            if (ImportedTransactions.Any()) {
                AutoMatchTransactions();
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

        var rawParsedRecords = new List<ImportedTransactionViewModel>();

        // 1. Read CSV into temporary holding list
        using (var reader = new StreamReader(filePath))
        using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture)) {
            await csv.ReadAsync();
            csv.ReadHeader();

            while (await csv.ReadAsync()) {
                string bankId = csv.GetField(CsvMapping.BankIdHeader!) ?? Guid.NewGuid().ToString();
                string rawDate = csv.GetField(CsvMapping.DateHeader!) ?? "";
                string rawAmount = csv.GetField(CsvMapping.AmountHeader!) ?? "";
                string payee = csv.GetField(CsvMapping.PayeeHeader!) ?? "";

                if (!DateTime.TryParse(rawDate, CultureInfo.CurrentCulture, DateTimeStyles.None, out var date)) {
                    continue; // Skip invalid dates cleanly
                }

                decimal.TryParse(rawAmount, NumberStyles.Any, CultureInfo.InvariantCulture, out var amount);

                rawParsedRecords.Add(new ImportedTransactionViewModel {
                    BankId = bankId,
                    Date = date,
                    Amount = amount,
                    Payee = payee.Trim(),
                    Status = "Unmatched",
                    IsCleared = true
                });
            }
        }

        if (!rawParsedRecords.Any()) return;

        // 2. Fetch only the imported FitIds that are ALREADY cleared in the DB
        var incomingBankIds = rawParsedRecords
            .Where(x => !string.IsNullOrWhiteSpace(x.BankId))
            .Select(x => x.BankId!)
            .ToList();

        var existingDbIds = await _budgetService.GetAlreadyImportedBankIdsAsync(_account.Id, incomingBankIds);
        var processedBankIds = new HashSet<string>(existingDbIds, StringComparer.OrdinalIgnoreCase);

        // 3. O(1) Lookup dictionary for unreconciled manual records
        var manualLookup = UnreconciledManualTransactions
            .Where(x => !string.IsNullOrWhiteSpace(x.FitId))
            .ToLookup(x => x.FitId!, StringComparer.OrdinalIgnoreCase);

        var filteredImports = new List<ImportedTransactionViewModel>(rawParsedRecords.Count);
        var manualRecordsToRemove = new List<ManualTransactionViewModel>();

        // 4. Single-pass partition: Separate new imports from already-cleared duplicates
        foreach (var record in rawParsedRecords) {
            if (processedBankIds.Contains(record.BankId!)) {
                // Locate manual record for removal in O(1) time
                var matches = manualLookup[record.BankId!];
                manualRecordsToRemove.AddRange(matches);
            }
            else {
                filteredImports.Add(record);
            }
        }

        // 5. Prune already-cleared manual transactions from UI collection
        foreach (var rec in manualRecordsToRemove) {
            UnreconciledManualTransactions.Remove(rec);
        }

        // 6. Populate UI collection in a single batch update
        if (ImportedTransactions is RangeObservableCollection<ImportedTransactionViewModel> rangeCollection) {
            rangeCollection.AddRange(filteredImports);
        }
        else {
            foreach (var item in filteredImports) {
                ImportedTransactions.Add(item);
            }
        }

        // 7. Auto-match pass
        AutoMatchTransactions();
        await AutoApplySubCategory();
    }

    private async Task ParseAndPopulateQfxAsync(string filePath) {
        if (!File.Exists(filePath)) return;

        LastImportAsQfx = true;
        ImportedTransactions.Clear();

        string content = await File.ReadAllTextAsync(filePath);

        // 1. Normalize SGML transaction blocks
        var matches = Regex.Matches(content, @"<STMTTRN>(.*?)(?=</STMTTRN>|<STMTTRN>|</STMTRS>)",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);

        var rawParsedRecords = new List<ImportedTransactionViewModel>();

        // 2. Initial parse into in-memory collection
        foreach (Match m in matches) {
            string txBlock = m.Groups[1].Value;
            string bankId = GetQfxTagValue(txBlock, "FITID");
            if (string.IsNullOrWhiteSpace(bankId)) continue;

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

            rawParsedRecords.Add(new ImportedTransactionViewModel {
                BankId = bankId,
                Date = date,
                Amount = amount,
                Payee = payee, // Already sanitized by GetQfxTagValue
                Status = "Unmatched",
                IsCleared = true
            });
        }

        if (!rawParsedRecords.Any()) return;

        // 3. Query DB ONLY for the FITIDs present in this QFX file
        var incomingBankIds = rawParsedRecords
            .Where(x => !string.IsNullOrWhiteSpace(x.BankId))
            .Select(x => x.BankId!)
            .ToList();

        var existingDbIds = await _budgetService.GetAlreadyImportedBankIdsAsync(_account.Id, incomingBankIds);
        var processedBankIds = new HashSet<string>(existingDbIds, StringComparer.OrdinalIgnoreCase);

        // 4. O(1) Lookup for unreconciled manual records
        var manualLookup = UnreconciledManualTransactions
            .Where(x => !string.IsNullOrWhiteSpace(x.FitId))
            .ToLookup(x => x.FitId!, StringComparer.OrdinalIgnoreCase);

        var filteredImports = new List<ImportedTransactionViewModel>(rawParsedRecords.Count);
        var transactionsToRemove = new List<ManualTransactionViewModel>();

        // 5. Partition: Filter out already-imported bank IDs & flag manual matches for removal
        foreach (var record in rawParsedRecords) {
            if (processedBankIds.Contains(record.BankId!)) {
                var matchesToRemove = manualLookup[record.BankId!];
                transactionsToRemove.AddRange(matchesToRemove);
            }
            else {
                filteredImports.Add(record);
            }
        }

        // 6. Safely mutate the UI tracking collection outside the parsing loop
        foreach (var rec in transactionsToRemove) {
            UnreconciledManualTransactions.Remove(rec);
        }

        // 7. Populate ImportedTransactions in a single batch notification
        if (ImportedTransactions is RangeObservableCollection<ImportedTransactionViewModel> rangeCollection) {
            rangeCollection.AddRange(filteredImports);
        }
        else {
            foreach (var item in filteredImports) {
                ImportedTransactions.Add(item);
            }
        }

        // 8. Auto-match pass
        AutoMatchTransactions();
        await AutoApplySubCategory();

    }
    
    private async Task AutoApplySubCategory() {
        foreach (var x in ImportedTransactions.Where(x => !x.IsMatched)) {
            if (x.SubCategoryId == null && !string.IsNullOrWhiteSpace(x.Payee)) {
                x.SubCategoryId = await _budgetService.GetSuggestedSubCategoryIdAsync(x.Payee, x.Date);
            }
        }
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
                matchedManualIds.Add(exactMatch.TransactionId!);
                imported.BillId = exactMatch.BillId;
                imported.BucketId = exactMatch.BucketId;
                imported.IsSelected = true;
                imported.IsCleared = true;
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
                matchedManualIds.Add(match.TransactionId!);
            }
        }

        // Set UI Selection defaults cleanly outside the loop execution window
        var firstUnmatchedImport = ImportedTransactions.FirstOrDefault(x => !x.IsMatched);
        if (firstUnmatchedImport != null) {
            SelectedImported = firstUnmatchedImport;
            SelectedManual = UnreconciledManualTransactions.FirstOrDefault(m =>
                !m.IsMatched && Math.Abs(m.Amount) == Math.Abs(firstUnmatchedImport.Amount));
        }
    }

// Clean helper parameterized explicitly to match your class definitions
    private void ApplyMatch(ImportedTransactionViewModel imported, ManualTransactionViewModel manual,
        string statusText) {
        imported.Id = manual.Id;
        imported.IsMatched = true;
        imported.IsCleared = true;
        imported.IsSelected = true;
        imported.Status = statusText;
        imported.MatchedManualFitId = manual.FitId;
        imported.MatchedManualTransactionDate = manual.TransactionDate;
        imported.MatchedManualTransactionId = manual.TransactionId;
        imported.BillId = manual.BillId;
        imported.BucketId = manual.BucketId;
    }

    private void FilterManualSuggestions() {
        // Optional: Filter or highlight the Manual list here based on SelectedImported's Amount/Date
    }

    // Update the methods to handle the object parameter:
    private void LinkTransactions() {
        if (SelectedImported == null || SelectedManual == null) return;
        SelectedImported.Id = SelectedManual.Id;
        SelectedImported.IsMatched = true;
        SelectedImported.IsCleared = true;
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
            SelectedImported.Id = null;
            SelectedImported.IsMatched = false;
            SelectedImported.IsCleared = false;
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
        var unreconciledTransactions = (await _budgetService.GetAllUnclearedTransactionsAsync()).ToList();
        unreconciledTransactions = unreconciledTransactions
            .Where(x => (x.AccountId == _account.Id && x.FromAccountIsCleared == false) ||
                        (x.ToAccountId == _account.Id && x.ToAccountIsCleared == false)).ToList();

        UnreconciledManualTransactions.Clear();
        var temp = new List<ManualTransactionViewModel>(unreconciledTransactions.Count);
        foreach (var transaction in unreconciledTransactions) {
            temp.Add(new ManualTransactionViewModel {
                Id = (int?)(transaction.AccountId == _account.Id ? transaction.FromRecordId : transaction.ToRecordId),
                FitId = transaction.FitId,
                TransactionDate = transaction.TransactionDate,
                Amount = transaction.Amount,
                Description = transaction.Description, TransactionId = transaction.TransactionId.ToString(),
                BillId = transaction.BillId,
                BucketId = transaction.BucketId
            });
        }

        UnreconciledManualTransactions.AddRange(temp);

        var billsFromDb = (await _budgetService.GetAllBillsAsync()).ToList();

        // Pre-allocate space for "None" (+1) plus all DB items
        var billsTemp = new List<Bill>(billsFromDb.Count + 1) {
            new Bill { Id = 0, Name = "None" }
        };

        billsTemp.AddRange(billsFromDb);

        BillsWithNone.Clear();
        BillsWithNone.AddRange(billsTemp);

        var bucketsFromDb = (await _budgetService.GetAllBucketsAsync()).ToList();

        // Pre-allocate space for "None" (+1) plus all DB items
        var bucketsTemp = new List<BudgetBucket>(bucketsFromDb.Count + 1) {
            new BudgetBucket { Id = 0, Name = "None" }
        };

        bucketsTemp.AddRange(bucketsFromDb);

        BucketsWithNone.Clear();
        BucketsWithNone.AddRange(bucketsTemp);

        var subCategoriesFromDb = (await _budgetService.GetAllSubCategoriesAsync()).ToList();

        // Pre-allocate space for "None" (+1) plus all DB items
        var subCategoriesTemp = new List<SubCategory>(subCategoriesFromDb.Count + 1) {
            new SubCategory { Id = 0, Name = "None" }
        };

        subCategoriesTemp.AddRange(subCategoriesFromDb);

        SubCategoriesWithNone.Clear();
        SubCategoriesWithNone.AddRange(subCategoriesTemp);
    }
}