using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace StayOnTarget.Converters;

public class BooleanAndToVisibilityConverter : IMultiValueConverter {
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) {
        // Ensure all incoming values are booleans and all of them are True
        bool allTrue = values.OfType<bool>().All(b => b);

        return allTrue ? Visibility.Visible : Visibility.Collapsed;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) {
        throw new NotImplementedException();
    }

    public object[] ConvertBack(object[] value, Type[] targetTypes, object parameter, CultureInfo culture) {
        throw new NotImplementedException();
    }
}
