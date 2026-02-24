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
