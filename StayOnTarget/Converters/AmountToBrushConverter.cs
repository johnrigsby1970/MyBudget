using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace StayOnTarget.Converters
{
    public class AmountToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal amount)
            {
                // If negative, use the themed Error/Danger text brush; otherwise fallback to primary text
                string resourceKey = amount < 0 ? "ButtonDangerBackgroundBrush" : "PrimaryTextBrush";

                if (Application.Current.TryFindResource(resourceKey) is Brush brush)
                {
                    return brush;
                }
            }

            // Default fallback using the application's primary text brush
            return Application.Current.TryFindResource("PrimaryTextBrush") as Brush ?? Brushes.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}