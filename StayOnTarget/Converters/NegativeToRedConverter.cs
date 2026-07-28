using System.Globalization;
using System.Windows.Data;

namespace StayOnTarget.Converters;

public class NegativeToRedConverter : IValueConverter {
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) {
        if (value is decimal d) {
            return d < 0;
        }

        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) {
        throw new NotImplementedException();
    }
}
