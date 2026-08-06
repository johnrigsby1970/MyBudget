using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace StayOnTarget.Converters;

public class NullOrZeroToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool hasValue = value switch
        {
            null => false,
            int i => i > 0,
            _ => true
        };

        return hasValue ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}