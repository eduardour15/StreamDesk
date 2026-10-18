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
        if (_disposed || channel is null) { EpgStatus = "Selecciona un canal para consultar su guía."; return; }
        _epgRequest?.Cancel(); using var request = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token, _session.Token); _epgRequest = request;
        var version = _sessionVersion; var profile = _catalogOwnerId;
        try
        {
            EpgStatus = "Consultando guía…";
            var cached = !force && _contentStore is not null && profile is not null ? await _contentStore.LoadEpgAsync(profile, channel.Id, request.Token) : null;
            var programs = cached;
            if (programs is null)
            {
                if (_client is null && CanConnect) await ConnectProfileAsync();
                request.Token.ThrowIfCancellationRequested();
                if (_client is not IContentProvider provider) { EpgStatus = "No hay guía disponible en este adaptador."; return; }
                programs = await provider.GetEpgAsync(channel.Id, request.Token);
                if (profile is not null && _contentStore is not null) await _contentStore.SaveEpgAsync(profile, channel.Id, programs, request.Token);
            }
            request.Token.ThrowIfCancellationRequested();
            if (version != _sessionVersion || (_watch?.Channel ?? SelectedChannel)?.Id != channel.Id) return;
            _epgPrograms = programs; UpdateEpgLabels();
            EpgStatus = programs.Count == 0 ? "El proveedor no devolvió programación con horarios verificables." : "Guía local actualizada; horarios en la zona de Windows.";
        }
        catch (OperationCanceledException) { }
        catch (Exception error) { _logger.LogError("epg_load", error); if (version == _sessionVersion) EpgStatus = "No se pudo actualizar la guía. Puedes reintentar o importar XMLTV."; }
        finally { if (ReferenceEquals(_epgRequest, request)) _epgRequest = null; }
    }
    public async Task ImportEpgAsync(Stream input)
    {
        var channel = _watch?.Channel ?? SelectedChannel; var version = _sessionVersion;
        if (_epgImporter is null || string.IsNullOrWhiteSpace(channel?.EpgChannelId)) { EpgStatus = "El canal no tiene un ID EPG para asociar XMLTV."; return; }
        try
        {
            var token = _session.Token;
            var programs = await Task.Run(() => _epgImporter.ReadAsync(input, channel.EpgChannelId, token), token);
            if (version != _sessionVersion || _disposed) return;
            if (_catalogOwnerId is { } profile && _contentStore is not null) await _contentStore.SaveEpgAsync(profile, channel.Id, programs, token);
            if (version != _sessionVersion) return;
            _epgPrograms = programs; UpdateEpgLabels(); EpgStatus = programs.Count == 0 ? "XMLTV no contiene programación válida para este ID." : "Guía XMLTV importada para el canal seleccionado.";
        }
        catch (OperationCanceledException) { }
        catch (Exception error) { _logger.LogError("epg_import", error); if (version == _sessionVersion) EpgStatus = "XMLTV inválido, demasiado grande o con entidades no permitidas."; }
    }
    public void UpdateEpgLabels()
    {
        var now = DateTimeOffset.UtcNow;
        EpgNow = _epgPrograms.LastOrDefault(item => item.StartUtc <= now && item.EndUtc > now)?.Label ?? "Sin programación actual.";
        EpgNext = _epgPrograms.FirstOrDefault(item => item.StartUtc > now)?.Label ?? "Sin programación siguiente.";
        OnPropertyChanged(nameof(EpgNow)); OnPropertyChanged(nameof(EpgNext));
    }
}
