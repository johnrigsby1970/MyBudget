using System.Globalization;
using System.Windows;
using System.Windows.Data;
using StayOnTarget.Models;

namespace StayOnTarget.Converters;

public class PaycheckFieldVisibilityConverter : IMultiValueConverter {
    public object Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture) {
        // values[0] should be ToAccountId (int?)
        // values[1] should be the list of Accounts

        if (values.Length < 2 || values[0] is not int toAccountId ||
            values[1] is not System.Collections.IEnumerable accounts) {
            return Visibility.Collapsed;
        }

        // Find the account with the matching ToAccountId
        foreach (var item in accounts) {
            if (item is Account account && account.Id == toAccountId) {
                // Check if the account type is Checking or Savings
                if (account.Type == AccountType.Checking || account.Type == AccountType.Savings) {
                    return Visibility.Visible;
                }

                break;
            }
        }

        return Visibility.Collapsed;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) {
        throw new NotImplementedException();
    }
}
