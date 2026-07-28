using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace StayOnTarget.Converters;

public class AmountToBrushConverter : IValueConverter {
    private static readonly SolidColorBrush ModernRed =
        new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C62828"));

    private static readonly SolidColorBrush StandardDark = Brushes.Black; // Or a soft dark charcoal like #2C3E50

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
        if (value is decimal amount) {
            // Only apply red if it's a negative expense
            return amount < 0 ? ModernRed : StandardDark;
        }

        return StandardDark;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
        throw new NotImplementedException();
    }
}
