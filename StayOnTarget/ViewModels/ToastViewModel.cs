using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;

namespace StayOnTarget.ViewModels;

public class ToastViewModel : ViewModelBase
{
    private readonly DispatcherTimer _autoCloseTimer;

    public string Message { get; }
    
    public Brush Background { get; set; } = Brushes.Goldenrod;
    public Brush Border { get; set; } = Brushes.DarkRed;
    public Brush Text { get; set; } = Brushes.Black;
    
    public static SolidColorBrush DangerBrush { get; set; } = Brushes.Red;
    public static SolidColorBrush SuccessBrush { get; set; } = Brushes.YellowGreen;
    public static SolidColorBrush WarningBrush { get; set; } = Brushes.LightGoldenrodYellow;
    public static SolidColorBrush InfoBrush { get; set; } = Brushes.LightSkyBlue;
    
    public IRelayCommand CloseCommand { get; }

    public ToastViewModel(string message, Action<ToastViewModel> onClose, ToastType? type = null)
    {
        Message = message;
        if (type == null) {
            type  = ToastType.Neutral;
        }

        SetProperties(type.Value);
        
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

    private void SetProperties(ToastType toastType) {
        switch (toastType) {
            case ToastType.Info: Background = InfoBrush; Text = Brushes.White; Border = Brushes.DeepSkyBlue; break;
            case ToastType.Danger: Background = DangerBrush; Text = Brushes.White; Border = Brushes.Firebrick;break;
            case ToastType.Success: Background = SuccessBrush;Text = Brushes.White; Border = Brushes.Black; break;
            case ToastType.Warning: Background = WarningBrush; Text = Brushes.DimGray; Border = Brushes.Black;break;
            case ToastType.Neutral: Background = Brushes.LightGray; Text = Brushes.Black; Border = Brushes.DimGray;break;
        }
    }
}

public enum ToastType {
    Info,
    Danger,
    Success,
    Warning,
    Neutral
}