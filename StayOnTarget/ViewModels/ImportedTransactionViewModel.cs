namespace StayOnTarget.ViewModels {
    // A wrapper for the QFX imported transactions
    public class ImportedTransactionViewModel : ViewModelBase {
        // Delegate assigned by ImportReconciliationViewModel's CollectionChanged handler
        public Func<int, int?>? GetDefaultBucketForSubCategory { get; set; }

        private string? _bankId;
        public string? BankId {
            get => _bankId;
            set => SetProperty(ref _bankId, value);
        }

        private DateTime? _date;
        public DateTime? Date {
            get => _date;
            set => SetProperty(ref _date, value);
        }

        private decimal _amount;
        public decimal Amount {
            get => _amount;
            set => SetProperty(ref _amount, value);
        }

        private string? _payee;
        public string? Payee {
            get => _payee;
            set => SetProperty(ref _payee, value);
        }
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

        private string? _matchedManualFitId;
        public string? MatchedManualFitId {
            get => _matchedManualFitId;
            set => SetProperty(ref _matchedManualFitId, value);
        }

        private DateTime? _matchedManualTransactionDate;
        public DateTime? MatchedManualTransactionDate {
            get => _matchedManualTransactionDate;
            set => SetProperty(ref _matchedManualTransactionDate, value);
        }

        private string? _matchedManualTransactionId;
        public string? MatchedManualTransactionId {
            get => _matchedManualTransactionId;
            set => SetProperty(ref _matchedManualTransactionId, value);
        }

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
            // 1. Skip if already matched (matched transactions maintain their existing mappings)
            // 2. Skip if SubCategory is empty or 0 ("None")
            // 3. Only auto-fill if BucketId is currently unset or 0 ("None")
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