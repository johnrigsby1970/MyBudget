using System.Globalization;
using System.Windows.Data;

namespace MyBudget;

public class NegativeToRedConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is decimal d)
        {
            return d < 0;
        }
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not null)
        {
            return System.Windows.Visibility.Visible;
        }
        return System.Windows.Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            return b ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
        }
        return System.Windows.Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is System.Windows.Visibility v)
        {
            return v != System.Windows.Visibility.Visible;
        }
        return false;
    }
}

public class AccountTypeToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        Models.AccountType? accountType = null;
        if (value is Models.AccountType t) accountType = t;
        else if (value is Models.Account account) accountType = account.Type;

        if (accountType.HasValue && parameter is string targetTypeStr)
        {
            string actualTypeStr = accountType.Value.ToString();
            if (targetTypeStr.Contains("|"))
            {
                var targets = targetTypeStr.Split('|');
                return targets.Contains(actualTypeStr) ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            }
            return actualTypeStr == targetTypeStr ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        }
        return System.Windows.Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class PaycheckFieldVisibilityConverter : IMultiValueConverter
{
    public object Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        // values[0] should be ToAccountId (int?)
        // values[1] should be the list of Accounts

        if (values.Length < 2 || values[0] is not int toAccountId || values[1] is not System.Collections.IEnumerable accounts)
        {
            return System.Windows.Visibility.Collapsed;
        }

        // Find the account with the matching ToAccountId
        foreach (var item in accounts)
        {
            if (item is Models.Account account && account.Id == toAccountId)
            {
                // Check if account type is Checking or Savings
                if (account.Type == Models.AccountType.Checking || account.Type == Models.AccountType.Savings)
                {
                    return System.Windows.Visibility.Visible;
                }
                break;
            }
        }

        return System.Windows.Visibility.Collapsed;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
