using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using CsvHelper;
using System.Globalization;
using StayOnTarget.Helpers;
using Serilog;

namespace StayOnTarget.ViewModels;

public class CsvImportMappingViewModel : ViewModelBase {
    public string FilePath { get; }
    public List<string> Headers { get; private set; } = new();
    private List<Dictionary<string, string>> _rawPreviewData = new();
    public RangeObservableCollection<ImportedTransactionViewModel> PreviewRows { get; } = new();

    public ObservableCollection<string?> AvailableHeaders { get; } = new();

    private string? _dateHeader;
    public string? DateHeader {
        get => _dateHeader;
        set {
            try {
                if (SetProperty(ref _dateHeader, value)) {
                    RefreshPreview();
                    OnPropertyChanged(nameof(CanImport));
                }
            }
            catch (Exception ex) {
                Log.Error(ex, "Error setting DateHeader in CsvImportMappingViewModel[cite: 19].");
                
            }
        }
    }

    private string? _amountHeader;
    public string? AmountHeader {
        get => _amountHeader;
        set {
            try {
                if (SetProperty(ref _amountHeader, value)) {
                    RefreshPreview();
                    OnPropertyChanged(nameof(CanImport));
                }
            }
            catch (Exception ex) {
                Log.Error(ex, "Error setting AmountHeader in CsvImportMappingViewModel[cite: 19].");
                
            }
        }
    }

    private string? _payeeHeader;
    public string? PayeeHeader {
        get => _payeeHeader;
        set {
            try {
                if (SetProperty(ref _payeeHeader, value)) {
                    RefreshPreview();
                    OnPropertyChanged(nameof(CanImport));
                }
            }
            catch (Exception ex) {
                Log.Error(ex, "Error setting PayeeHeader in CsvImportMappingViewModel[cite: 19].");
                
            }
        }
    }

    private string? _bankIdHeader;
    public string? BankIdHeader {
        get => _bankIdHeader;
        set {
            try {
                if (SetProperty(ref _bankIdHeader, value)) {
                    RefreshPreview();
                    OnPropertyChanged(nameof(CanImport));
                }
            }
            catch (Exception ex) {
                Log.Error(ex, "Error setting BankIdHeader in CsvImportMappingViewModel[cite: 19].");
                
            }
        }
    }

    public bool CanImport => !string.IsNullOrEmpty(DateHeader) &&
                            !string.IsNullOrEmpty(AmountHeader) &&
                            !string.IsNullOrEmpty(PayeeHeader) &&
                            !string.IsNullOrEmpty(BankIdHeader);

    public CsvImportMappingViewModel(string filePath, string mappingConfigPath) {
        try {
            FilePath = filePath;
            LoadPreview();
            
            AvailableHeaders.Add(null);
            foreach (var header in Headers) {
                AvailableHeaders.Add(header);
            }

            if (File.Exists(mappingConfigPath)) {
                LoadMapping(mappingConfigPath);
            } else {
                AutoDetectHeaders();
            }
        }
        catch (Exception ex) {
            Log.Fatal(ex, "Critical error initializing CsvImportMappingViewModel[cite: 19].");
            
        }
    }

    private void LoadPreview() {
        try {
            using var reader = new StreamReader(FilePath);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            csv.Read();
            csv.ReadHeader();
            Headers = csv.HeaderRecord?.ToList() ?? new List<string>();

            int count = 0;
            while (csv.Read() && count < 5) {
                var row = new Dictionary<string, string>();
                foreach (var header in Headers) {
                    row[header] = csv.GetField(header) ?? "";
                }
                _rawPreviewData.Add(row);
                count++;
            }
        }
        catch (Exception ex) {
            Log.Error(ex, "Error loading preview for CSV import[cite: 19].");
            
        }
    }

    private void RefreshPreview() {
        try {
            PreviewRows.Clear();
            var tempRows = new List<ImportedTransactionViewModel>(_rawPreviewData.Count);

            foreach (var rawRow in _rawPreviewData) {
                var tx = new ImportedTransactionViewModel();
            
                if (!string.IsNullOrEmpty(DateHeader) && rawRow.TryGetValue(DateHeader, out var dateStr)) {
                    if (DateTime.TryParse(dateStr, CultureInfo.CurrentCulture, DateTimeStyles.None, out var d))
                        tx.Date = d;
                }

                if (!string.IsNullOrEmpty(AmountHeader) && rawRow.TryGetValue(AmountHeader, out var amountStr)) {
                    if (decimal.TryParse(amountStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var a))
                        tx.Amount = a;
                }

                if (!string.IsNullOrEmpty(PayeeHeader) && rawRow.TryGetValue(PayeeHeader, out var payee)) {
                    tx.Payee = payee.Trim();
                }

                if (!string.IsNullOrEmpty(BankIdHeader) && rawRow.TryGetValue(BankIdHeader, out var bankId)) {
                    tx.BankId = bankId;
                } else {
                    tx.BankId = "Preview";
                }

                tempRows.Add(tx);
            }

            PreviewRows.AddRange(tempRows);
        }
        catch (Exception ex) {
            Log.Error(ex, "Error refreshing preview rows[cite: 19].");
            
        }
    }

    private void AutoDetectHeaders() {
        try {
            foreach (var header in Headers) {
                var lower = header.ToLower();
                if (lower.Contains("date") && DateHeader == null) DateHeader = header;
                else if ((lower.Contains("amount") || lower.Contains("value")) && AmountHeader == null) AmountHeader = header;
                else if ((lower.Contains("payee") || lower.Contains("description") || lower.Contains("name")) && PayeeHeader == null) PayeeHeader = header;
                else if ((lower.Contains("id") || lower.Contains("fitid") || lower.Contains("transaction id") || lower.Contains("reference")) && BankIdHeader == null) BankIdHeader = header;
            }
            
            if (_rawPreviewData.Count > 0) {
                var firstRow = _rawPreviewData[0];
                foreach (var kvp in firstRow) {
                    if (DateHeader == null && DateTime.TryParse(kvp.Value, out _)) DateHeader = kvp.Key;
                    else if (AmountHeader == null && decimal.TryParse(kvp.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out _)) AmountHeader = kvp.Key;
                }
            }

            RefreshPreview();
        }
        catch (Exception ex) {
            Log.Error(ex, "Error auto-detecting CSV headers[cite: 19].");
            
        }
    }

    public void SaveMapping(string path) {
        try {
            var mapping = new Dictionary<string, string?> {
                { "Date", DateHeader },
                { "Amount", AmountHeader },
                { "Payee", PayeeHeader },
                { "BankId", BankIdHeader }
            };
            File.WriteAllText(path, JsonSerializer.Serialize(mapping));
        }
        catch (Exception ex) {
            Log.Error(ex, "Error saving CSV mapping configuration[cite: 19].");
            
        }
    }

    private void LoadMapping(string path) {
        try {
            var json = File.ReadAllText(path);
            var mapping = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (mapping != null) {
                if (mapping.TryGetValue("Date", out var val) && Headers.Contains(val)) DateHeader = val;
                if (mapping.TryGetValue("Amount", out val) && Headers.Contains(val)) AmountHeader = val;
                if (mapping.TryGetValue("Payee", out val) && Headers.Contains(val)) PayeeHeader = val;
                if (mapping.TryGetValue("BankId", out val) && Headers.Contains(val)) BankIdHeader = val;
            }
        } catch (Exception ex) {
            Log.Warning(ex, "Failed to load mapping config, falling back to auto-detection[cite: 19].");
            
            AutoDetectHeaders();
        }
    }
}