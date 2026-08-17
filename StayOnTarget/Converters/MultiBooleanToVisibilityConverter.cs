using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace StayOnTarget.Converters;

public class MultiBooleanToVisibilityConverter : IMultiValueConverter {
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) {
        // Values expected:
        // values[0]: EnableSnowball (bool)
        // values[1]: IsSnowballProjecting (bool)

        if (values.Length < 2 || values[0] is not bool enableSnowball || values[1] is not bool isProjecting) {
            return Visibility.Collapsed;
        }

        // Show chart ONLY when EnableSnowball is true AND IsSnowballProjecting is false
        bool shouldShow = enableSnowball && !isProjecting;

        return shouldShow ? Visibility.Visible : Visibility.Collapsed;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) {
        throw new NotImplementedException();
    }
}