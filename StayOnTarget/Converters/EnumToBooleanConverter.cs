using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace StayOnTarget.Converters;

public class EnumToBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null || parameter == null) return false;

        return string.Equals(value.ToString(), parameter.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is true && parameter != null)
        {
            string? paramString = parameter.ToString();
            
            if (!string.IsNullOrEmpty(paramString) && Enum.IsDefined(targetType, paramString))
            {
                return Enum.Parse(targetType, paramString, ignoreCase: true);
            }
        }

        return DependencyProperty.UnsetValue;
    }
}