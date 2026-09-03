using CommunityToolkit.Mvvm.ComponentModel;

namespace StayOnTarget.ViewModels;

public class TransactionStatusItemViewModel : ObservableObject {
    private ReconciliationStatus _currentStatus;
    
    public int AccountId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public TransactionSide Side { get; set; } // Identifies From or To
    public Action<TransactionStatusItemViewModel, ReconciliationStatus>? StatusChangedCallback { get; set; }

    public List<ReconciliationStatus> AvailableStatuses { get; } = new() {
        ReconciliationStatus.Uncleared,
        ReconciliationStatus.Cleared,
        ReconciliationStatus.Reconciled
    };

    public ReconciliationStatus CurrentStatus {
        get => _currentStatus;
        set {
            if (SetProperty(ref _currentStatus, value)) {
                StatusChangedCallback?.Invoke(this, value);
            }
        }
    }

    public string StatusDetailsText { get; set; } = string.Empty;
}

public enum TransactionSide { From, To }

public enum ReconciliationStatus {
    Uncleared,
    Cleared,
    Reconciled
}