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

public sealed class EqualityConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var equal = values.Length == 2 && values[0] is string a && values[1] is string b &&
                    !string.IsNullOrEmpty(a) && string.Equals(a, b, StringComparison.Ordinal);
        return targetType == typeof(Visibility) ? (equal ? Visibility.Visible : Visibility.Collapsed) : equal;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        targetTypes.Select(_ => DependencyProperty.UnsetValue).ToArray();
}

public sealed class EpgSubtitleConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var id = values.Length > 0 ? values[0] as string : null;
        var epgKey = values.Length > 1 ? values[1] as string : null;
        var dial = !string.IsNullOrWhiteSpace(id) && int.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var num) && num > 0
            ? $"Dial {num}"
            : null;

        string? programTitle = null;
        if (values.Length > 2 && values[2] is IReadOnlyDictionary<string, IReadOnlyList<StreamDesk.Probe.Core.EpgProgram>> map)
        {
            var key = !string.IsNullOrWhiteSpace(epgKey) ? epgKey : id;
            if (!string.IsNullOrWhiteSpace(key) && map.TryGetValue(key, out var list) && list.Count > 0)
            {
                var now = DateTimeOffset.UtcNow;
                var current = list.FirstOrDefault(p => p.StartUtc <= now && now < p.EndUtc) ?? list[0];
                programTitle = current.Title;
            }
        }
        else if (values.Length > 3 && values[2] is string playingId &&
                 !string.IsNullOrEmpty(id) && string.Equals(id, playingId, StringComparison.Ordinal) &&
                 values[3] is string epgNow && !string.IsNullOrWhiteSpace(epgNow) &&
                 !epgNow.StartsWith("Sin ", StringComparison.Ordinal) &&
                 !epgNow.StartsWith("La guía", StringComparison.Ordinal))
        {
            programTitle = epgNow;
        }

        if (!string.IsNullOrWhiteSpace(dial) && !string.IsNullOrWhiteSpace(programTitle))
            return $"{dial} · {programTitle}";
        if (!string.IsNullOrWhiteSpace(programTitle))
            return programTitle!;
        if (!string.IsNullOrWhiteSpace(dial))
            return $"{dial} · Emisión en directo";
        return "Emisión en directo";
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        targetTypes.Select(_ => DependencyProperty.UnsetValue).ToArray();
}
