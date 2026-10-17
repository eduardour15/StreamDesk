using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace StreamDesk.Probe.Desktop;

public partial class App : System.Windows.Application
{
    private AppServices? _services;

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        base.OnExit(e);
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StreamDesk");
        DispatcherUnhandledException += (_, eventArgs) =>
        {
            _services?.Logger.LogError("ui_unhandled", eventArgs.Exception);
            eventArgs.Handled = true;
            MessageBox.Show("Ocurrió un error recuperable. Se registró un código técnico sin datos de la cuenta.", "StreamDesk", MessageBoxButton.OK, MessageBoxImage.Warning);
        };
        TaskScheduler.UnobservedTaskException += (_, eventArgs) =>
        {
            _services?.Logger.LogError("task_unobserved", eventArgs.Exception);
            eventArgs.SetObserved();
        };

        try
        {
            _services = new AppServices(directory);
            await _services.Settings.InitializeAsync();
            var window = new MainWindow(_services);
            MainWindow = window;
            window.Show();
        }
        catch (Exception exception)
        {
            _services?.Logger.LogError("startup_failed", exception);
            MessageBox.Show("StreamDesk no pudo preparar sus datos locales. Revisa los permisos de tu carpeta de usuario.", "StreamDesk", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(-1);
        }
    }
}
