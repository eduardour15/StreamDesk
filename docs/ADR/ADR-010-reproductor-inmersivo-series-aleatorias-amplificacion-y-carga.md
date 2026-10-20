# ADR-010: Reproductor inmersivo unificado, episodios al azar, amplificación de audio controlada e indicador de carga (`PLAY-UX-T01` a `PLAY-UX-T04`)

- **Fecha:** 2026-10-20
- **Estado:** Aceptada e implementada (incremento post-F7).

---

## Contexto

Tras culminar la Fase 7 y las optimizaciones de rendimiento/UX (`UX-OPT-T01` a `UX-OPT-T08`), se identificaron cuatro mejoras de experiencia de reproducción (`docs/prompts/REPRODUCTOR_INMERSIVO_Y_SERIES_ALEATORIAS.md`):
1. **`PLAY-UX-T01`:** Extender el modo enfocado (originalmente exclusivo de series mediante `IsEpisodePlayerFocus`) a **TV en vivo, películas y episodios de series** (`IsImmersiveMode`), maximizando el área de vídeo dentro de la ventana normal sin perder el contexto al volver.
2. **`PLAY-UX-T02`:** Incorporar reproducción aleatoria de episodios en series (`Episodio al azar` manual y opción `Siguiente episodio al azar` al finalizar naturalmente un episodio), evitando repeticiones inmediatas y dentro del ciclo actual.
3. **`PLAY-UX-T03`:** Permitir elevar el nivel sonoro de streams con audio bajo sin introducir distorsión por sobreganancia bruta (`Volume > 100` en LibVLC aplica ganancia cúbica por software propensa a *clipping*).
4. **`PLAY-UX-T04`:** Mostrar un indicador visual animado y accesible durante los estados de apertura (`Loading`) y almacenamiento en búfer (`Buffering`) respetando estrictamente el *airspace* del HWND nativo de `VideoView`.

---

## Decisiones

### 1. Modo inmersivo unificado (`PLAY-UX-T01`)
- Se introduce `IsImmersiveMode` en `MainViewModel` (manteniendo `IsEpisodePlayerFocus` y `ReturnToEpisodes()` como alias compatibles).
- Al iniciar reproducción por acción del usuario en TV en vivo (`PlayChannelAsync`), películas o series (`PlayMediaItemAsync`), `IsImmersiveMode` se activa automáticamente.
- `MainWindow.UpdateLayoutMode()` oculta `Sidebar`, `TvHeader`, `ConnectionNotices`, `CatalogPane`, `MovieDetailsPane`, `SeriesDetailsPane`, `EpgPanel` y `StatusBar` cuando `IsImmersiveMode` está activo (y no está en Fullscreen ni PiP), cediendo todo el espacio de cliente al reproductor (`SidebarColumn.Width = 0`, `CatalogColumn.Width = 0`).
- El botón de retorno contextual (`ImmersiveReturnLabel`: `Volver a canales`, `Volver a películas` o `Volver a episodios`) y la tecla `Esc` (con prioridad Fullscreen/PiP primero y luego modo inmersivo) salen del modo inmersivo **sin detener el stream activo** y preservando intactos los filtros, búsquedas y selecciones previas.

### 2. Reproducción aleatoria de episodios con ciclo sin repetición (`PLAY-UX-T02`)
- El conjunto de candidatos reproducibles (`GetPlayableEpisodes()`) se construye en memoria a partir del catálogo `_episodes` ya cargado para la serie activa (todas las temporadas), filtrando elementos mediante `IsPlayableEpisode` (ID y nombre no vacíos y extensión permitida por `MediaMetadata.SafeExtension`), sin redescargar el catálogo en cada elección.
- Se inyecta una fuente de aleatoriedad (`Func<int, int> RandomIndex`) en `MainViewModel` para permitir pruebas deterministas.
- Se mantiene un conjunto `_randomCyclePlayedKeys` por serie seleccionada que evita repetir el episodio actual/recién terminado cuando existen `> 1` episodios reproducibles y agota todos los episodios de la serie antes de iniciar un nuevo ciclo.
- Al elegir un episodio al azar (`PlayRandomEpisodeAsync`), se sincronizan `Season` y `SelectedEpisode` y se abre el medio por el flujo estándar de stream único (`PlayMediaItemAsync`).
- El avance automático (`AutoRandomNextEpisode`, desactivado por defecto) solo se dispara una única vez por episodio (`media.RandomAdvanceTriggered`) cuando `PlaybackSnapshot.EndOfMedia == true` (fin natural `PlaybackSignalKind.Ended`), nunca en error, *timeout*, pausa, detención manual ni cambio de perfil.

### 3. Amplificación de audio conservadora mediante preamplificador nativo de LibVLC (`PLAY-UX-T03`)
- Se verificó la versión redistribuida en el proyecto: **LibVLCSharp 3.10.2** y **VideoLAN.LibVLC.Windows 3.0.24**, que incluye los plugins oficiales `audio_filter/libequalizer_plugin.dll` y `audio_filter/libcompressor_plugin.dll`.
- El control `Volume` se mantiene estrictamente acotado a `0..100` (`100% = 0 dB` nominales), prohibiendo valores `> 100` en `MediaPlayer.Volume`.
- Se añade una propiedad independiente `AmplificationDb` acotada de forma conservadora a **`0..6 dB`** (`PlaybackService.MaxAmplificationDb = 6`, valor por defecto `0 dB (Normal)`).
- En `LibVlcBackend`, cuando `AmplificationDb > 0`, se instancia `LibVLCSharp.Shared.Equalizer`, se configura `SetPreamp(clamped)` (`+1` a `+6 dB`) y se enlaza al `MediaPlayer` mediante `SetEqualizer(equalizer)`. Cuando `AmplificationDb == 0`, se invoca `_player.UnsetEqualizer()` y se libera la instancia nativa.
- La preferencia `AmplificationDb` se persiste por perfil en la tabla `ProfilePreferences` de SQLite (`SqliteSettingsService` y `SqliteViewerStore`) mediante migración idempotente de columna (`PRAGMA table_info(ProfilePreferences)`), sin almacenar secretos.

### 4. Indicador de carga y búfer fuera del HWND nativo (`PLAY-UX-T04`)
- `MainViewModel` expone `IsVideoLoading`, `IsVideoLoadingIndeterminate`, `HasVideoBufferPercent`, `VideoBufferPercent` y `VideoLoadingMessage`, distinguiendo entre apertura inicial (`Abriendo transmisión…`, indeterminado) y almacenamiento en búfer (`Cargando búfer (N%)…` cuando LibVLC reporta `0 < BufferPercent < 100`, o `Cargando búfer…` indeterminado en caso contrario).
- En `PlaybackService`, cuando `BufferPercent >= 100` antes de recibir `Playing`, se mantiene el estado `Loading` para evitar destellos de `100%` previos al primer fotograma.
- En `MainWindow.xaml`, el banner `VideoLoadingIndicator` se ubica en `Grid.Row="2"` de `PlayerPane` (fila WPF adyacente al `VideoView` de `Grid.Row="1"`), garantizando cero intersección con el HWND nativo (`!videoBounds.IntersectsWith(loadingBounds)`).

---

## Consecuencias y verificación

- Se preserva la invariante de **1 `VideoView`, 1 `MediaPlayer` y 1 único stream activo** en todas las transiciones.
- Validado mediante pruebas unitarias deterministas (`UxOptimizationTests`) y pruebas reales de ventana WPF + `LibVlcBackend` nativo (`CatalogUiTests`).
