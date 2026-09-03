using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using StayOnTarget.Themes;

namespace StayOnTarget.Converters;

public class DeltaToBrushConverter : IValueConverter
{
    public Brush PositiveBrush { get; set; } = System.Windows.Application.Current.TryFindResource(ThemeKeys.PositiveBrush) as SolidColorBrush ?? Brushes.Green;
    public Brush NegativeBrush { get; set; } = System.Windows.Application.Current.TryFindResource(ThemeKeys.NegativeBrush) as SolidColorBrush ?? Brushes.Red;
    public Brush NeutralBrush { get; set; } = System.Windows.Application.Current.TryFindResource(ThemeKeys.SecondaryTextBrush) as SolidColorBrush ?? Brushes.Gray;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is decimal d)
        {
            if (d > 0) return PositiveBrush;
            if (d < 0) return NegativeBrush;
        }
        else if (value is double db)
        {
            if (db > 0) return PositiveBrush;
            if (db < 0) return NegativeBrush;
        }
        else if (value is int i)
        {
            if (i > 0) return PositiveBrush;
            if (i < 0) return NegativeBrush;
        }
        else if (value is long l)
        {
            if (l > 0) return PositiveBrush;
            if (l < 0) return NegativeBrush;
        }

        return NeutralBrush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
