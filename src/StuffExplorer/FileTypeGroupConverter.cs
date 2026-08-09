using System.Globalization;
using System.Windows.Data;

namespace StuffExplorer;

public sealed class FileTypeGroupConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is string { Length: > 0 } extension ? extension : MainWindow.S("NoType");

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
