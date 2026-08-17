using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using System.Windows.Data;

namespace StayOnTarget.Converters;

public class EnumToDisplayNameConverter : IValueConverter {
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) {
        if (value == null) return string.Empty;

        string? valueName = value.ToString();
        if (string.IsNullOrEmpty(valueName)) return string.Empty;

        // Fetch the enum field member safely
        var memberInfo = value.GetType().GetMember(valueName).FirstOrDefault();
        if (memberInfo == null) return valueName;

        // Extract the [Display(Name = "...")] attribute
        var displayAttribute = memberInfo.GetCustomAttribute<DisplayAttribute>();

        return displayAttribute?.GetName() ?? valueName;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) {
        throw new NotImplementedException("One-way conversion only.");
    }
}