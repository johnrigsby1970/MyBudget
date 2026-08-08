using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace StayOnTarget.Converters;

public class MarkBillAsPaidVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool hasValue = value switch
        {
            null => false,
            int i => i > 0,
            decimal d => d > 0m,
            double db => db > 0,
            _ => true
        };
        if (parameter != null && parameter.ToString() == "Invert") {
            return hasValue ? Visibility.Visible : Visibility.Collapsed;
        }
        return hasValue ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}