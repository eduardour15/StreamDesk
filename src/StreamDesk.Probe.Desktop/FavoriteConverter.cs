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

public sealed class MediaFavoriteConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 4 || values[0] is not string id || values[1] is not StreamDesk.Probe.Core.ContentKind kind) return false;
        return kind switch
        {
            StreamDesk.Probe.Core.ContentKind.Movie when values[2] is IReadOnlySet<string> movies => movies.Contains(id),
            StreamDesk.Probe.Core.ContentKind.Series when values[3] is IReadOnlySet<string> series => series.Contains(id),
            _ => false
        };
    }

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

public sealed class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is not Visibility.Visible;
}

public sealed class ChannelSecondaryInfoConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var channelId = values.Length > 0 ? values[0] as string : null;
        var categoryId = values.Length > 1 ? values[1] as string : null;
        var categories = values.Length > 2 ? values[2] as IReadOnlyDictionary<string, string> : null;
        var playingId = values.Length > 3 ? values[3] as string : null;
        var epgNow = values.Length > 4 ? values[4] as string : null;

        string? program = null;
        if (!string.IsNullOrEmpty(channelId) && string.Equals(channelId, playingId, StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(epgNow) &&
            !epgNow.StartsWith("Sin ", StringComparison.Ordinal) &&
            !epgNow.StartsWith("La guía", StringComparison.Ordinal) &&
            !epgNow.StartsWith("Cargando", StringComparison.Ordinal))
        {
            program = epgNow;
        }

        string? categoryName = null;
        if (!string.IsNullOrWhiteSpace(categoryId) && categories is not null && categories.TryGetValue(categoryId, out var resolved) && !string.IsNullOrWhiteSpace(resolved))
        {
            categoryName = resolved;
        }

        var text = !string.IsNullOrWhiteSpace(program) ? program! : (categoryName ?? string.Empty);
        if (targetType == typeof(Visibility))
            return string.IsNullOrWhiteSpace(text) ? Visibility.Collapsed : Visibility.Visible;
        return text;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        targetTypes.Select(_ => DependencyProperty.UnsetValue).ToArray();
}

public sealed class MediaProgressValueConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var hasProgress = values.Length >= 2 &&
                          values[0] is string key &&
                          !string.IsNullOrEmpty(key) &&
                          values[1] is IReadOnlyDictionary<string, double> map &&
                          map.TryGetValue(key, out var progress) &&
                          progress > 0;
        if (targetType == typeof(Visibility))
            return hasProgress ? Visibility.Visible : Visibility.Collapsed;
        return hasProgress && values[1] is IReadOnlyDictionary<string, double> dict && values[0] is string k && dict.TryGetValue(k, out var val)
            ? val
            : 0d;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        targetTypes.Select(_ => DependencyProperty.UnsetValue).ToArray();
}
