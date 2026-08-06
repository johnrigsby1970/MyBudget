namespace StayOnTarget.ViewModels {
    // A wrapper for the QFX imported transactions
    public class ImportedTransactionViewModel : ViewModelBase {
        // Delegate assigned by ImportReconciliationViewModel's CollectionChanged handler
        public Func<int, int?>? GetDefaultBucketForSubCategory { get; set; }

        public string? BankId { get; set; } // The FITID from the QFX
        public DateTime? Date { get; set; }
        public decimal Amount { get; set; }
        public string? Payee { get; set; }

        private bool _isMatched;
        
        public bool IsMatched {
            get => _isMatched;
            set => SetProperty(ref _isMatched, value);
        }

        private bool _isCleared;

        public bool IsCleared {
            get => _isCleared;
            set => SetProperty(ref _isCleared, value);
        }
        
        private string _status = "Unmatched";

        public string Status {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        public string? MatchedManualFitId { get; set; }
        public DateTime? MatchedManualTransactionDate { get; set; }
        public string? MatchedManualTransactionId { get; set; }

        private int? _bucketId;
        public int? BucketId {
            get => _bucketId;
            set => SetProperty(ref _bucketId, value);
        }
        
        private int? _subCategoryId;
        public int? SubCategoryId {
            get => _subCategoryId;
            set {
                if (SetProperty(ref _subCategoryId, value)) {
                    // Trigger auto-assignment when SubCategoryId changes in the DataGrid row
                    ApplyDefaultBucket();
                }
            }
        }
        
        private int? _billId;
        public int? BillId {
            get => _billId;
            set => SetProperty(ref _billId, value);
        }

        private bool _isSelected;
        public bool IsSelected {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }
        
        private void ApplyDefaultBucket() {
            // 1. Skip matched transactions (reconciled data takes precedence)
            // 2. Ignore SubCategoryId if empty or set to 0 ("None")
            // 3. Only auto-fill if BucketId is currently unassigned or 0 ("None")
            if (!IsMatched && 
                SubCategoryId.HasValue && 
                SubCategoryId.Value != 0 && 
                (!BucketId.HasValue || BucketId == 0)) {

                var defaultBucket = GetDefaultBucketForSubCategory?.Invoke(SubCategoryId.Value);
            
                if (defaultBucket.HasValue && defaultBucket.Value != 0) {
                    BucketId = defaultBucket.Value;
                }
            }
        }
    }
}