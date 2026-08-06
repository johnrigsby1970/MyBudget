using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using StayOnTarget.Models;
using StayOnTarget.Services.Projections;

namespace StayOnTarget.Converters;

public class ProjectionAmountToBrushConverter: IValueConverter {
    public SolidColorBrush BucketBrush { get; set; } = Brushes.MediumPurple; // Soft Purple
    public SolidColorBrush RedBrush { get; set; } = Brushes.Red;
    public SolidColorBrush GreenBrush { get; set; } = Brushes.Green;
    public SolidColorBrush DefaultBrush { get; set; } = Brushes.Black;
    public SolidColorBrush SweepBrush { get; set; } = Brushes.Goldenrod;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ProjectionGridItem item)
        {
            // 1. Bucket/Envelope allocations take highest priority
            if (item.IsBucket)
            {
                return BucketBrush;
            }

            if (item.IsSweep) {
                return SweepBrush;
            }

            // 2. Standard Inflow/Outflow amounts
            if (item.Amount < 0) return RedBrush;
            if (item.Amount > 0) return DefaultBrush;
        }

        return DefaultBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}