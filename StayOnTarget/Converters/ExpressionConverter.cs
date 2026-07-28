using System.Windows.Data;
using Serilog;

namespace StayOnTarget.Converters;

public class ExpressionConverter<TIn, TOut> : IValueConverter {
    private readonly Func<TIn, TOut?> _expression;
    public ExpressionConverter(Func<TIn, TOut?> expression) => _expression = expression;

    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) {
        try {
            if (value is TIn typedValue) return _expression(typedValue);
        }
        catch (Exception ex) {
            Log.Error(ex, "Error in ExpressionConverter for type {InType} to {OutType}.", typeof(TIn).Name,
                typeof(TOut).Name);
        }

        return default(TOut);
    }

    public object?
        ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
        throw new NotImplementedException();
}
