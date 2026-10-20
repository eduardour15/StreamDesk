# Visión Arquitectónica — StreamDesk v1.1+ (Mejoras Futuras)

> Documento de diseño y preparación arquitectónica para la evolución de **StreamDesk** posterior a las optimizaciones `UX-OPT-T01..T08`. Respeta estrictamente la regla de **1 única conexión activa simultánea**, privacidad DPAPI (`CurrentUser`) y arquitectura limpia MVVM.

---

## 1. Guía de Programación Interactiva (EPG Grid Timeline)

### Objetivo
Proporcionar una vista en rejilla horizontal estilo guía de televisión tradicional (canales en el eje Y, línea de tiempo por horas en el eje X) con desplazamiento virtualizado bidireccional a 60/120 FPS.

### Diseño Técnico
- **Contrato de consulta por ventana temporal (`IEpgTimelineStore`):**
  ```csharp
  public sealed record EpgTimeSlot(string ChannelId, string Title, DateTimeOffset StartUtc, DateTimeOffset EndUtc, string? Description);
  public interface IEpgTimelineProvider
  {
      Task<IReadOnlyDictionary<string, IReadOnlyList<EpgTimeSlot>>> GetTimelineRangeAsync(
          string profileId,
          IReadOnlyList<string> channelIds,
          DateTimeOffset windowStartUtc,
          DateTimeOffset windowEndUtc,
          CancellationToken cancellationToken = default);
  }
  ```
- **Virtualización 2D (`EpgVirtualizingCanvas`):**
  - Eje Y: sincronizado con el `ScrollViewer` de la columna de cabeceras de canales (`ScrollUnit="Pixel"`, altura fija por fila de 56 px).
  - Eje X: escala de `240 px` por cada hora (`4 px/min`), renderizando únicamente los bloques cuyo intervalo `[StartUtc, EndUtc]` intersecte el viewport horizontal visible `[ViewportStartUtc, ViewportEndUtc]`.
  - Indicador visual de hora actual (*Now Line*) actualizado cada 60 segundos sin recalcular toda la colección.

---

## 2. Caché Persistente de Logos en Disco (Disk Cache LRU)

### Objetivo
Almacenar miniaturas ya decodificadas/validadas en `%LocalAppData%\StreamDesk\Cache\Logos` con una cuota máxima de **50 MB** y purga automática por antigüedad (**TTL 14 días**), permitiendo arranque visual offline instantáneo.

### Diseño Técnico
- **Estructura de almacenamiento:**
  - Ruta: `%LocalAppData%\StreamDesk\Cache\Logos\{sha256_url}_{width}x{height}.bin`
  - Nombre de archivo derivado con `SHA256.HashData(Encoding.UTF8.GetBytes(normalizedSafeUrl))` en hexadecimal para nunca almacenar URLs ni parámetros en nombres de archivo del sistema de ficheros.
- **Política de retención y seguridad:**
  - `ChannelLogoLoader` consulta primero la caché L1 en RAM (`MaxCacheEntries = 500`), luego la caché L2 en disco (`DiskLogoCache`), y finalmente descarga vía HTTP solo si no existe o expiró (`LastWriteTimeUtc < UtcNow - 14 días`).
  - Un barrido asíncrono en segundo plano (`CleanExpiredAndEnforceQuotaAsync`) ordena los archivos por `LastAccessTimeUtc` y elimina los menos usados cuando la suma supere `50 * 1024 * 1024` bytes.

---

## 3. Normalización Relacional de VOD en SQLite con FTS5

### Objetivo
Migrar el almacenamiento de películas y series desde blobs JSON monolíticos (`MediaCache`) hacia tablas SQLite relacionales indexadas con soporte de búsqueda instantánea **FTS5 (Full-Text Search)**, reduciendo drásticamente el consumo de RAM en catálogos de más de 20.000 títulos.

### Esquema SQLite Propuesto
```sql
CREATE TABLE IF NOT EXISTS VodItems (
    ProfileId TEXT NOT NULL,
    Kind INTEGER NOT NULL,          -- 0 = Movie, 1 = Series, 2 = Episode
    ProviderId TEXT NOT NULL,
    Name TEXT NOT NULL,
    CategoryId TEXT NULL,
    PosterUrl TEXT NULL,
    Plot TEXT NULL,
    Extension TEXT NULL,
    SeriesId TEXT NULL,
    Season INTEGER NULL,
    Episode INTEGER NULL,
    PRIMARY KEY (ProfileId, Kind, ProviderId)
);

CREATE INDEX IF NOT EXISTS IX_VodItems_Profile_Kind_Category
    ON VodItems (ProfileId, Kind, CategoryId);

CREATE VIRTUAL TABLE IF NOT EXISTS VodItemsFts USING fts5(
    ProfileId UNINDEXED,
    Kind UNINDEXED,
    ProviderId UNINDEXED,
    Name,
    Plot,
    tokenize = 'unicode61 remove_diacritics 2'
);
```
- **Impacto en memoria:** En lugar de deserializar 20.000 objetos `MediaItem` en memoria, `SqliteContentStore` ejecuta consultas paginadas/filtradas por categoría y texto con latencia `< 5 ms`.

---

## 4. Zapping Predictivo con Búfer de Precalentamiento (Smart Buffering)

### Objetivo
Analizar la latencia de apertura del stream y ajustar dinámicamente el búfer de red (`500 ms – 3000 ms`) en función de la estabilidad de la conexión para evitar micro-congelamientos sin abrir conexiones simultáneas.

### Diseño Técnico
- **Cumplimiento estricto de 1 única conexión:**
  - *Smart Buffering* **nunca** abre un segundo socket en paralelo.
  - En su lugar, mantiene una ventana móvil de telemetría local por perfil (`FirstFrameLatencyMs`, `BufferingEventsCount`, `ReconnectCount`).
- **Algoritmo adaptativo (`AdaptiveBufferPolicy`):**
  - Si un canal abre en `< 600 ms` sin eventos de *rebuffering* durante 5 minutos, el cambio rápido de canal (*zapping*) utiliza un búfer ágil de `800 ms` (`:network-caching=800`).
  - Si se detectan caídas de red o reintentos en `PlaybackRecoveryService`, el búfer escala automáticamente en pasos de `+500 ms` hasta `3000 ms` para absorber *jitter* de red.

---

## 5. Modo Cine / Mini-Player Integrado (In-App Docked Player)

### Objetivo
Permitir navegar por el catálogo completo de Películas, Series o Ajustes manteniendo una pequeña ventana interna acoplada en la esquina inferior derecha de la aplicación antes de pasar a PiP nativo del sistema operativo.

### Diseño Técnico
- **Reutilización de la única instancia `VideoView`:**
  - Dado que `LibVLCSharp.WPF.VideoView` utiliza un `HwndHost` nativo con reglas de *airspace*, el contenedor `PlayerPane` permanece montado en el `Grid` raíz de trabajo y conmuta su disposición mediante estados visuales (`Normal`, `InAppMiniPlayer`, `NativePiP`, `Fullscreen`).
  - Cuando el usuario navega a Películas/Series o Ajustes con reproducción activa (`IsPlaying == true`), `PlayerPane` puede acoplarse en una región inferior derecha (`Width="320" Height="180"`) con controles compactos (`▶/⏸`, `⏹`, `⤢ Expandir`), garantizando continuidad visual sin reabrir el stream.
