using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace StayOnTarget.Converters;

public class DeltaToBrushConverter : IValueConverter
{
    public Brush PositiveBrush { get; set; } = (Brush)new BrushConverter().ConvertFrom("#2E7D32")!;
    public Brush NegativeBrush { get; set; } = (Brush)new BrushConverter().ConvertFrom("#C62828")!;
    public Brush NeutralBrush { get; set; } = Brushes.Transparent; // Will be handled by style or binding if neutral

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is decimal d)
        {
            if (d > 0) return PositiveBrush;
            if (d < 0) return NegativeBrush;
        }
        else if (value is double db)
        {
            if (db > 0) return PositiveBrush;
            if (db < 0) return NegativeBrush;
        }
        else if (value is int i)
        {
            if (i > 0) return PositiveBrush;
            if (i < 0) return NegativeBrush;
        }
        else if (value is long l)
        {
            if (l > 0) return PositiveBrush;
            if (l < 0) return NegativeBrush;
        }

        return Binding.DoNothing;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
