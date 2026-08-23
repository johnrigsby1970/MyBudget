using System.Globalization;
using System.Windows;
using System.Windows.Data;
using StayOnTarget.Models;

namespace StayOnTarget.Converters;

public class MarkBillPaidVisibilityConverter : IValueConverter {
    public object Convert(object[]? values, Type targetType, object? parameter, CultureInfo culture) {
        if (values == null || values.Length < 2) return Visibility.Collapsed;

        // Check the IsPaid flag (assuming it's a bool)
        if (values[1] is bool isPaid && isPaid) {
            return Visibility.Collapsed; // Hide if it's paid, regardless of amount
        }

        // Fallback to your original logic based on TransactionAmount
        var amount = values[0];
        if (amount is not decimal d || d == 0) {
            return Visibility.Collapsed;
        }

        return Visibility.Visible;
    }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) {
        // Safely check if the value is actually a PeriodBill
        if (value is PeriodBill bill) {
            // Check the IsPaid flag
            if (bill.IsPaid) {
                return Visibility.Collapsed; // Hide if it's paid, regardless of amount
            }
//0 is a valid amount but we can only tell if its 0 because its entered that way or 0 because thats the default, by checking IsPaid
            // // Check TransactionAmount
            // var amount = bill.TransactionAmount;
            // if (amount == null || amount == 0)
            // {
            //     return Visibility.Collapsed;
            // }

            return Visibility.Visible;
        }

        // Fallback if the binding context isn't a PeriodBill yet (e.g., during initialization)
        return Visibility.Collapsed;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) {
        return Binding.DoNothing;
    }
}