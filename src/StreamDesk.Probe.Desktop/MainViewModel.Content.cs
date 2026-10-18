using StreamDesk.Application;
using StreamDesk.Probe.Core;

namespace StreamDesk.Probe.Desktop;

public sealed partial class MainViewModel
{
    private readonly IContentStore? _contentStore;
    private MediaCatalog? _movies, _series, _episodes;
    private MediaItem? _selectedMedia, _selectedEpisode, _selectedDetails;
    private XtreamCategory? _mediaCategory, _season;
    private string _mediaSearch = "", _contentStatus = "Consulta el contenido disponible de tu cuenta.", _epgStatus = "Selecciona un canal para consultar su guía.";
    private bool _contentBusy;
    private CancellationTokenSource? _mediaFilter, _detailRequest, _epgRequest;
    private Task _progressWrites = Task.CompletedTask;
    private readonly SemaphoreSlim _mediaCapture = new(1, 1);
    private MediaWatch? _mediaWatch;
    private IReadOnlyList<MediaProgress> _progress = [];
    private MediaPlaybackDetails _mediaDetails = MediaPlaybackDetails.Empty;
    public bool ShowLiveCatalog => CurrentTabIndex is >= 1 and <= 3;
    public bool ShowMediaCatalog => CurrentTabIndex is 5 or 6;
    public bool ShowContinueWatching => CurrentTabIndex == 7;
    public bool ShowEpisodePicker => SelectedMedia?.Kind == ContentKind.Series;
    public string VersionLabel => typeof(MainViewModel).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
        .OfType<System.Reflection.AssemblyInformationalVersionAttribute>().FirstOrDefault()?.InformationalVersion.Split('+')[0] ?? "Candidato";
    public string HeaderSummary => ShowLiveCatalog ? CatalogSummary : ShowContinueWatching ? $"{ContinueWatching.Count} contenidos en curso" : $"{MediaItems.Count} títulos disponibles";
    public bool HasMovies => _movies?.Items.Count > 0;
    public bool HasSeries => _series?.Items.Count > 0;
    public bool ContentBusy { get => _contentBusy; private set { if (SetProperty(ref _contentBusy, value)) OnPropertyChanged(nameof(IsWorking)); } }
    public string ContentStatus { get => _contentStatus; private set => SetProperty(ref _contentStatus, value); }
    public string EpgStatus { get => _epgStatus; private set => SetProperty(ref _epgStatus, value); }
    public string EpgNow { get; private set; } = "Sin programación actual.";
    public string EpgNext { get; private set; } = "Sin programación siguiente.";
    public IReadOnlyList<MediaItem> MediaItems { get; private set; } = [];
    public IReadOnlyList<XtreamCategory> MediaCategories => new[] { new XtreamCategory("", "Todas las categorías") }.Concat((CurrentTabIndex == 6 ? _series : _movies)?.Categories ?? []).ToArray();
    public IReadOnlyList<XtreamCategory> Seasons => _episodes?.Categories ?? [];
    public IReadOnlyList<MediaItem> Episodes => (_episodes?.Items ?? []).Where(item => Season is null || item.CategoryId == Season.Id).ToArray();
    public IReadOnlyList<MediaProgress> ContinueWatching => _progress.Where(item => !item.Completed && item.PositionMilliseconds > 0).ToArray();
    public MediaProgress? SelectedProgress { get; set; }
    public MediaItem? SelectedMedia
    {
        get => _selectedMedia;
        set { if (SetProperty(ref _selectedMedia, value)) { _selectedDetails = null; OnPropertyChanged(nameof(MediaDescription)); OnPropertyChanged(nameof(ShowEpisodePicker)); _ = LoadMediaDetailsAsync(value); } }
    }
    public MediaItem? SelectedEpisode { get => _selectedEpisode; set => SetProperty(ref _selectedEpisode, value); }
    public XtreamCategory? MediaCategory { get => _mediaCategory ?? MediaCategories.FirstOrDefault(); set { if (SetProperty(ref _mediaCategory, value)) _ = FilterMediaAsync(); } }
    public XtreamCategory? Season { get => _season; set { if (SetProperty(ref _season, value)) { SelectedEpisode = null; OnPropertyChanged(nameof(Episodes)); } } }
    public string MediaSearch { get => _mediaSearch; set { if (SetProperty(ref _mediaSearch, value)) _ = FilterMediaAsync(true); } }
    public string MediaDescription => (_selectedDetails ?? SelectedMedia)?.Description ?? "Selecciona una película o serie para ver su ficha.";
    public IReadOnlyList<MediaTrack> AudioTracks => _mediaDetails.AudioTracks;
    public IReadOnlyList<MediaTrack> SubtitleTracks => _mediaDetails.SubtitleTracks;
    public MediaTrack? SelectedAudioTrack { get; set; }
    public MediaTrack? SelectedSubtitleTrack { get; set; }
    public bool CanSeek => _mediaWatch is not null && _mediaDetails.CanSeek && _mediaDetails.LengthMilliseconds > 0;
    public double DurationSeconds => _mediaDetails.LengthMilliseconds / 1000d;
    public double PositionSeconds => _mediaDetails.TimeMilliseconds / 1000d;
    public string PositionLabel => $"{TimeSpan.FromMilliseconds(_mediaDetails.TimeMilliseconds):hh\\:mm\\:ss} / {TimeSpan.FromMilliseconds(_mediaDetails.LengthMilliseconds):hh\\:mm\\:ss}";

