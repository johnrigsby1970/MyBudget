using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Data;

namespace StayOnTarget.Converters;

public class SetContainsAccountIdConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is HashSet<int> set && parameter is int accountId)
        {
            return set.Contains(accountId);
        }
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // Not used directly if handled via Command, but implemented for two-way convenience:
        throw new NotImplementedException("Use the ToggleExcludedAccountCommand on CheckBox click.");
    }
}