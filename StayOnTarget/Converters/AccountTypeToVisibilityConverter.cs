using System.Globalization;
using System.Windows;
using System.Windows.Data;
using StayOnTarget.Models;

namespace StayOnTarget.Converters;

public class AccountTypeToVisibilityConverter : IValueConverter {
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) {
        AccountType? accountType = null;
        if (value is AccountType t) accountType = t;
        else if (value is Account account) accountType = account.Type;

        if (accountType.HasValue && parameter is string targetTypeStr) {
            string actualTypeStr = accountType.Value.ToString();
            if (targetTypeStr.Contains("|")) {
                var targets = targetTypeStr.Split('|');
                return targets.Contains(actualTypeStr) ? Visibility.Visible : Visibility.Collapsed;
            }

            return actualTypeStr == targetTypeStr ? Visibility.Visible : Visibility.Collapsed;
        }

        return Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) {
        throw new NotImplementedException();
    }
}
