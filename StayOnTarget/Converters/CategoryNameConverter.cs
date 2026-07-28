using System.Globalization;
using System.Windows.Data;
using StayOnTarget.Models;

namespace StayOnTarget.Converters;

public class CategoryNameConverter : IValueConverter {
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) {
        // 'value' will be the entire row data object (your ViewModel or Model)
        if (value is null) return string.Empty;

        // Replace 'YourRowDataType' with the actual class name of your items
        var rowData = (Transaction)value;

        if (rowData.BillId != null) {
            return $"Bill: {rowData.BillName}";
        }

        return rowData.BucketName??"";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) {
        throw new NotImplementedException();
    }
}
