using System.Globalization;
using System.Windows.Data;

namespace StayOnTarget.Converters;

public class ProjectionYearsLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int years)
        {
            return $"Financial Projection ({years} {(years == 1 ? "Year" : "Years")})";
        }

        return "Financial Projection";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}