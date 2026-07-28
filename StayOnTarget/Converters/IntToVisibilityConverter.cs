using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace StayOnTarget.Converters;

public class IntToVisibilityConverter : IValueConverter {
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
        if (value is int id && id == 0) {
            // Hide the button (and do not reserve layout space) when Id is 0
            return Visibility.Collapsed;
        }

        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
        // This method is used for two-way binding; a simple implementation is sufficient here.
        return DependencyProperty.UnsetValue;
    }
}
