using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace StayOnTarget.Converters;

public class EnumToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value?.ToString() is not { } checkValue || parameter?.ToString() is not { } targetValue)
            return Visibility.Collapsed;

        // Support pipe-separated parameters for multiple matches e.g. "FixedMonthlyAmount|Hybrid"
        string[] targets = targetValue.Split('|');

        foreach (var target in targets)
        {
            if (string.Equals(checkValue.Trim(), target.Trim(), StringComparison.OrdinalIgnoreCase))
                return Visibility.Visible;
        }

        return Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}