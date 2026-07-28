using System.Globalization;
using System.Windows.Data;

namespace StayOnTarget.Converters;

public class AbsoluteValueConverter : IValueConverter {
    // When reading from the Model to show in the TextBox
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
        if (value is decimal decimalValue) {
            return Math.Abs(decimalValue);
        }

        return value;
    }

    // When writing from the TextBox back to the Model
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
        if (value is string stringValue && decimal.TryParse(stringValue, out decimal result)) {
            return Math.Abs(result); // Force it to stay positive in the Clone
        }

        if (value is decimal decimalValue) {
            return Math.Abs(decimalValue);
        }

        return value;
    }
}
