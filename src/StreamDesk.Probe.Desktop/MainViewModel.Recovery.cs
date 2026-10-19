using StreamDesk.Application;

namespace StreamDesk.Probe.Desktop;

public sealed partial class MainViewModel
{
    private readonly IRecoveryService? _recovery;
    public async Task CreateBackupAsync(bool diagnostics = false)
    {
        if (_disposed || IsBusy || _recovery is null) return;
        SetProfileBusy(true);
        try
        {
            var location = diagnostics ? await _recovery.ExportDiagnosticsAsync(_shutdown.Token) : await _recovery.CreateBackupAsync(_shutdown.Token);
            ProfileStatus = diagnostics ? $"Informe guardado en: {location}" : $"Copia guardada en: {location}";
        }
        catch (OperationCanceledException) { }
        catch (Exception error) { _logger.LogError("local_recovery", error); ProfileStatus = "No se pudo crear la copia."; }
        finally { SetProfileBusy(false); }
    }
}
