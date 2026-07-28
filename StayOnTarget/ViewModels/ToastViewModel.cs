using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;

namespace StayOnTarget.ViewModels;

public class ToastViewModel : ViewModelBase
{
    private readonly DispatcherTimer _autoCloseTimer;

    public string Message { get; }

    public IRelayCommand CloseCommand { get; }

    public ToastViewModel(string message, Action<ToastViewModel> onClose)
    {
        Message = message;

        CloseCommand = new RelayCommand(() => 
        {
            _autoCloseTimer?.Stop(); // Stop timer if manually closed
            onClose(this);
        });

        // Set up auto-close timer on the UI thread
        _autoCloseTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _autoCloseTimer.Tick += (s, e) =>
        {
            _autoCloseTimer.Stop();
            onClose(this);
        };
        _autoCloseTimer.Start();
    }
}