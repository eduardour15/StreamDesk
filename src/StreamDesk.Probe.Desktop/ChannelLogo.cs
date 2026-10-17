using System.Windows;
using System.Windows.Controls;

namespace StreamDesk.Probe.Desktop;

public sealed class ChannelLogo : Image
{
    public static readonly DependencyProperty UrlProperty = DependencyProperty.Register(nameof(Url), typeof(string), typeof(ChannelLogo), new PropertyMetadata(null, Changed));
    public static readonly DependencyProperty LoaderProperty = DependencyProperty.Register(nameof(Loader), typeof(ChannelLogoLoader), typeof(ChannelLogo), new PropertyMetadata(null, Changed));
    private CancellationTokenSource? _load;
    public string? Url { get => (string?)GetValue(UrlProperty); set => SetValue(UrlProperty, value); }
    public ChannelLogoLoader? Loader { get => (ChannelLogoLoader?)GetValue(LoaderProperty); set => SetValue(LoaderProperty, value); }
    public ChannelLogo()
    {
        Loaded += (_, _) => Reload();
        Unloaded += (_, _) => { _load?.Cancel(); Source = null; };
    }
    private static void Changed(DependencyObject sender, DependencyPropertyChangedEventArgs e) => ((ChannelLogo)sender).Reload();
    private async void Reload()
    {
        _load?.Cancel(); Source = null;
        if (!IsLoaded || Loader is null || string.IsNullOrEmpty(Url)) return;
        using var load = new CancellationTokenSource(); _load = load;
        try
        {
            var image = await Loader.GetAsync(Url, load.Token);
            if (ReferenceEquals(_load, load) && IsLoaded) Source = image;
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { } // The shared loader may close while a row is unloading.
        finally { if (ReferenceEquals(_load, load)) _load = null; }
    }
}
