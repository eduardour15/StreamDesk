using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace StreamDesk.Probe.Desktop;

public sealed class FavoriteConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) =>
        values.Length == 2 && values[0] is string id && values[1] is IReadOnlySet<string> favorites && favorites.Contains(id);
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        targetTypes.Select(_ => DependencyProperty.UnsetValue).ToArray();
}
