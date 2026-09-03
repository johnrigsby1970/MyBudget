using CommunityToolkit.Mvvm.ComponentModel;

namespace StayOnTarget.ViewModels;

public class ViewModelBase : ObservableObject
{
    // public event PropertyChangedEventHandler? PropertyChanged;
    //
    // protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    // {
    //     PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    // }
    //
    // protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    // {
    //     if (Equals(storage, value)) return false;
    //     storage = value;
    //     OnPropertyChanged(propertyName);
    //     return true;
    // }
}
