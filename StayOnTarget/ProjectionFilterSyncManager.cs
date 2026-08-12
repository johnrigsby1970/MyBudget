namespace StayOnTarget;

public static class ProjectionFilterSyncManager {
    private static bool _isSyncEnabled = true; // Global Sync default

    public static bool IsSyncEnabled {
        get => _isSyncEnabled;
        set {
            if (_isSyncEnabled != value) {
                _isSyncEnabled = value;
                OnSyncEnabledChanged?.Invoke(_isSyncEnabled);
            }
        }
    }

    public static bool IsTotalBalanceVisible { get; set; } = true;
    public static Dictionary<string, bool> ToggleStates { get; } = new();

    public static event Action? OnFilterStateChanged;
    public static event Action<bool>? OnSyncEnabledChanged;

    public static void BroadcastState(bool totalBalanceVisible, IEnumerable<SeriesToggleItem> toggles) {
        if (!IsSyncEnabled) return;

        IsTotalBalanceVisible = totalBalanceVisible;
        foreach (var item in toggles) {
            ToggleStates[item.Name] = item.IsVisible;
        }
        OnFilterStateChanged?.Invoke();
    }
}