    public async Task ProbeContentAsync()
    {
        if (_disposed || ContentBusy || !CanConnect) return;
        var version = _sessionVersion;
        ContentBusy = true;
        CancellationTokenSource? operation = null;
        var failed = false;
        try
        {
            if (_client is null) await ConnectProfileAsync();
            if (version != _sessionVersion || _disposed) return;
            if (_client is not IContentProvider provider) { ContentStatus = "Este adaptador no ofrece películas o series."; return; }
            operation = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token, _session.Token);
            _operation = operation;
            foreach (var kind in new[] { ContentKind.Movie, ContentKind.Series })
            {
                try
                {
                    ContentStatus = kind == ContentKind.Movie ? "Consultando películas…" : "Consultando series…";
                    var catalog = await provider.GetMediaCatalogAsync(kind, operation.Token);
                    operation.Token.ThrowIfCancellationRequested(); if (version != _sessionVersion) return;
                    if (_connectedProfileId is { } id && _contentStore is not null) await _contentStore.SaveCatalogAsync(id, kind, "", catalog, operation.Token);
                    if (version != _sessionVersion) return;
                    if (kind == ContentKind.Movie) _movies = catalog; else _series = catalog;
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception error) { failed = true; _logger.LogError("content_catalog", error); }
            }
            if (version != _sessionVersion || _disposed) return;
            ContentStatus = $"Películas: {_movies?.Items.Count ?? 0} · Series: {_series?.Items.Count ?? 0}. " + (failed ? "Algunas consultas fallaron; se conserva la caché conocida. Puedes reintentar." : "La disponibilidad depende de la cuenta.");
            NotifyContent(); await FilterMediaAsync();
        }
        catch (OperationCanceledException) { if (version == _sessionVersion) ContentStatus = "Consulta de contenido cancelada."; }
        catch (Exception error) { _logger.LogError("content_probe", error); if (version == _sessionVersion) ContentStatus = "No se pudo consultar el contenido. Reintenta cuando haya conexión."; }
        finally { if (ReferenceEquals(_operation, operation)) _operation = null; operation?.Dispose(); ContentBusy = false; }
    }

    private async Task LoadContentCacheAsync(string? profile, int version, CancellationToken token)
    {
        if (profile is null || _contentStore is null) return;
        var movies = await _contentStore.LoadCatalogAsync(profile, ContentKind.Movie, cancellationToken: token);
        var series = await _contentStore.LoadCatalogAsync(profile, ContentKind.Series, cancellationToken: token);
        var progress = await _contentStore.LoadProgressAsync(profile, token);
        if (version != _sessionVersion || _disposed) return;
        _movies = movies; _series = series; _progress = progress; NotifyContent(); await FilterMediaAsync();
    }

    private async Task FilterMediaAsync(bool debounce = false)
    {
        if (_disposed) return;
        _mediaFilter?.Cancel(); using var request = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token, _session.Token); _mediaFilter = request;
        var catalog = CurrentTabIndex == 6 ? _series : _movies; var query = CatalogSearch.Normalize(MediaSearch); var category = MediaCategory?.Id;
        try
        {
            if (debounce) await Task.Delay(180, request.Token);
            var matches = await Task.Run(() => (catalog?.Items ?? []).Where(item =>
            {
                request.Token.ThrowIfCancellationRequested();
                return (string.IsNullOrEmpty(category) || item.CategoryId == category) && CatalogSearch.Normalize(item.Name).Contains(query, StringComparison.Ordinal);
            }).ToArray(), request.Token);
            if (!ReferenceEquals(_mediaFilter, request) || _disposed) return;
            MediaItems = matches; OnPropertyChanged(nameof(MediaItems)); OnPropertyChanged(nameof(HeaderSummary));
        }
        catch (OperationCanceledException) { }
        finally { if (ReferenceEquals(_mediaFilter, request)) _mediaFilter = null; }
    }

    private async Task LoadMediaDetailsAsync(MediaItem? item)
    {
        _detailRequest?.Cancel(); _episodes = null; Season = null; SelectedEpisode = null; OnPropertyChanged(nameof(Seasons)); OnPropertyChanged(nameof(Episodes));
        if (item is null || _disposed) return;
        using var request = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token, _session.Token); _detailRequest = request;
        var version = _sessionVersion;
        try
        {
            if (_client is null) await ConnectProfileAsync();
            request.Token.ThrowIfCancellationRequested();
            if (_client is not IContentProvider provider || version != _sessionVersion) return;
            if (item.Kind == ContentKind.Movie)
            {
                var details = await provider.GetMovieInfoAsync(item, request.Token);
                if (!ReferenceEquals(_selectedMedia, item) || version != _sessionVersion) return;
                _selectedDetails = details; OnPropertyChanged(nameof(MediaDescription));
            }
            else
            {
                var cached = _contentStore is not null && _connectedProfileId is { } profile ? await _contentStore.LoadCatalogAsync(profile, ContentKind.Episode, item.Id, request.Token) : null;
                var episodes = cached ?? await provider.GetEpisodesAsync(item.Id, request.Token);
                request.Token.ThrowIfCancellationRequested(); if (!ReferenceEquals(_selectedMedia, item) || version != _sessionVersion) return;
                if (cached is null && _contentStore is not null && _connectedProfileId is { } id) await _contentStore.SaveCatalogAsync(id, ContentKind.Episode, item.Id, episodes, request.Token);
                if (version != _sessionVersion) return;
                _episodes = episodes; Season = Seasons.FirstOrDefault(); OnPropertyChanged(nameof(Seasons)); OnPropertyChanged(nameof(Episodes));
                ContentStatus = episodes.Items.Count == 0 ? "La serie no contiene episodios disponibles." : $"{episodes.Items.Count} episodios disponibles. Elige temporada y episodio.";
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception error) { _logger.LogError("content_details", error); if (version == _sessionVersion) ContentStatus = "Ficha no disponible. Puedes reintentar seleccionando el contenido."; }
        finally { if (ReferenceEquals(_detailRequest, request)) _detailRequest = null; }
    }

    public Task PlayMediaAsync(bool restart = false) => PlayMediaItemAsync(ShowContinueWatching ? SelectedProgress?.Item : SelectedMedia?.Kind == ContentKind.Series ? SelectedEpisode : _selectedDetails ?? SelectedMedia, restart);
    private async Task PlayMediaItemAsync(MediaItem? item, bool restart = false)
    {
        if (item is null || _disposed || IsBusy || IsCatalogLoading) { if (item is null) ContentStatus = "Selecciona una película o episodio."; return; }
        var version = _sessionVersion;
        try
        {
            await CaptureMediaAsync(true);
            if (_client is null) await ConnectProfileAsync();
            if (_disposed || version != _sessionVersion || _client is not IContentProvider provider || _account is null) return;
            if (_account.ExpiresAt <= DateTimeOffset.UtcNow) throw new PlaybackException(PlaybackFailure.Expired);
            if (!_playbackService.HasActiveMedia && _account.HasReachedConnectionLimit) throw new PlaybackException(PlaybackFailure.ConnectionLimit);
            if (!MediaMetadata.SafeExtension(item.Extension) && item.Kind == ContentKind.Movie) item = await provider.GetMovieInfoAsync(item, _session.Token);
            var uri = provider.CreateMediaUri(item);
            if (version != _sessionVersion || _disposed) return;
            var resume = _progress.FirstOrDefault(entry => entry.Item.Kind == item.Kind && entry.Item.StorageKey == item.StorageKey);
            var position = restart || resume?.Completed != false ? 0 : resume.PositionMilliseconds;
            _watch = null; _playingChannelId = null;
            _mediaDetails = MediaPlaybackDetails.Empty; NotifyMediaDetails();
            _mediaWatch = new(item, _connectedProfileId, version, _playbackService.Snapshot.Sequence) { Position = position, Length = resume?.DurationMilliseconds ?? 0 };
            _playbackService.Options = _playbackService.Options with { OnDemand = true, StartPositionMilliseconds = position };
            OnPropertyChanged(nameof(NowPlaying));
            await _playbackService.PlayAsync(uri, _session.Token);
            OnPropertyChanged(nameof(PlayerHandle));
        }
        catch (OperationCanceledException) { }
        catch (Exception error) { _logger.LogError("content_play", error); ContentStatus = error is PlaybackException playback ? playback.Message : "No se pudo iniciar el contenido. Revisa permisos y formato."; }
    }

    public async Task NavigateEpisodeAsync(int direction)
    {
        var items = _episodes?.Items ?? []; var key = _mediaWatch?.Item.StorageKey ?? SelectedEpisode?.StorageKey;
        var index = items.ToList().FindIndex(item => item.StorageKey == key); var next = index + direction;
        if (direction is not (-1 or 1) || index < 0 || next < 0 || next >= items.Count) return;
        SelectedEpisode = items[next]; await PlayMediaItemAsync(items[next]);
    }

    public async Task CaptureMediaAsync(bool persist = false)
    {
        var watch = _mediaWatch;
        if (watch is null) return;
        await _mediaCapture.WaitAsync();
        try
        {
            if (!ReferenceEquals(watch, _mediaWatch)) return;
            var details = await _playbackService.ReadDetailsAsync();
            if (!ReferenceEquals(watch, _mediaWatch)) return;
            if (details.LengthMilliseconds > 0) { watch.Position = details.TimeMilliseconds; watch.Length = details.LengthMilliseconds; }
            if (!_disposed) { _mediaDetails = details; NotifyMediaDetails(); }
            if (watch.Played && (persist || DateTimeOffset.UtcNow - watch.LastSaved > TimeSpan.FromSeconds(15)))
            { watch.LastSaved = DateTimeOffset.UtcNow; _progressWrites = SaveMediaProgressAsync(_progressWrites, watch); await _progressWrites; }
        }
        catch (Exception error) { _logger.LogError("content_position", error); }
        finally { _mediaCapture.Release(); }
    }
    public async Task RefreshPlaybackDetailsAsync()
    {
        UpdateEpgLabels();
        if (_disposed || !_playbackService.HasActiveMedia) return;
        if (_mediaWatch is not null) { await CaptureMediaAsync(); return; }
        var version = _sessionVersion; var channel = _playingChannelId;
        try
        {
            var details = await _playbackService.ReadDetailsAsync(_session.Token);
            if (!_disposed && version == _sessionVersion && channel == _playingChannelId && _mediaWatch is null) { _mediaDetails = details; NotifyMediaDetails(); }
        }
        catch (OperationCanceledException) { }
        catch (Exception error) { _logger.LogError("playback_details", error); }
    }
    private async Task SaveMediaProgressAsync(Task previous, MediaWatch watch)
    {
        var progress = new MediaProgress(watch.Item, watch.Completed && watch.Length > 0 ? watch.Length : watch.Position, watch.Length, DateTimeOffset.UtcNow, watch.Completed);
        await previous;
        if (_contentStore is null || watch.ProfileId is null || !watch.Played) return;
        try
        {
            await _contentStore.SaveProgressAsync(watch.ProfileId, progress);
            if (_disposed || watch.Version != _sessionVersion) return;
            _progress = await _contentStore.LoadProgressAsync(watch.ProfileId);
            if (!_disposed && watch.Version == _sessionVersion) OnPropertyChanged(nameof(ContinueWatching));
        }
        catch (Exception error) { _logger.LogError("content_progress", error); }
    }
    public async Task SeekToAsync(double seconds)
    {
        try { if (!CanSeek || !await _playbackService.SeekAsync((long)(Math.Clamp(seconds, 0, DurationSeconds) * 1000), _session.Token)) ContentStatus = "Este medio no permite cambiar de posición."; }
        catch (OperationCanceledException) { }
        catch (Exception error) { _logger.LogError("content_seek", error); ContentStatus = "No se pudo cambiar la posición."; }
    }
    public async Task SelectTrackAsync(bool subtitles)
    {
        var track = subtitles ? SelectedSubtitleTrack : SelectedAudioTrack;
        try { if (track is null || !await _playbackService.SelectTrackAsync(subtitles, track.Id, _session.Token)) ContentStatus = "La pista ya no está disponible en este medio."; }
        catch (OperationCanceledException) { }
        catch (Exception error) { _logger.LogError("media_track", error); ContentStatus = "No se pudo seleccionar la pista."; }
    }
    private void NotifyMediaDetails()
    {
        SelectedAudioTrack = AudioTracks.FirstOrDefault(item => item.Id == _mediaDetails.AudioTrack);
        SelectedSubtitleTrack = SubtitleTracks.FirstOrDefault(item => item.Id == _mediaDetails.SubtitleTrack);
        foreach (var name in new[] { nameof(AudioTracks), nameof(SubtitleTracks), nameof(SelectedAudioTrack), nameof(SelectedSubtitleTrack), nameof(CanSeek), nameof(DurationSeconds), nameof(PositionSeconds), nameof(PositionLabel) }) OnPropertyChanged(name);
    }
    private void NotifyContent()
    { foreach (var name in new[] { nameof(HasMovies), nameof(HasSeries), nameof(MediaCategories), nameof(ContinueWatching) }) OnPropertyChanged(name); }
    private void ResetContent()
    {
        _detailRequest?.Cancel(); _mediaFilter?.Cancel(); _epgRequest?.Cancel();
        _movies = _series = _episodes = null; _progress = []; _selectedMedia = _selectedEpisode = _selectedDetails = null; _season = _mediaCategory = null;
        MediaItems = []; _mediaDetails = MediaPlaybackDetails.Empty;
        ContentStatus = "Consulta el contenido disponible de tu cuenta."; EpgStatus = "Selecciona un canal para consultar su guía.";
        NotifyContent(); NotifyMediaDetails(); OnPropertyChanged(nameof(MediaItems)); OnPropertyChanged(nameof(SelectedMedia)); OnPropertyChanged(nameof(MediaDescription)); OnPropertyChanged(nameof(Seasons)); OnPropertyChanged(nameof(Episodes));
        _epgPrograms = []; UpdateEpgLabels();
    }
    private sealed class MediaWatch(MediaItem item, string? profileId, int version, long afterSequence)
    {
        public MediaItem Item { get; } = item; public string? ProfileId { get; } = profileId; public int Version { get; } = version;
        public long AfterSequence { get; } = afterSequence;
        public bool Played, Completed; public long Position, Length; public DateTimeOffset LastSaved;
    }
}
