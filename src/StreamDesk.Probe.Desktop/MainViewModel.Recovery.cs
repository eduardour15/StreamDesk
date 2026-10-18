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
            ProfileStatus = diagnostics ? $"Diagnóstico sin datos de cuenta: {location}" : $"Copia protegida para este usuario de Windows: {location}";
        }
        catch (OperationCanceledException) { }
        catch (Exception error) { _logger.LogError("local_recovery", error); ProfileStatus = "No se pudo completar la copia local. No se ha reemplazado tu configuración."; }
        finally { SetProfileBusy(false); }
    }
}
