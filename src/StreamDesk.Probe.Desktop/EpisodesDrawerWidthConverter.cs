using System.Globalization;
using System.Windows.Data;

namespace StreamDesk.Probe.Desktop;

public sealed class EpisodesDrawerWidthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not double width || !double.IsFinite(width) || width <= 0) return 0d;

        var minimum = width >= 900 ? 380d : 280d;
        return Math.Min(width, Math.Min(520d, Math.Max(minimum, width * 0.40d)));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}
