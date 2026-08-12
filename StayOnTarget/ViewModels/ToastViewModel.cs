using System.Net.Mime;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;
using StayOnTarget.Themes;

namespace StayOnTarget.ViewModels;

public class ToastViewModel : ViewModelBase
{
    private readonly DispatcherTimer _autoCloseTimer;

    public string Message { get; }
    
    public Brush Background { get; set; } = Brushes.Goldenrod;
    public Brush Border { get; set; } = Brushes.DarkRed;
    public Brush Text { get; set; } = Brushes.Black;
    
    public static SolidColorBrush DangerBrush { get; set; } = System.Windows.Application.Current.TryFindResource(ThemeKeys.ErrorBrush)! as SolidColorBrush ;
    public static SolidColorBrush SuccessBrush { get; set; } = System.Windows.Application.Current.TryFindResource(ThemeKeys.SuccessBrush)! as SolidColorBrush ;
    public static SolidColorBrush WarningBrush { get; set; } = System.Windows.Application.Current.TryFindResource(ThemeKeys.WarningBrush)! as SolidColorBrush ;
    public static SolidColorBrush InfoBrush { get; set; } = System.Windows.Application.Current.TryFindResource(ThemeKeys.InfoBrush)! as SolidColorBrush ;
    public static SolidColorBrush NeutralBrush { get; set; } = System.Windows.Application.Current.TryFindResource(ThemeKeys.NeutralBrush)! as SolidColorBrush ;
    
    public static SolidColorBrush DangerBorderBrush { get; set; } = System.Windows.Application.Current.TryFindResource(ThemeKeys.ErrorBorderBrush)! as SolidColorBrush ;
    public static SolidColorBrush SuccessBorderBrush { get; set; } = System.Windows.Application.Current.TryFindResource(ThemeKeys.SuccessBorderBrush)! as SolidColorBrush ;
    public static SolidColorBrush WarningBorderBrush { get; set; } = System.Windows.Application.Current.TryFindResource(ThemeKeys.WarningBorderBrush)! as SolidColorBrush ;
    public static SolidColorBrush InfoBorderBrush { get; set; } = System.Windows.Application.Current.TryFindResource(ThemeKeys.InfoBorderBrush)! as SolidColorBrush ;
    public static SolidColorBrush NeutralBorderBrush { get; set; } = System.Windows.Application.Current.TryFindResource(ThemeKeys.NeutralBorderBrush)! as SolidColorBrush ;
    
    public static SolidColorBrush DangerTextBrush { get; set; } = System.Windows.Application.Current.TryFindResource(ThemeKeys.ErrorTextBrush)! as SolidColorBrush ;
    public static SolidColorBrush SuccessTextBrush { get; set; } = System.Windows.Application.Current.TryFindResource(ThemeKeys.SuccessTextBrush)! as SolidColorBrush ;
    public static SolidColorBrush WarningTextBrush { get; set; } = System.Windows.Application.Current.TryFindResource(ThemeKeys.WarningTextBrush)! as SolidColorBrush ;
    public static SolidColorBrush InfoTextBrush { get; set; } = System.Windows.Application.Current.TryFindResource(ThemeKeys.InfoTextBrush)! as SolidColorBrush ;
    public static SolidColorBrush NeutralTextBrush { get; set; } = System.Windows.Application.Current.TryFindResource(ThemeKeys.NeutralTextBrush)! as SolidColorBrush ;
    
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
            case ToastType.Info: Background = InfoBrush; Text = InfoTextBrush; Border = InfoBorderBrush; break;
            case ToastType.Danger: Background = DangerBrush; Text = DangerTextBrush; Border = DangerBorderBrush;break;
            case ToastType.Success: Background = SuccessBrush;Text = SuccessTextBrush; Border = SuccessBorderBrush; break;
            case ToastType.Warning: Background = WarningBrush; Text = WarningTextBrush; Border = WarningBorderBrush;break;
            case ToastType.Neutral: Background = NeutralBrush; Text = NeutralTextBrush; Border = NeutralBorderBrush;break;
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