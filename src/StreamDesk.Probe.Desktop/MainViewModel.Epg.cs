using StreamDesk.Application;
using StreamDesk.Probe.Core;
using System.IO;

namespace StreamDesk.Probe.Desktop;

public sealed partial class MainViewModel
{
    private readonly IEpgImporter? _epgImporter;
    private IReadOnlyList<EpgProgram> _epgPrograms = [];
    public async Task RefreshEpgAsync(bool force = false)
    {
        var channel = _watch?.Channel ?? SelectedChannel;
        if (_disposed || channel is null) { EpgStatus = "Elige un canal para ver su programación."; return; }
        _epgRequest?.Cancel(); using var request = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token, _session.Token); _epgRequest = request;
        var version = _sessionVersion; var profile = _catalogOwnerId;
        try
        {
            EpgStatus = "Cargando guía…";
            var cached = !force && _contentStore is not null && profile is not null ? await _contentStore.LoadEpgAsync(profile, channel.Id, request.Token) : null;
            var programs = cached;
            if (programs is null)
            {
                if (_client is null && CanConnect) await ConnectProfileAsync();
                request.Token.ThrowIfCancellationRequested();
                if (_client is not IContentProvider provider) { EpgStatus = "Guía no disponible para esta cuenta."; return; }
                programs = await provider.GetEpgAsync(channel.Id, request.Token);
                if (profile is not null && _contentStore is not null) await _contentStore.SaveEpgAsync(profile, channel.Id, programs, request.Token);
            }
            request.Token.ThrowIfCancellationRequested();
            if (version != _sessionVersion || (_watch?.Channel ?? SelectedChannel)?.Id != channel.Id) return;
            _epgPrograms = programs; UpdateEpgLabels();
            EpgStatus = programs.Count == 0 ? "Sin guía disponible para este canal." : "Guía actualizada (hora local).";
        }
        catch (OperationCanceledException) { }
        catch (Exception error) { _logger.LogError("epg_load", error); if (version == _sessionVersion) EpgStatus = "No se pudo cargar la guía."; }
        finally { if (ReferenceEquals(_epgRequest, request)) _epgRequest = null; }
    }
    public async Task ImportEpgAsync(Stream input)
    {
        var channel = _watch?.Channel ?? SelectedChannel; var version = _sessionVersion;
        if (_epgImporter is null || string.IsNullOrWhiteSpace(channel?.EpgChannelId)) { EpgStatus = "Este canal no admite guía externa."; return; }
        try
        {
            var token = _session.Token;
            var programs = await Task.Run(() => _epgImporter.ReadAsync(input, channel.EpgChannelId, token), token);
            if (version != _sessionVersion || _disposed) return;
            if (_catalogOwnerId is { } profile && _contentStore is not null) await _contentStore.SaveEpgAsync(profile, channel.Id, programs, token);
            if (version != _sessionVersion) return;
            _epgPrograms = programs; UpdateEpgLabels(); EpgStatus = programs.Count == 0 ? "El archivo no tiene datos para este canal." : "Guía importada.";
        }
        catch (OperationCanceledException) { }
        catch (Exception error) { _logger.LogError("epg_import", error); if (version == _sessionVersion) EpgStatus = "El archivo de guía no es válido."; }
    }
    public void UpdateEpgLabels()
    {
        var now = DateTimeOffset.UtcNow;
        EpgNow = _epgPrograms.LastOrDefault(item => item.StartUtc <= now && item.EndUtc > now)?.Label ?? "Sin programación actual.";
        EpgNext = _epgPrograms.FirstOrDefault(item => item.StartUtc > now)?.Label ?? "Sin programación siguiente.";
        OnPropertyChanged(nameof(EpgNow)); OnPropertyChanged(nameof(EpgNext));
    }
}
