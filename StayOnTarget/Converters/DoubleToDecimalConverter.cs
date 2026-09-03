using System.Globalization;
using System.Windows.Data;

namespace StayOnTarget.Converters
{
    public class DoubleToDecimalConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal d)
            {
                return (double)d;
            }
            return 0.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double d)
            {
                return (decimal)d;
            }
            return 0.0m;
        }
    }
}
