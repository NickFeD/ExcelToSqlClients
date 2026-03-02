using System.Globalization;
using System.Windows.Data;

namespace ExcelToSqlClients.Dynamic.Desktop.WPF.Converters;

public sealed class BoolInvertConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b ? !b : true;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b ? !b : false;
}
