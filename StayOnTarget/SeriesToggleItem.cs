
using System.ComponentModel;
using StayOnTarget.Models;

namespace StayOnTarget;

public class SeriesToggleItem : INotifyPropertyChanged {
    public string Name { get; set; } = string.Empty;
    public AccountType Type { get; set; }

    private bool _isVisible = true;
    public bool IsVisible {
        get => _isVisible;
        set {
            if (_isVisible != value) {
                _isVisible = value;
                OnPropertyChanged(nameof(IsVisible));
                OnVisibilityChanged?.Invoke();
            }
        }
    }

    // Allows updating state without triggering the local change broadcast callback
    public void SetIsVisibleQuietly(bool value) {
        if (_isVisible != value) {
            _isVisible = value;
            OnPropertyChanged(nameof(IsVisible));
        }
    }

    public Action? OnVisibilityChanged { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}