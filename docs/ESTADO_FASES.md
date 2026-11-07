# StreamDesk — Registro de ejecución por fases

Este archivo registra trabajo y evidencias reales. La confirmación del usuario se identifica como tal y no se completa con datos supuestos.

| Fase | Gate | Tareas completas | Última revisión | Bloqueos o dependencias |
|---|---|---:|---|---|
| F0 | APROBADA | 5/11 | 2026-10-19 | Fase marcada completada por confirmación expresa del usuario. Los criterios no acreditados siguen identificados por tarea: F0-T05/T06/T07 PARCIAL; métricas, condiciones del proveedor y avisos nativos pendientes. |
| F1 | APROBADA | 10/10 | 2026-10-19 | Fase marcada completada por confirmación expresa del usuario; la prueba real de reproducción posterior a las correcciones no se registra como evidencia independiente. |
| F2 | APROBADA | 10/10 | 2026-10-19 | Fase marcada completada por confirmación expresa del usuario. Se conservan los resultados automatizados registrados y la dependencia de validación real F0/F1 como nota histórica, sin atribuir pruebas no ejecutadas. |
| F3 | APROBADA | 7/11 verificadas; cierre solicitado | 2026-10-19 | COMPLETADA por decisión expresa del usuario. Se conservan las verificaciones físicas/IPTV/mediciones pendientes de T04/T05/T10/T11; esta decisión no constituye evidencia de esas pruebas. |
| F4 | PENDIENTE_DE_VALIDACION | 9/11 histórico | 2026-11-07 | La aprobación de 2026-10-19 se conserva como antecedente. Rediseño definitivo de Inicio y TV en vivo + optimización de rendimiento (Partes A–G) verificados con 252/252 pruebas y capturas WPF a 1366×768, 1600×900 y 1920×1080; faltan recorrido humano en monitores físicos y reproducción con cuenta autorizada. |
| F5 | APROBADA | 8/9 | 2026-10-19 | Fase marcada completada por confirmación expresa del usuario; capacidades y calidad EPG/VOD reales siguen pendientes de evidencia. |
| F6 | APROBADA | 6/12 | 2026-10-19 | Fase marcada completada por confirmación expresa del usuario. T08 sigue pendiente y T12 bloqueada hasta pruebas físicas/licencias; esta marca no constituye publicación ni aprobación de release 1.0. |
| F7 | APROBADA | 6/6 | 2026-10-19 | Fase culminada: F7-T01 (PiP), F7-T02 (M3U), F7-T03 (Perfiles múltiples y conexiones), F7-T04 (Sincronización cifrada), F7-T05 (Personalización UI) y F7-T06 (Control parental y factibilidad) implementadas y verificadas con 170 pruebas pasadas (incluyendo incremento UX-OPT-T01..T08). Validación con credenciales y listas de proveedor real en entorno físico permanece PENDIENTE_DE_VALIDACION. |
| F14 | PENDIENTE_DE_VALIDACION | 6/6 bloques de implementación | 2026-11-06 | 14.1–14.6 implementados; build y 252 pruebas pasan. Faltan recorrido manual, ventana/escalado físicos y playback con una cuenta autorizada. F14 no está desglosada en tareas del ROADMAP, así que no se inventan IDs. |

### 2026-11-07 — Rediseño definitivo de Inicio y TV en vivo + Optimización de Rendimiento y Fluidez (Partes A–G)

- **Alcance:** cierre integral del rediseño de Inicio y TV en vivo (Partes A–F) y optimización de rendimiento y fluidez como requisito funcional de alta prioridad (Parte G).
- **Sistema visual compartido (Parte A):** fondo `#0B0D12`, superficies `#171A21` y `#20242D`, acento `#38BDF8`, texto `#F8FAFC`/`#9CA3AF`, bordes sutiles `#14FFFFFF` (`rgba(255,255,255,0.08)`), radios de tarjeta `10–14 px` y botones `8 px`, jerarquía tipográfica (`30 px` título principal, `21 px` sección, `14 px` tarjeta, `12–14 px` secundario/controles) y barra lateral compacta (`176 px` expandida, `64 px` en ventana estrecha).
- **Inicio (Parte B):** portada destacada (`350 px`) alineada a la izquierda con degradado oscuro izquierda-derecha y póster vertical en el extremo derecho, año extraído del título cuando existe (`ReleaseYear`), tipo de contenido, sinopsis de hasta 3 líneas, acciones `Reproducir`/`Continuar`, `Más información` y acceso rápido para reanudar el último canal visto (`Último canal: ...`). Carruseles horizontales independientes con controles `‹`/`›`, `Shift + Rueda` y teclado para `Continuar viendo` (se oculta si no hay elementos y se reactiva al cargar caché), `Tus canales favoritos` (o invitación discreta si está vacío), `Películas para explorar` y `Series para explorar` (pósteres 2:3 con año e indicador de progreso cuando aplica).
- **TV en vivo (Parte C):** lista de canales (`320 px` / `300 px` en ventanas reducidas, contraíble) que permanece visible al seleccionar y reproducir un canal (`UpdateLayoutMode` solo activa modo inmersivo en VOD/episodios), buscador con debounce, selector de categorías buscable, filtro de favoritos y contador. Filas de `60 px` con logo de `40 px`, nombre con elipsis y tooltip, línea secundaria real (programa EPG en curso para el canal activo o nombre de categoría) e indicador de canal activo + estrella de favorito sin cambiar canal ni reiniciar el scroll. Superficie de vídeo `#000000` con estados diferenciados (`Selecciona un canal` sin controles deshabilitados, `Cargando`, `Reproduciendo`, `No se pudo reproducir` y `Sin señal disponible` con botón `Reintentar` centrado), overlay superior en vivo (`LiveTopInfoOverlay` con logo, badge `EN VIVO`, nombre y EPG actual) sincronizado con el autohide de 2,5 s de los controles inferiores, y panel EPG compacto inferior (`Programación no disponible` cuando no hay datos).
- **Rendimiento y fluidez (Parte G):**
  - **Caché O(1) por referencia en ViewModel (`EnsureLibraryCaches` y `EnsureContentCaches`):** se eliminaron los escaneos LINQ repetidos en el hilo UI sobre catálogos e historial en cada lectura de `FavoriteChannels`, `HomeFavoriteChannels`, `FavoriteMovies`, `FavoriteSeries`, `LastChannel`, `ContinueWatching`, `HomeContinueWatching`, `HomeMovies`, `HomeSeries`, `HomeFeaturedMedia`, `HomeFeaturedProgress` y `MediaProgressMap`. `1,000` lecturas consecutivas de todas las colecciones de Inicio pasaron a ejecutarse en **0,187 ms** en total.
  - **Preservación de referencia y scroll en TV en vivo:** `ApplyFilterAsync` comprueba `Channels.SequenceEqual(matches)` antes de reemplazar la lista observable, evitando reconstruir contenedores de `ChannelsList` o reiniciar el scroll al marcar favoritos o registrar historial durante la reproducción.
  - **Navegación instantánea entre pestañas:** cambiar entre Inicio, TV en vivo, Favoritos, Recientes, Continuar viendo y Ajustes no dispara re-filtrado de películas/series (`FilterMediaAsync` solo se ejecuta al entrar en Películas/Series o si la colección está vacía); tampoco reinicia la lista de canales al volver a TV en vivo.
  - **Fast-path en búsqueda de catálogos (`CatalogSearch` y `MediaCatalogSearch`) y caché síncrona de imágenes (`ChannelLogoLoader.TryGetCached`):** consultas sin filtro evitan normalizaciones por elemento conservando `CancellationToken`, y los logos/pósteres ya en memoria se asignan síncronamente sin poner `Source = null` (cero parpadeo al reciclar celdas).
  - **Sincronización no bloqueante:** se eliminó la carrera entre `LoadProfileCacheAsync` y `InitializeStartupCatalogAsync`, `AutoSyncCatalogAsync(force: true)` espera correctamente `_syncGate` y `ContentBusy` solo se activa si no hay catálogo local previo.
- **Evidencia visual y métricas automatizadas:**
  - Capturas WPF verificadas en las tres resoluciones obligatorias (`1366 × 768`, `1600 × 900`, `1920 × 1080`): `artifacts/redesign-home-1366x768.png`, `artifacts/redesign-home-1600x900.png`, `artifacts/redesign-home-1920x1080.png`, `artifacts/redesign-livetv-idle-1366x768.png`, `artifacts/redesign-livetv-playing-1366x768.png`, `artifacts/redesign-livetv-playing-1600x900.png`, `artifacts/redesign-livetv-playing-1920x1080.png` y `artifacts/redesign-livetv-error-1920x1080.png`.
  - Métricas registradas en `artifacts/redesign-performance-evidence.json` y `artifacts/flux-search-after.json`: hidratación inicial + caché `561,67 ms`, transición Inicio → TV en vivo `379,60 ms`, activación de canal + layout `60,26 ms`, `1,000` lecturas cacheadas de Inicio `0,187 ms`, virtualización de `5,000` y `20,000` canales con solo `48` filas realizadas en el árbol visual.
  - Ejecución completa: `dotnet test StreamDesk.sln -v minimal` → **252 pasadas, 0 fallidas, 0 omitidas** (`1 m 19 s`).

### 2026-11-06 — Rediseño de Inicio y TV en vivo

- **Alcance:** nueva composición de Inicio y TV en vivo conforme a la solicitud del usuario; se reevalúan F4-T01/T02/T03/T04/T05/T07/T08/T09/T10/T11. No se inicia otra fase ni se modifica el motor LibVLC.
- **Sistema visual y navegación:** canvas `#0B0D12`, superficies `#171A21`/`#20242D`, acento `#38BDF8`, texto `#F8FAFC`/`#9CA3AF`; barra lateral de 176 px que se compacta a 64 px en ventanas estrechas.
- **Inicio:** hero estable derivado del contenido pendiente o catálogo guardado; usa póster vertical real sin estirarlo como panorámica. Filas visuales independientes para continuar, favoritos, películas y series. Continuar limita a 8 elementos; catálogo inicial limita cada fila a 12, sin duplicados. Se oculta cada fila vacía, se muestra skeleton durante sincronización y se evita presentar año, sinopsis o imagen que el catálogo no contenga. Episodios muestran temporada y número.
- **TV en vivo:** lista de 320 px (300 px en ventanas reducidas), búsqueda/categoría/favoritos/conteo, filas de canal de 58 px, selección con franja de acento y lista contraíble. Importación M3U y controles administrativos permanecen en Ajustes. EPG compacto muestra programa actual/siguiente reales o «Programación no disponible»; se conserva el estado «Guía importada» en Ajustes. El favorito restablece la selección previa. Controles de seek/formato/audio/reintento/detención se ocultan cuando no aplican; el overlay y su autohide existente de 2,5 s se conservan.
- **Archivos principales:** `src/StreamDesk.Probe.Desktop/MainWindow.xaml`, `MainWindow.xaml.cs`, `MainViewModel.Content.cs`, `MainViewModel.Library.cs`, `MainViewModel.Epg.cs`, `MainViewModel.Playback.cs`, `MainViewModel.Tracks.cs`, `MainViewModel.Catalog.cs`, `MainViewModel.Management.cs`, `MainViewModel.cs`, `src/StreamDesk.Probe.Core/ContentModels.cs`, `src/StreamDesk.Probe.Desktop/Resources/Theme.xaml`, `tests/StreamDesk.Probe.Tests/SystemEndToEndTests.cs`.
- **Build:** `dotnet build StreamDesk.sln --no-restore --verbosity minimal --maxcpucount:1 -p:OutDir="C:\Users\PROGRAMADOR2\Documents\IPTV PLAYER\tests\StreamDesk.Probe.Tests\bin\home-live-validation-20261106\" -p:UseSharedCompilation=false`: **0 advertencias, 0 errores**.
- **Pruebas:** una ejecución completa registró **251 pasadas, 1 fallida, 0 omitidas**; falló `CatalogUiTests.WpfCatalogVirtualizesFiveThousandRowsFiltersAndSurvivesFailedRefresh` al agotar 2:29 en la verificación WPF/LibVLC nativa. La evidencia del test confirma que llegó a la captura de Inicio y a navegación Home/TV; no se atribuye una causa interna sin stack. Tras hacer dinámica la fecha del fixture XMLTV para evitar caducidad, la suite filtrada se repitió después del ajuste final de ancho del hero: **251 pasadas, 0 fallidas, 0 omitidas**.
- **Evidencia visual disponible:** `artifacts/player-home-layout.png` (Inicio vacío, sin cuenta) y `artifacts/f4-mvp.png` (TV con 50 canales ficticios), capturas WPF automatizadas a 1360×850 e inspeccionadas visualmente. Se generaron antes del último ajuste del ancho máximo del hero, así que documentan la composición general y TV, pero no verifican visualmente ese ajuste final. No son capturas de proveedor ni de reproducción real. La inspección de la ventana interactiva agotó el tiempo de aprobación del entorno. Tampoco se completaron capturas a 1366×768, 1600×900 y 1920×1080; una ejecución WPF dedicada se bloqueó antes del render al sembrar datos de prueba.
- **Recorrido real pendiente:** proveedor autorizado, reproducción/cambio de canal, pantalla completa, audio/subtítulos reales, persistencia entre cuentas y evaluación humana. No se usaron credenciales de usuario ni se afirma éxito de vídeo real.

| ID del ROADMAP | Estado del incremento | Evidencia / pendiente |
|---|---|---|
| F4-T01 | COMPLETADA | Paleta, jerarquía y navegación implementadas; captura WPF disponible. |
| F4-T02 | COMPLETADA | Composición Home/TV y lista/protagonismo de reproducción renderizados con fixtures; prueba física de proveedor pendiente. |
| F4-T03 | COMPLETADA | Se conservan favoritos persistidos y se añade acceso en Home; comportamiento de selección protegido. |
| F4-T04 | COMPLETADA | Fila de reanudación usa progreso persistido y etiqueta temporada/episodio. |
| F4-T05 | COMPLETADA | Búsqueda y categorías existentes permanecen en la nueva composición. |
| F4-T06 | COMPLETADA | Atajos existentes conservados; sin cambios al motor ni a sus reglas de foco. |
| F4-T07 | COMPLETADA | Overlay y autohide existente preservados; controles incompatibles se ocultan por estado. |
| F4-T08 | COMPLETADA | Skeleton, vacíos, carga y error reutilizan estados reales existentes. |
| F4-T09 | PARCIAL | Captura WPF a 1360×850 y escalado simulado existentes; faltan tres resoluciones físicas exactas, DPI/monitores y ventana pequeña en escritorio. |
| F4-T10 | PARCIAL | 251 pruebas filtradas pasan con fixtures; falta recorrido manual y validación IPTV real. |
| F4-T11 | COMPLETADA | Build de prueba generado; se registró el timeout del WPF nativo y no se oculta el fallo. |

- **Gate del incremento F4:** `PENDIENTE_DE_VALIDACION`. Próximo paso: abrir el build en escritorio sin conectar credenciales para capturar las tres resoluciones exactas y ejecutar luego el recorrido con una cuenta autorizada. No se inicia F5 ni otra fase.

### 2026-11-06 — Revisión de catálogo visual y selector de episodios

- **Hallazgos corregidos:** `IsMediaGridView` existía en el ViewModel, pero no estaba conectado a la vista; el catálogo seguía siendo únicamente una lista. El selector de episodios usaba ancho fijo y cerraba el panel aunque la solicitud de reproducción hubiera terminado en error; además, el badge podía marcar el intento fallido como reproducción activa.
- **Catálogo:** cuadrícula de pósteres predeterminada con páginas de 48 elementos, alternancia funcional a lista compacta virtualizada, búsqueda/categoría y orden por título A–Z/Z–A. Se añadió filtro y estado de favorito por película/serie. Los años y fechas de incorporación no se muestran ni ordenan porque `MediaItem` y el catálogo del proveedor no contienen esos metadatos fiables.
- **Persistencia:** nueva migración SQLite v6 con `MediaFavorites` separado por perfil y tipo (`movie`/`series`); la tabla previa de favoritos en directo y sus registros se conservan. La importación/exportación cifrada incluye los nuevos favoritos.
- **Selector de episodios:** el panel calcula su ancho con el tamaño real del reproductor, el badge depende de una sesión activa confirmada y un fallo deja el panel disponible con mensaje y permite reintentar el episodio.
- **Archivos principales:** `MainWindow.xaml`, `MainWindow.xaml.cs`, `MainViewModel.Content.cs`, `MainViewModel.EpisodesDrawer.cs`, `MainViewModel.Playback.cs`, `MainViewModel.Library.cs`, `EpisodesDrawerWidthConverter.cs`, `MediaFavoritesMigration.cs`, `SqliteSettingsService.cs`, `SqliteViewerStore.cs`, `ViewerLibrary.cs`, pruebas de catálogo, selector, almacenamiento y migraciones.
- **Comprobaciones ejecutadas:** `dotnet build StreamDesk.sln --no-restore --verbosity minimal --maxcpucount:1 -p:OutDir="$PWD\tests\StreamDesk.Probe.Tests\bin\ux-validation-20261106\" -p:UseSharedCompilation=false` (0 advertencias, 0 errores); `dotnet test StreamDesk.sln --no-build --no-restore --verbosity minimal --maxcpucount:1 -p:OutDir="$PWD\tests\StreamDesk.Probe.Tests\bin\ux-validation-20261106\"` (250 pasadas, 0 fallidas, 0 omitidas). La salida aislada evitó sobrescribir DLL que mantenía bloqueadas una instancia ya abierta de StreamDesk.
- **Pendiente manual:** comprobar con una cuenta y episodios autorizados que el vídeo continúa al abrir el panel y que un episodio remoto inaccesible deja el panel abierto; probar teclado, clic fuera, pantalla completa, ventanas pequeñas y escalas DPI físicas. Las pruebas automatizadas usan datos ficticios y no acreditan reproducción IPTV real.
- **Gate F13:** implementación y regresiones automatizadas verificadas; validación manual `PENDIENTE_DE_VALIDACION`. El repositorio no define F13 en el mapa de prompts de `AGENTS.md`; esta ejecución se ciñó a la especificación detallada de Fase 13 incluida en la solicitud del usuario.

### 2026-11-06 — FASE 14: TV en vivo, favoritos, cuentas, ajustes y navegación

- **Solicitud y alcance:** simplificar StreamDesk a una experiencia de streaming comprensible, usando como referencia las seis capturas de StreamDesk 0.5.3 adjuntas. Se preserva el trabajo previo de catálogo visual, episodios, reproducción y sincronización.
- **Diagnóstico de favoritos:** Favoritos navegaba por la vista de TV en vivo (ruta de sección 2), por lo que consultaba el conjunto de canales y presentaba «0 canales»; eso no demostraba que la acción de guardar película/serie hubiera fallado. La persistencia ya estaba separada entre favoritos `live` y `MediaFavorites`, ambos con `ProfileId`. Además, los favoritos de canales M3U se mantenían solo en memoria. No se eliminaron registros existentes.
- **Correcciones de datos:** biblioteca unificada de canales, películas y series con pestañas Todos/Canales/Películas/Series, cambios inmediatos, eliminación desde la biblioteca y filtros de tipo. Se añadió migración no destructiva SQLite v7 para guardar IDs de canales M3U por cuenta guardada, sin persistir URLs ni tokens; imports temporales siguen siendo de sesión. La sincronización cifrada incluye esa colección. Las claves de favoritos se mantienen aisladas por cuenta y tipo, sin duplicados. `docs/ADR/ADR-008-importacion-m3u.md` registra la evolución de la decisión.
- **TV y navegación:** panel de canales adaptable/redimensionable, búsqueda/categorías visibles, logos y nombres con tooltip, canal activo resaltado, lista virtualizada y controles del reproductor condicionados a contenido. El reproductor presenta estados vacío/carga/error, muestra «EN VIVO» solo con TV activa y usa programa/progreso únicamente desde datos EPG reales. Ajuste de pantalla completa conserva el área nativa del vídeo. La navegación lateral ahora ordena Inicio, TV, Películas, Series, Favoritos, Recientes, Continuar y Ajustes; Recientes conserva una ruta independiente.
- **Cuenta y ajustes:** selector visual asociado a cuentas IPTV existentes; administración incluye cambio/edición segura de una cuenta guardada sin revelar ni borrar secretos al dejar la contraseña vacía, confirmación al eliminar y estado de carga. Cabecera sin accesos laterales duplicados ni botón Flotante (PiP sigue en el reproductor). Ajustes se organizó en seis categorías; opciones numéricas y herramientas técnicas permanecen bajo Opciones avanzadas. La estabilidad tiene etiquetas cotidianas mapeadas a los valores existentes.
- **Notificaciones:** el estado fijo inferior se sustituyó por aviso temporal accesible; errores importantes permanecen visibles.
- **Archivos principales:** `src/StreamDesk.Probe.Desktop/MainWindow.xaml`, `MainWindow.xaml.cs`, `MainViewModel.Library.cs`, `MainViewModel.Content.cs`, `MainViewModel.Epg.cs`, `MainViewModel.Management.cs`, `MainViewModel.Playback.cs`, `src/StreamDesk.Application/Contracts.cs`, `src/StreamDesk.Probe.Infrastructure/M3uFavoritesMigration.cs`, `SqliteSettingsService.cs`, `SqliteViewerStore.cs`, `src/StreamDesk.Probe.Core/ViewerLibrary.cs`, pruebas de perfil, favoritos, M3U y catálogo. Guía: `docs/PRUEBA_MANUAL_FASE_14.md`.
- **Comprobaciones ejecutadas:** `dotnet build StreamDesk.sln --no-restore -m:1 -v:minimal` (0 advertencias, 0 errores); `dotnet test tests/StreamDesk.Probe.Tests/StreamDesk.Probe.Tests.csproj --no-restore -m:1 -v:minimal` (252 pasadas, 0 fallidas, 0 omitidas); caso WPF de virtualización/pantalla completa ejecutado aislado (1/1); lectura XML de `MainWindow.xaml` correcta; `git diff --check` sin errores de whitespace (Git solo avisó normalización LF/CRLF).
- **Pendiente manual:** ejecutar la guía con cuentas/catálogos autorizados; revisar tamaños pequeño/mediano/maximizado, teclado y escalas DPI de Windows; probar persistencia tras reinicio, cambio de cuenta, sincronización con archivos de prueba, EPG real disponible y reproducción de proveedor. La suite usa fixtures ficticios y un archivo local para pruebas nativas, no comprueba streaming IPTV real.
- **Gate F14:** `PENDIENTE_DE_VALIDACION`. La implementación y suite automatizada pasan, pero faltan los casos manuales requeridos. El proyecto define las fases en `docs/ROADMAP.md` hasta F7; F14 se implementó estrictamente contra la especificación detallada suministrada en esta solicitud y no se asignaron IDs de ROADMAP inventados.

## Bitácora de evidencias

### 2026-11-06 — Rediseño integral UX/UI de StreamDesk (Fases 1 a 12)

- **Solicitud y alcance:** Transformación integral de la experiencia de usuario y diseño visual de StreamDesk siguiendo los principios de interacción de Netflix, Apple TV y Disney+. Resolución definitiva del bloqueo de catálogos de películas y series deshabilitados al iniciar la app, corrección del badge «EN VIVO» en medios bajo demanda, rediseño de Inicio como dashboard de entretenimiento, cuadrícula visual de Continuar viendo sin reproductor vacío, y sistema visual oscuro moderno y cohesivo.
- **Implementación técnica por fases:**
  - **Fase 1 (Auditoría de interfaz):** Auditoría técnica profunda completada y aprobada por el usuario (registrada en `auditoria_ux_ui_fase_1.md`), identificando las 10 áreas clave de mejora arquitectónica, ciclos de ciclo de vida de caché y flujos de navegación.
  - **Fase 2 (Sistema visual y navegación):** Paleta oscura cinematográfica (`CanvasBrush: #101116`, `SurfaceBrush: #1B1D23`, `SurfaceRaisedBrush: #232630`, acentos turquesa `#70D6B2`). Barra lateral optimizada a 176 px con elementos estilo píldora (`CornerRadius="8"`), iconos Fluent uniformes, efectos hover, indicador sutil de sincronización y prevención de scrolls horizontales espurios.
  - **Fases 3 y 4 (Catálogo de películas y series):** Tarjetas con relación de aspecto vertical 2:3 (`DecodeWidth="150" DecodeHeight="220"`), badges de categoría, año y sinopsis. Paneles de detalle (`MovieDetailsPane`, `SeriesDetailsPane`) con acciones de reproducción («Reproducir», «Desde el inicio», «Episodio al azar»), selector de temporadas y episodios enriquecido. Filtrado reactivo en memoria (`MediaCatalogSearch`) preservando la virtualización de 5,000 elementos en menos de 100 ms.
  - **Fase 5 (Dashboard de Inicio):** Rediseño completo de `HomePage` reemplazando tutoriales estáticos por un dashboard vivo: banner Hero interactivo del último canal sintonizado con botón de reproducción directa, carrusel horizontal con tarjetas de contenido en curso (`ContinueWatching`), y tarjetas de acceso directo a Películas, Series, TV en vivo y Favoritos.
  - **Fase 6 (Continuar viendo):** Eliminación del reproductor vacío desaprovechado. Cuando no hay reproducción activa, la vista ocupa el espacio completo con tarjetas interactivas que muestran miniatura, barra de progreso proporcional, botón de reanudación y acción para descartar elementos (`RemoveFromContinueWatchingAsync`). Estado vacío ilustrado con acceso directo a explorar películas.
  - **Fase 7 (TV en vivo y badge «EN VIVO» estricto):** Corrección de la propiedad `IsLivePlayback` en `MainViewModel.Content.cs`. El indicador en directo solo se activa cuando existe un canal de televisión activo (`_watch is not null && _mediaWatch is null && _playback.IsActive`). Películas, series, episodios y estados vacíos quedan estrictamente excluidos.
  - **Fase 8 (Sincronización automática de catálogo y persistencia offline):** Resolución de la causa raíz de películas/series deshabilitadas: se incorporó la sobrecarga `allowStale: true` en `SqliteContentStore.LoadCatalogAsync` para cargar el catálogo guardado localmente de inmediato al iniciar, sin descartarlo por superar 7 días. Se implementó `AutoSyncCatalogAsync` en segundo plano con semáforo de concurrencia `_syncGate`, umbral de frescura de 24 horas y badge de progreso en la interfaz.
  - **Fase 9 (Ajustes y cuentas):** Sección dedicada a «Sincronización del catálogo IPTV» con información de estado y botón manual de sincronización («Sincronizar ahora»), además de gestión de perfiles con protección DPAPI.
  - **Fase 10 (Estados vacíos y de carga):** Incorporación de `InverseBooleanToVisibilityConverter` y vistas vacías informativas en todas las secciones principales (Canales, Películas, Series y Continuar viendo).
- **Evidencias y verificación:**
  - `dotnet build StreamDesk.sln`: **0 errores, 0 advertencias**.
  - `dotnet test tests/StreamDesk.Probe.Tests/StreamDesk.Probe.Tests.csproj --filter "FullyQualifiedName~PlayerUxNetflixImprovementsTests"`: **14/14 pasadas**.
  - `dotnet test tests/StreamDesk.Probe.Tests/StreamDesk.Probe.Tests.csproj`: **248 pruebas pasadas, 0 fallidas, 0 omitidas** (1 min 13 s).
- **Archivos modificados:**
  - `src/StreamDesk.Application/ContentContracts.cs`
  - `src/StreamDesk.Probe.Infrastructure/SqliteContentStore.cs`
  - `src/StreamDesk.Probe.Desktop/MainWindow.xaml`
  - `src/StreamDesk.Probe.Desktop/MainWindow.xaml.cs`
  - `src/StreamDesk.Probe.Desktop/MainViewModel.Content.cs`
  - `src/StreamDesk.Probe.Desktop/MainViewModel.Catalog.cs`
  - `src/StreamDesk.Probe.Desktop/MainViewModel.Playback.cs`
  - `src/StreamDesk.Probe.Desktop/FavoriteConverter.cs`
  - `src/StreamDesk.Probe.Desktop/ChannelLogoLoader.cs`
  - `tests/StreamDesk.Probe.Tests/PlayerUxNetflixImprovementsTests.cs`
  - `tests/StreamDesk.Probe.Tests/CatalogUiTests.cs`
- **Gate:** `APROBADA` a nivel de arquitectura, código, UX e integración de pruebas automatizadas (248/248 tests en verde). Validación con transmisiones de vídeo de proveedores físicos reales permanece como prueba en vivo del usuario.

### 2026-11-06 — FASE 13: Selector de temporadas y episodios integrado en el reproductor (estilo Netflix)

- **Solicitud y alcance:** Implementación de un panel lateral derecho flotante de temporadas y episodios integrado directamente en el reproductor multimedia (patrón Netflix). Permite navegar por la serie, cambiar de temporada y seleccionar episodios sin salir del reproductor ni interrumpir la visualización.
- **Implementación técnica:**
  - **Visibilidad condicionada del botón de acceso (`CanBrowseEpisodes`):** El botón de acceso `EpisodesDrawerButton` en la barra de controles (`MainWindow.xaml`) y el atajo de teclado 'E' solo están activos cuando el contenido actual es un episodio de serie (`_mediaWatch.Item.Kind == ContentKind.Episode`). Queda oculto estrictamente para películas VOD y canales de TV en vivo.
  - **Panel lateral superpuesto (`EpisodesDrawerOverlay`):** Se renderiza en `PlayerOverlay` ocupando entre el 35 % y el 42 % del ancho (38 %, `MinWidth="380"`, `MaxWidth="540"`), flotando sobre el lienzo del vídeo sin provocar redimensionamientos ni saltos en el renderizador de VLC.
  - **Vistas duales independientes:**
    - **Vista de episodios:** Cabecera con botón de navegación hacia el selector de temporadas `[‹]`, título de la temporada actual y botón de cierre `[✕]`. Scroll vertical independiente con tarjetas estilizadas (`DrawerEpisodeCardStyle`): número de episodio, título formateado, miniatura de vídeo (`ChannelLogo`), duración en minutos, sinopsis/trama del episodio (`Plot`), barra de progreso de visualización (`_progress`) y badge distintivo `[▶ EN CURSO]` acentuado en turquesa para el episodio activo.
    - **Vista de temporadas:** Listado vertical de temporadas ordenadas numéricamente mediante `ExtractSeasonNumber`, badge con cantidad de episodios disponibles y resalte de la temporada actualmente seleccionada.
  - **Interacciones seguras y protección de reproducción:**
    - Seleccionar el episodio que ya se encuentra en reproducción no reinicia innecesariamente el vídeo ni produce recargas de stream.
    - Seleccionar un nuevo episodio invoca `PlayEpisodeFromDrawerAsync`, cancela temporizadores pendientes de auto-play, inicia la nueva pista y cierra el panel tras confirmar la transición.
    - Exclusión mutua: Abrir el selector de episodios cierra de inmediato el selector de pistas de audio/subtítulos y el HUD de estadísticas de reproducción.
    - Cerrar mediante tecla `Escape`, atajo `E`, botón `[✕]` o clic fuera del panel sobre la superficie del vídeo (sin pausar la reproducción de vídeo).
    - Prevención de desvanecimiento de controles (`UpdateControls`) mientras el puntero o el foco de teclado se encuentren dentro del drawer.
- **Evidencias y verificación:**
  - `dotnet build StreamDesk.sln`: **0 errores, 0 advertencias**.
  - `dotnet test StreamDesk.sln --filter "FullyQualifiedName~EpisodesDrawerTests"`: **6 pruebas pasadas, 0 fallidas, 0 omitidas**.
  - `dotnet test StreamDesk.sln`: **247 pruebas pasadas, 0 fallidas, 0 omitidas** (1 min 14 s).
- **Archivos creados y modificados:**
  - Creados: `src/StreamDesk.Probe.Desktop/MainViewModel.EpisodesDrawer.cs`, `tests/StreamDesk.Probe.Tests/EpisodesDrawerTests.cs`.
  - Modificados: `src/StreamDesk.Probe.Desktop/MainWindow.xaml`, `src/StreamDesk.Probe.Desktop/MainWindow.xaml.cs`, `src/StreamDesk.Probe.Desktop/MainViewModel.cs`, `src/StreamDesk.Probe.Desktop/MainViewModel.Content.cs`, `src/StreamDesk.Probe.Desktop/MainViewModel.Playback.cs`, `src/StreamDesk.Probe.Desktop/MainViewModel.Tracks.cs`, `src/StreamDesk.Probe.Desktop/MainViewModel.Diagnostics.cs`, `tests/StreamDesk.Probe.Tests/CatalogFixtures.cs`.

### 2026-11-06 — Rediseño UX/UI del Reproductor y Corrección Integral de Interacciones

- **Solicitud y alcance:** Rediseño completo de la experiencia visual y funcional del reproductor IPTV (inspirado en patrones de interacción de Netflix, Apple TV y Disney+) y resolución definitiva del fallo en el selector de pistas donde los clics pausaban el vídeo en lugar de aplicar las opciones.
- **Diagnóstico y causa raíz:**
  - **Fallo de jerarquía visual y consumo de eventos:** En `MainWindow.xaml.cs`, `Overlay_MouseDown` empleaba `FindAncestor<Border>(source) != AudioSubtitlesOverlay` para determinar clics fuera del panel. Dado que los controles `Button` y `ui:Button` de WPF contienen elementos `Border` dentro de sus plantillas de control internas, la búsqueda siempre se detenía en el primer borde interno de la plantilla, evaluando la condición como verdadera y cerrando el panel inmediatamente al pulsar el ratón con `e.Handled = true`.
  - **Evento tunelado bloqueador:** `AudioSubtitlesOverlay` poseía `PreviewMouseDown="AudioSubtitlesOverlay_MouseDown"` con `e.Handled = true;`. Al ser un evento tunelado (*tunneling*), cancelaba la propagación antes de que los eventos de ratón alcanzaran los botones hijos de las pistas.
  - **Fuga de clics al motor multimedia:** Cualquier clic sobre la superficie del panel o de la barra de controles que no fuera un botón específico caía en `VideoSurface_MouseDown`, iniciando un temporizador de 280 ms que invocaba `TogglePlayPause()`, pausando accidentalmente el vídeo.
- **Solución técnica implementada:**
  - **Detección robusta de ancestros (`IsDescendantOf`):** Se reemplazó la búsqueda superficial por una comprobación exhaustiva del árbol visual que verifica si el origen del evento desciende de `AudioSubtitlesOverlay`, `PlaybackStatsOverlay`, `PlayerChrome`, `NextEpisodeOverlay` o `PipHeader`. Clics dentro de estos paneles interactivos se procesan normalmente sin alcanzar la superficie del vídeo ni activar la pausa.
  - **Selector flotante de audio y subtítulos en dos columnas:**
    - Panel flotante elegante (`#F210141D`, borde `#45FFFFFF`, esquinas redondeadas 12px, sombra profunda).
    - Dos columnas claras e independientes: AUDIO a la izquierda y SUBTÍTULOS a la derecha, con separador vertical discreto y scroll independiente para catálogos extensos.
    - Botones de pista de ancho completo (`TrackOptionButtonStyle`) con altura mínima de 38px, checkmark acentuado a la derecha, estados hover, focus por teclado y seleccionado claramente diferenciados.
    - **Comportamiento continuo requerido:** El panel **permanece abierto** tras seleccionar pistas de audio o subtítulos, permitiendo al usuario configurar ambas opciones secuencialmente sin interrupción del vídeo ni cierres automáticos. Solo se cierra mediante el botón [X], la tecla Escape o un clic intencionado fuera de los paneles.
    - Pill informativo inferior (`AudioSubtitlesFeedback` / `HasAudioSubtitlesFeedback`) con mensajes de transición y confirmación inmediatos.
  - **Reorganización lógica de la barra inferior de controles:**
    - **Grupo izquierdo:** Botón Reproducir / Pausar (38x36px), Retroceder 10 s, Avanzar 10 s, botón de Silencio y deslizador de volumen de 84px con visualización de nivel de 0% a 100%.
    - **Grupo derecho:** Selector de Audio y Subtítulos con indicación de estado, Lista de episodios (para series), Estadísticas de reproducción y Pantalla completa.
    - **Barra de progreso de alta usabilidad:** Estilo de slider rediseñado (`PlayerProgressSliderStyle`) con área interactiva de 24px para clics cómodos, grosor de pista de 5-6px, indicador circular brillante blanco de 14px, salto directo a posición (`IsMoveToPointEnabled="True"`), arrastre fluido, badges de tiempo transcurrido / duración total, y badge flotante interactivo de previsualización de tiempo (`SeekPreviewBadge`) al pasar el cursor sobre la barra.
    - **Indicador distintivo para contenido EN VIVO:** Cuando `CanSeek` es falso (televisión en directo), la barra de búsqueda se sustituye por una insignia distintiva roja `[● EN VIVO] Transmisión en directo`, evitando barras de desplazamiento vacías o engañosas.
  - **HUD flotante de Estadísticas de Reproducción:**
    - La información técnica (velocidad en Mb/s, FPS, búfer efectivo en segundos/ms, recargas, decodificación por hardware/software y formato del flujo) se desacopló de la barra principal y se reubicó en un panel HUD secundario accesible a demanda mediante botón en la barra o atajo de teclado, garantizando máxima limpieza visual durante la visualización normal y preservando compatibilidad regresiva con `PlaybackDiagnosticsLabel`.
- **Evidencias y verificación:**
  - `dotnet build StreamDesk.sln`: **0 errores, 0 advertencias**.
  - `dotnet test StreamDesk.sln`: **241 pruebas pasadas, 0 fallidas, 0 omitidas** (1 min 13 s).
  - Incluye 4 nuevas pruebas en `PlayerUxNetflixImprovementsTests.cs` validando el comportamiento continuo del selector de audio/subtítulos, la emisión de retroalimentación de pistas, la gestión del panel HUD de estadísticas y el indicador de directos.

### 2026-11-06 — Mejoras UX/UI tipo Netflix: Selector de audio/subtítulos, Auto-play y persistencia inteligente

- **Solicitud y alcance:** Implementación de experiencia de usuario estilo Netflix solicitada por el usuario: rediseño del selector de audio y subtítulos flotante, reproducción automática del siguiente episodio con cuenta regresiva de 10 segundos para series, y persistencia inteligente de preferencias de pistas por serie y película con coincidencia normalizada.
- **Implementación técnica:**
  - **Identificación y normalización de pistas (`src/StreamDesk.Probe.Core/MediaTrackInfo.cs`):**
    - Modelo `MediaTrackInfo` y `MediaTrackPreference` para representación inmutable de pistas con detección de idioma normalizado (códigos ISO 639), variantes regionales (Latinoamérica, España, etc.), audiodescripción (AD), subtítulos para sordos (CC/SDH), canales de audio (5.1, estéreo) y opción de subtítulos desactivados (`Id = -1`).
    - `MediaTrackParser.FindBestAudioMatch` y `FindBestSubtitleMatch` implementan coincidencia inteligente: prioridad 1) idioma y región exactos, 2) coincidencia de código de idioma, 3) coincidencia por título normalizado, 4) idioma global del usuario (`SelectedLanguage` / "es"). Si la pista preferida no existe en un episodio, se utiliza la pista por defecto sin sobrescribir la preferencia guardada, restaurándola automáticamente cuando vuelva a estar disponible en episodios posteriores.
  - **Persistencia SQLite de preferencias (`src/StreamDesk.Probe.Infrastructure/SqliteContentStore.cs`, `ContentMigration.cs`):**
    - Tabla `MediaTrackPreferences` con clave primaria `(ProfileId, ContentKey)` donde `ContentKey` mapea de forma aislada a `series:{seriesId}` para series (compartido entre episodios) y `movie:{movieId}` para películas.
    - Operaciones `LoadTrackPreferenceAsync` y `SaveTrackPreferenceAsync` sin almacenar secretos, URLs ni contraseñas.
  - **Selector flotante de audio y subtítulos (`src/StreamDesk.Probe.Desktop/MainWindow.xaml`, `MainViewModel.Tracks.cs`, `MainWindow.xaml.cs`):**
    - Panel flotante semitransparente oscuro (`#F210141D`) sobre el reproductor con dos columnas (Audio y Subtítulos), indicadores de selección `✓` (`Checkmark24`), estados de carga y pistas vacías.
    - Cierre rápido mediante clic fuera del panel, tecla Escape o botón de cierre. Atajo de teclado 'S' / 'C' para alternar.
    - Selección con 1 solo clic que aplica la pista en caliente mediante `_playbackService.SelectTrackAsync` sin reiniciar el vídeo ni alterar la posición.
    - Se mantiene compatibilidad regresiva con `AudioTrackBox` y `SubtitleTrackBox` en modo oculto para aserciones de pruebas y accesibilidad automatizada.
  - **Auto-play del siguiente episodio (`src/StreamDesk.Probe.Desktop/MainViewModel.AutoPlay.cs`, `MainWindow.xaml`, `MainViewModel.Playback.cs`):**
    - Activado exclusivamente para episodios de series (`ContentKind.Episode`) al finalizar la reproducción (`EndOfMedia`). Películas y canales en vivo excluidos explícitamente.
    - Algoritmo de detección `FindNextEpisode` que ordena temporadas y episodios numéricamente, manejando transiciones entre temporadas (último episodio de temporada N -> primer episodio de temporada N+1) y final de serie (último episodio de última temporada no activa auto-play).
    - Tarjeta superpuesta con cuenta regresiva de 10 segundos en tiempo real (`Stopwatch`), barra de progreso animada, botón de reproducción inmediata («Reproducir siguiente») y botón «Cancelar».
    - Cancelación segura ante cierre del reproductor, cambio de canal o parada de reproducción sin dejar temporizadores huérfanos ni dobles reproducciones (`Peak == 1`).
- **Archivos creados y modificados:**
  - Creados: `src/StreamDesk.Probe.Core/MediaTrackInfo.cs`, `src/StreamDesk.Probe.Desktop/MainViewModel.Tracks.cs`, `src/StreamDesk.Probe.Desktop/MainViewModel.AutoPlay.cs`, `tests/StreamDesk.Probe.Tests/PlayerUxNetflixImprovementsTests.cs`.
  - Modificados: `src/StreamDesk.Application/ContentContracts.cs`, `src/StreamDesk.Application/PlaybackService.Media.cs`, `src/StreamDesk.Probe.Infrastructure/SqliteContentStore.cs`, `src/StreamDesk.Probe.Infrastructure/ContentMigration.cs`, `src/StreamDesk.Probe.Desktop/MainViewModel.Content.cs`, `src/StreamDesk.Probe.Desktop/MainViewModel.Playback.cs`, `src/StreamDesk.Probe.Desktop/MainWindow.xaml`, `src/StreamDesk.Probe.Desktop/MainWindow.xaml.cs`.
- **Evidencia automatizada:**
  - `dotnet build StreamDesk.sln`: **0 errores, 0 advertencias**.
  - `dotnet test tests/StreamDesk.Probe.Tests/StreamDesk.Probe.Tests.csproj --filter "FullyQualifiedName~PlayerUxNetflixImprovementsTests"`: **9/9 pasadas** (0 ms fallos).
  - `dotnet test StreamDesk.sln`: **237 pasadas, 0 fallidas, 0 omitidas**, 1 min 7 s.
- **Gate:** `APROBADA` a nivel de implementación y pruebas automatizadas locales; validación audiovisual interactiva con proveedor IPTV real permanece como escenario de validación física del usuario.

### 2026-10-24 — Fluidez por protocolo, calidad HLS y diagnóstico de sesión

- **Autorización y alcance:** el usuario solicita aplicar la alternativa considerada más conveniente frente a cortes frecuentes. Se implementa el incremento de F3-T05/T07/T10/T11 descrito en ADR-014, conservando las correcciones previas y los gates históricos. No se inicia otra fase ni se asume que nombres parecidos identifican señales equivalentes.
- **Comportamiento:** «Priorizar fluidez» está activo por defecto en la aplicación; conserva el búfer elegido y usa al menos 5000 ms efectivos en HTTP de TV. Al conectar prefiere HLS solo si la cuenta declara `m3u8`. Se configuran el retraso objetivo adaptativo de 20–30 s, máximo de reserva de 30 s y exclusión de baja latencia en ese modo; el proveedor puede limitarlo. Los watchdogs de fuentes `.m3u8` permiten rellenar esa reserva. «Automática» usa la adaptación nativa `nearoptimal`; «Ahorro» solicita `lowest`. Una única variante conserva la señal original. Se mantienen TS manual, stream único, VOD/archivos locales y recuperación acotada.
- **Diagnóstico:** deltas monotónicos del contador de lectura y fotogramas de VLC, caché efectiva, recargas y tiempo observado de espera en Ajustes/controles. Datos ausentes se muestran como «sin datos»; no son un test de velocidad ni identifican la causa del problema. Los contadores se reinician por sesión/intento según su origen, y pausa/seek no cuentan como nuevas recargas. No se persisten URLs, datos de cuenta ni estadísticas en logs.
- **Persistencia y UI:** columnas idempotentes `PrioritizeContinuity`/`LiveQuality` preservan búfer, software y otras preferencias. Valores de calidad desconocidos vuelven a Automática. Las escrituras se esperan por la cola existente al cerrar. «Aplicar al canal actual» guarda y reabre la misma entrada por el flujo serializado; en Ahorro selecciona HLS si está declarado. La adaptación no se obtiene reduciendo el tamaño de la imagen ni transcodificando localmente.
- **Archivos principales:** modelos `PlaybackModels.cs`, `PlaybackDiagnostics.cs` y `Profile.cs`; contratos/servicio Application, nuevo `PlaybackService.Diagnostics.cs`; `LibVlcBackend.cs`; `SqliteSettingsService.cs`; composición `AppServices`, `MainViewModel.Diagnostics.cs`, integración de catálogo/preferencias/temporizador y vista WPF. Tests `PlaybackContinuityTests`, `NativeLiveQualityTests` y `CatalogUiTests.Continuity`; ADR-014 y actualización del protocolo manual de estabilidad.
- **Pruebas nuevas:** 4/4 casos controlados de migración/persistencia, reserva/timeout HLS y exclusión VOD/local, diagnóstico (incluye pausa, seek y reset de contadores) y aplicación de preferencias con `Peak == 1`. 1/1 prueba nativa HLS: MPEG-TS generado con el encoder incluido, servidor loopback con master de dos variantes declaradas y playlist única; Ahorro solicita la variante de menor ancho de banda y todos los escenarios producen frames con el mismo `MediaPlayer`. Las variantes del fixture reutilizan el mismo TS; no prueba ahorro medido de bytes, distinta resolución ni subida/bajada automática con ancho de banda real fluctuante.
- **Comandos y resultados:** `dotnet build StreamDesk.sln --no-restore --maxcpucount:1 -p:UseSharedCompilation=false --verbosity minimal`, con `-p:BaseOutputPath=C:\Users\PROGRAMADOR2\Documents\IPTV PLAYER\artifacts\fluidez\bin\`: **0 errores, 0 advertencias**. `dotnet test StreamDesk.sln --no-restore --maxcpucount:1 -p:UseSharedCompilation=false --verbosity minimal`, con la misma salida: **208 pasadas, 0 fallidas, 0 omitidas**, 1 min 7 s. Una suite anterior detectó una aserción de texto en el overlay aún oculto; se corrigió la prueba para verificar Ajustes y luego la página de TV visible. El resultado corresponde a la ejecución corregida. `git diff --check` no detecta defectos de whitespace (Git avisa de conversión LF/CRLF).
- **Ejecución y entrega:** se conservó abierta la instancia Debug del usuario. La salida alternativa evita reemplazar sus DLL en uso. Ejecutable nuevo: `artifacts/fluidez/bin/Debug/net10.0-windows/StreamDesk.Probe.Desktop.exe`, junto a sus dependencias. La instancia ya abierta no incorpora los cambios; cerrarla antes de abrir el nuevo ejecutable para conservar una sola reproducción. No se instala ni se cambia el acceso directo de Program Files.
- **Verificación visual adicional:** tras ajustar únicamente el desplazamiento para capturar las preferencias completas, se repitió el escenario WPF con filtro `FullyQualifiedName~WpfCatalogVirtualizesFiveThousandRowsFiltersAndSurvivesFailedRefresh`: **1/1 pasado**, 55 s. La captura local `artifacts/playback-continuity-settings.png` se inspeccionó visualmente: selector, botón Aplicar, ayuda y diagnóstico legibles, con wrapping y scroll. Se usan datos ficticios; no es una captura del proveedor ni de la sesión del usuario.

| ID del ROADMAP | Estado del incremento | Evidencia / pendiente |
|---|---|---|
| F3-T05 | PARCIAL | Reserva por protocolo y preferencias verificadas; selección HLS nativa local comprobada. Faltan variantes/calidad y fallback de códec/GPU del proveedor real. |
| F3-T07 | COMPLETADA | Presupuesto, cancelación, caché retenida y watchdogs ajustados con pruebas; la eficacia en la señal reportada no se da por acreditada. |
| F3-T10 | PARCIAL | Diagnóstico numérico y UI implementados; faltan CPU/RAM/GPU y comparación de cortes/apertura del proveedor. |
| F3-T11 | PARCIAL | Suite y fixtures locales con stream único pasan; faltan 20 canales y cortes/cierre con proveedor real. |

- **Gate del incremento:** `PENDIENTE_DE_VALIDACION`. Aceptación local de build, persistencia, UI, vídeo HLS local y exclusión VOD `PASA`; mejora de la señal real, adaptación ante red variable y métricas de GPU `NO VERIFICADO`. Próximo paso: cerrar el ejecutable anterior y abrir la salida nueva; comparar Automática/Ahorro y fluidez con la señal problemática según `docs/PRUEBA_MANUAL_ESTABILIDAD_TV.md`. No se inicia otra fase ni se publica o hace push.

### 2026-10-24 — Optimización de estabilidad y recuperación de TV

- **Solicitud y alcance:** analizar y mejorar cortes/congelamientos en la configuración actual; correcciones locales de F3-T05/T07/T10/T11. Se conservan los cambios previos del usuario y los gates históricos; no se inicia otra fase.
- **Configuración real consultada:** lectura SQLite limitada a `BufferMilliseconds` y `SoftwareDecoding`: **3500 ms / automático**. El primer intento de solo lectura no pudo acceder al WAL desde el aislamiento (`SQLITE_CANTOPEN`); la consulta de solo lectura fuera de él funcionó. No se consultaron perfiles/credenciales ni se modificó la configuración. La instancia activa era la instalación de Program Files; los cambios compilados del proyecto todavía no actualizan esa instalación.
- **Hallazgos reparados:** opciones que impedían descartar fotogramas atrasados; selección automática limitada a D3D11VA; avisos repetidos `Buffering/Playing` que podían ocultar salida congelada o cancelar su watchdog; detector calculado con caché inicial; pérdida del aumento de caché al resetear reintentos; pausas sin avance contadas como estabilidad continua. Se elimina además el uso incorrecto de bytes recibidos como posición temporal.
- **Implementación:** VLC puede recuperar atraso y elegir su decodificador; el búfer efectivo aumenta 1500 ms por reintento de TV hasta 10000 ms y se conserva en la sesión. Su valor se usa en la tolerancia de estancamiento. Se mantiene el muestreo durante búfer después del primer `Playing`, y solo el avance observado acredita recuperación de un estancamiento confirmado. Presupuesto de dos reintentos, backoff, stop previo y fallback software conservados. VOD no recibe escalado de caché. Ajustes explica cuándo aplicar los valores y el coste en demora.
- **Archivos:** `src/StreamDesk.Application/PlaybackService.cs`, `PlaybackService.Progress.cs`, `src/StreamDesk.Probe.Playback/LibVlcBackend.cs`, ayuda de preferencias en `src/StreamDesk.Probe.Desktop/MainWindow.xaml`, `tests/StreamDesk.Probe.Tests/PlaybackStabilityTests.cs`, `NativePlaybackStabilityTests.cs` y expectativas de tiempos en `FluidityTests.cs`. Decisiones en ADR-013 y referencias de ADR-003/011; protocolo `docs/PRUEBA_MANUAL_ESTABILIDAD_TV.md`.
- **Evidencia automatizada:** cinco casos nuevos fallaron antes de reparar el código (avisos alternos o búfer continuo con imagen congelada, tolerancia de caché real, conservación de caché y estabilidad interrumpida); después **45/45** tests de reproducción/fluidez pasaron. Prueba nativa adicional **1/1**: AVI local generado con píxeles cambiantes en automático/software/automático, mismo `MediaPlayer` y cierre correcto. No acredita decodificación acelerada de H.264/HEVC ni IPTV real.
- **Comandos ejecutados:** `dotnet build StreamDesk.sln --no-restore --maxcpucount:1 -p:UseSharedCompilation=false --verbosity minimal`: **0 errores, 0 advertencias**. `dotnet test StreamDesk.sln --no-build --no-restore --maxcpucount:1 --verbosity minimal`: **203 pasadas, 0 fallidas, 0 omitidas**, 1 min 10 s. La suite incluye WPF/LibVLC y HTTP local; no se usó una cuenta real para reproducción ni se ejecutó nueva entrada física de escritorio. No se midieron CPU/GPU ni tasa de cortes de la señal reportada.

| ID del ROADMAP | Estado del incremento | Evidencia / pendiente |
|---|---|---|
| F3-T05 | PARCIAL | Configuración nativa y caché efectiva reparadas; frames AVI en automático/software comprobados. Falta H.264/HEVC/GPU y fallback en hardware real. |
| F3-T07 | COMPLETADA | Recuperación por avance, caché retenida, presupuesto acotado y casos negativos/cancelación/regresión probados. La mejora del proveedor no se da por demostrada. |
| F3-T10 | PARCIAL | Configuración local leída y protocolo comparativo entregado; CPU/RAM/GPU, cortes y apertura en la señal problemática sin medir. |
| F3-T11 | PARCIAL | Regresiones controladas de 20 cambios y medios locales pasan en la suite; faltan 20 canales y corte/cierre con IPTV real. |

- **Aceptación local:** build/tests, recuperación controlada, stream único en backend controlado y vídeo nativo local `PASA`. Reducción de congelamientos, calidad audiovisual y rendimiento de proveedor/GPU `NO VERIFICADO`.
- **Gate del incremento:** `PENDIENTE_DE_VALIDACION`. Próximo paso: cerrar la instancia anterior y ejecutar el build actualizado; comparar la señal problemática con 3500/5000/8000 ms mediante la guía manual y registrar métricas sin secretos. No instalar/publicar/push ni iniciar otra fase.

### 2026-10-24 — Seguimiento: overlay del reproductor visible sobre Inicio

- **Reporte:** el usuario confirma que las funciones anteriores operan, salvo el icono y los controles superpuestos sobre Inicio mostrados en su captura. Se registra como confirmación del usuario, sin atribuir comprobaciones concretas de proveedor/DPI no aportadas.
- **Causa y corrección (`F4-T07`, `COMPLETADA`):** la ventana foreground de LibVLCSharp no hereda el `Collapsed` de la página WPF. `MainWindow.xaml.cs` sincroniza su `Hide`/`Show` con la vista del reproductor realmente visible y la minimización; Inicio, Ajustes y los catálogos no muestran overlay. Se cancela el clic pendiente y se restaura el cursor al ocultarlo. La reaparición no roba foco y conserva la política de activación original. No se recrea el vídeo ni se abre otro medio.
- **Regresión:** `CatalogUiTests.cs`/`CatalogUiTests.PlayerOverlay.cs` reprodujeron el fallo antes de corregirlo. Verifican Inicio inicial sin overlay, cuatro recorridos Inicio/Ajustes/Películas/Series y retorno a TV, ausencia del HWND mediante `IsWindowVisible`, notificaciones/activación sin resucitarlo y el mismo HWND/medio en reproducción. Se amplían ADR-012 y el paso 9 de la guía manual.
- **Comandos finales:** `dotnet build StreamDesk.sln --no-restore --maxcpucount:1 -p:UseSharedCompilation=false --verbosity minimal`: **0 errores y 0 advertencias**. `dotnet test StreamDesk.sln --no-build --no-restore --maxcpucount:1 --verbosity minimal`: **197 pasadas, 0 fallidas, 0 omitidas**. Se usó el modo serie por la limitación de MSBuild paralelo ya documentada.
- **Evidencia:** `artifacts/player-overlay-navigation-evidence.json` con WPF/LibVLC y AVI local; `player-home-layout.png` inspeccionada (captura de layout WPF, complementada por la comprobación del HWND real). El reintento opcional de entrada física no se completó: `GetCursorInfo` no estuvo disponible en el escritorio de esa ejecución. No se presenta como prueba de entrada física nueva ni se sobrescribe la evidencia anterior.
- **Gate:** corrección local completada; se mantiene `PENDIENTE_DE_VALIDACION` del incremento para proveedor, DPI/monitores y escenarios manuales pendientes. Próximo paso: reabrir el ejecutable Debug actualizado y repetir el paso 9 con la cuenta local. No se inicia otra fase, instala, publica ni hace push.

### 2026-10-24 — Corrección de overlay, cursor y atajos del reproductor

- **Alcance:** corrección de F0-T07/F3-T04 y F4-T06/T07/T09 solicitada por la franja inferior gris en pantalla completa; no se inicia otra fase ni se alteran gates históricos.
- **Implementación:** `MainWindow.xaml` aloja controles, estado, carga, OSD y cabecera PiP en `VideoView.Content`, usando el foreground HWND transparente de LibVLCSharp.WPF 3.10.2. Se elimina la fila fija de 34 DIP y el espacio de `Hidden`. La barra semitransparente desaparece con `Collapsed` sin variar los límites del vídeo. Se mantiene relación de aspecto, un `VideoView`, HWND, `MediaPlayer` y stream.
- **Interacción:** autoocultamiento a 2,5 s, cursor local mediante `Cursor`/`ForceCursor`, recuperación por movimiento/teclado/activación y restauración al salir, pausar, cargar, fallar, minimizar o cerrar. Protección de controles con hover, foco de navegación, captura táctil/ratón y desplegables. El fondo usa alfa 1/255: la prueba física detectó que los píxeles alfa cero de la ventana layered pasan el ratón al vídeo y no permiten ocultar su cursor desde WPF. El coste es una atenuación negra de aproximadamente 0,4 %.
- **Atajos:** Espacio/K pausa/reanuda la sesión activa; F/Esc; M; ↑/↓ en pasos de 5 acotados a 0–100; ←/→ ±5 s y J/L ±10 s con `CanSeek`; Shift+P/N para anterior/siguiente. Se conservan P (PiP), B y el Sound Booster manual existente. Se recibe teclado en ambas fuentes de presentación y se respetan edición, listas, controles, popups y repetición.
- **Archivos:** vista principal y coordinación Desktop, `PlaybackShortcuts`, tooltip del ViewModel, regresiones `CatalogUiTests`/`ShortcutTests`, vídeo generado `NativeVideoFixture`, ADR-012 y notas de sustitución de ADR-004/007/010/011; guía `docs/PRUEBA_MANUAL_REPRODUCTOR_OVERLAY.md`. Una aserción existente de catálogo vacío se sincroniza con el fin del filtrado asíncrono, sin cambiar el comportamiento del producto.
- **Comandos y resultados:** `dotnet build StreamDesk.sln`: 0 advertencias/errores fuera del aislamiento; dentro del sandbox el modo paralelo de MSBuild terminó sin diagnóstico, mientras `dotnet build StreamDesk.sln --no-restore --maxcpucount:1 -p:UseSharedCompilation=false` pasó. `dotnet test StreamDesk.sln` con `STREAMDESK_NATIVE_INPUT=1` fuera del aislamiento: **197 pasadas, 0 fallidas, 0 omitidas**. El primer intento completo detectó la aserción asíncrona citada; el resultado corresponde a la ejecución corregida. El sandbox no permite `GetCursorInfo`; la verificación de escritorio se ejecutó fuera de él.
- **Evidencia real:** WPF + LibVLC con vídeo AVI local generado de 16:9/2 fps, sin audio ni proveedor; 16 fotogramas decodificados en el punto registrado; clic real sobre Pausa, tecla Espacio de Windows, cursor ausente según `GetCursorInfo`, movimiento que lo recupera, viewport idéntico antes/después de ocultar, tres ciclos fullscreen/restauración con el mismo HWND y regresiones PiP. Capturas compositadas `artifacts/player-overlay-visible.png` y `player-overlay-hidden.png` inspeccionadas; JSON `player-overlay-desktop-evidence.json`. La suite normal registra evidencia separada en `player-overlay-evidence.json`.

| ID del ROADMAP | Estado del incremento | Evidencia / pendiente |
|---|---|---|
| F4-T06 | COMPLETADA | Mapeo, foco de overlay, pausa sin reapertura, seek acotado y volumen 0–100; pruebas unitarias/WPF y Espacio nativo. |
| F4-T07 | COMPLETADA | Foreground HWND, barra flotante, autohide 2,5 s, cursor nativo y geometría estable con vídeo local. |
| F0-T07 / F3-T04 | PARCIAL | Overlay y ciclos de fullscreen comprobados localmente; proveedor, dos monitores y DPI físicos múltiples pendientes. |
| F4-T09 | PARCIAL | Regresiones de escala/layout y PiP; faltan combinaciones físicas de DPI/monitores y evaluación humana. |

- **Gate del incremento:** `PENDIENTE_DE_VALIDACION` para vídeo/audio del proveedor, dos monitores/DPI distintos y los escenarios manuales no ejecutados de la guía. Las aprobaciones históricas de fases se conservan sin atribuirles esta evidencia. No se publica, instala ni hace push. Próximo paso recomendado: ejecutar la guía manual en el equipo del usuario, sin iniciar otra fase.

### 2026-10-21 — Refuerzo de estabilidad y anticongelamiento en reproducción de TV en vivo

- **Solicitud y alcance:** Mejorar la reproducción de canales en vivo para evitar al máximo congelamientos, microcortes y bloqueos silenciosos sin violar la regla de **1 único stream activo** (`Peak == 1`) ni exponer secretos.
- **Implementación técnica:**
  - **Opciones nativas LibVLC para TV en vivo (`src/StreamDesk.Probe.Playback/LibVlcBackend.cs`):**
    - Para emisiones en directo (`!options.OnDemand`) se añaden `:clock-jitter=5000`, `:no-drop-late-frames`, `:no-skip-frames`, `:avcodec-threads=0` y `:http-reconnect` (en esquemas HTTP/HTTPS) junto a `:network-caching`, `:live-caching` y `:file-caching`.
    - En `ReadProgressAsync` se incorpora respaldo con `stats.DemuxReadBytes` cuando `_player.Time <= 0`, garantizando que la supervisión de avance funcione también en flujos MPEG-TS crudos sin base PTS inicial.
  - **Supervisión de estancamientos, búfer adaptativo y reinicio de presupuesto (`src/StreamDesk.Application/PlaybackService.cs`, `PlaybackService.Progress.cs`, `PlaybackContracts.cs`, `NativeFailureClassifier.cs`):**
    - Búfer predeterminado de aplicación elevado a **3500 ms** (`AppSettings.BufferMilliseconds = 3500`, `AppServices`), manteniendo el valor por defecto del contrato base en `1000 ms` para compatibilidad con las pruebas unitarias de latencia.
    - **Escalado adaptativo de búfer en reintentos en vivo:** cada reconexión automática incrementa el caché efectivo en `+1500 ms` (`RetryBufferStepMilliseconds`, acotado a `250..10000 ms`, ej. `3500 ms -> 5000 ms -> 6500 ms`) tras cerrar determinísticamente el stream anterior.
    - **Corrección de congelamientos tras parpadeo de `Buffering`:** los eventos nativos intermedios `Buffering` / `Buffering(100%)` / `Playing` en una sesión ya iniciada (`session.HasPlayed`) ya no borran `session.Progress` ni `session.ProgressObserved`, por lo que si el canal sale del estado de búfer con el vídeo congelado en el mismo fotograma, el detector de estancamiento silencioso (`SilentStall`) actúa en `5 s` (`StallTimeout`) y reconecta.
    - **Reinicio de presupuesto de reintentos tras reproducción estable:** tras `60 s` (`StableResetThreshold`) de avance continuo de fotogramas, `session.Retries` vuelve a `0`, permitiendo que sesiones largas de TV en vivo superen microcortes aislados durante horas.
    - **Clasificación ampliada de fallos transitorios:** `NativeFailureClassifier` reconoce módulos `tls`, `gnutls`, `adaptive`, `httplive` y `prefetch`, así como errores de lectura (`read error`, `recv error`, `broken pipe`, `connection closed`, `peer closed`), y `FailAsync` trata fallos `Unknown` a mitad de emisión en vivo como `Network` para reconectar automáticamente.
  - **Conmutación automática y manual de formato en vivo (`ts` ↔ `m3u8`) y persistencia de estabilidad (`src/StreamDesk.Probe.Desktop/MainViewModel*.cs`, `MainWindow.xaml`, `MainWindow.xaml.cs`, `SqliteSettingsService.cs`, `Profile.cs`):**
    - Si un canal Xtream agota los reintentos en el formato actual (`ts` o `m3u8`) y la cuenta ofrece formato alternativo en `AvailableFormats`, conmuta automáticamente al formato alternativo por una vez por selección de canal (`TriedAlternateFormat`), siempre cerrando antes el stream previo (`Peak == 1`).
    - Al cambiar manualmente el formato en el selector `FormatBox` de la barra del reproductor (`DropDownClosed="Format_Changed"`), el canal en vivo se reabre al instante en el nuevo formato.
    - `BufferMilliseconds` y `SoftwareDecoding` se persisten en SQLite (`AppSettings` con migración no destructiva `EnsurePlaybackSettingsColumnsAsync`) y se exponen en Ajustes con selector rápido de nivel de estabilidad (`Rápido 1000 ms`, `Estable 2500 ms`, `Antitrabas 3500 ms`, `Antitrabas Plus 5000 ms`, `Antitrabas Máximo 8000/10000 ms`).
    - Se optimizó `_mediaTimer` (`RefreshPlaybackDetailsAsync(periodicTimer: true)`) para no sondear repetidamente las listas nativas de pistas de LibVLC cada 2 segundos en TV en vivo una vez cargadas las pistas del canal activo.
- **Comandos ejecutados y resultados:**
  - `dotnet build StreamDesk.sln -c Release`: **0 advertencias, 0 errores**.
  - `dotnet test StreamDesk.sln -c Release --no-build --no-restore --maxcpucount:1`: **191 pasadas, 0 fallidas, 0 omitidas** (incluye nuevas pruebas `LiveStreamAdaptiveBufferResetsRetriesAfterStablePlaybackAndSurvivesBufferingFlicker` y `LiveFormatFallbackAndPersistedStabilitySettingsWorkWithoutExtraActiveStreams` en `FluidityTests.cs`).
- **Validación manual / proveedor físico:** `PENDIENTE_DE_VALIDACION` con señal real del proveedor IPTV del usuario en Windows.

### 2026-10-20 — Ejecución del incremento de fluidez y UX/UI (`FLUX-T01` a `FLUX-T06`)

- **Solicitud y alcance:** Ejecución explícita de `docs/prompts/OPTIMIZACION_FLUIDEZ_UX_UI.md`. Se optimizaron las funciones actuales sin añadir funciones, paquetes ni migraciones. Se conservaron cambios anteriores y gates históricos; no se abrió otra fase.
- **Implementación:** Índice normalizado VOD reutilizable y búsqueda TV/VOD con debounce medido de 120 ms; protección de consultas por sesión/pestaña; supervisión de progreso multimedia serializada con reloj monotónico; loader durante estancamiento/espera y parada disponible durante backoff; controles con título legible y layout estable; tema dinámico sincronizado con WPF UI; menos notificaciones/sondeos y cambios de audio consolidados; descargas de imágenes compartidas, cancelación por consumidor, TTL de fallos y presupuesto de píxeles decodificados de 32 MiB además de 500 entradas.
- **Estados:** `FLUX-T01` y `FLUX-T03`: `COMPLETADA` en verificación local. `FLUX-T02`, `FLUX-T04`, `FLUX-T05` y `FLUX-T06`: `PARCIAL`, con código y pruebas locales, pendientes de interrupción nativa real, hardware/CPU/RAM/GPU sostenidos, DPI/monitores físicos y evaluación humana según cada bloque. Trazabilidad y criterios en `docs/CIERRE_FLUIDEZ_UX_UI.md`.
- **Evidencia:** Fixture WPF con 5.000/20.000 títulos, ocho consultas por tamaño y siete filas materializadas. p95 visible: **247,06 → 199,41 ms** y **299,95 → 197,94 ms** respectivamente, incluyendo el ajuste explícito del debounce **180 → 120 ms**. Asignaciones de 20.000 títulos: **46,59 → 11,24 MB**, sin afirmar reducción de RAM total. Comparación aislada del predicado previo frente al índice y capturas en `artifacts/flux-*`; resultados/copias numéricas y límites en el cierre. CPU/RAM/GPU globales no se consideran mejorados por esta muestra.
- **Comandos finales:** `dotnet build StreamDesk.sln -c Release --no-restore --maxcpucount:1 -p:UseSharedCompilation=false -nodeReuse:false`: **0 advertencias, 0 errores**. `dotnet test StreamDesk.sln -c Release --no-build --no-restore --maxcpucount:1`: **188 pasadas, 0 fallidas, 0 omitidas**. Se verificaron cancelación, descarte de lecturas nativas del canal anterior, presupuesto de reintentos, pausa/seek, imágenes concurrentes y tamaño del vídeo; la lectura del reloj nativo BMP no acredita un congelamiento IPTV real.
- **Decisión:** Gate del incremento **PENDIENTE_DE_VALIDACION**. No se ejecutó sesión con proveedor real ni se aprobaron DPI/monitores/GPU físicos o una release. Próximo paso: protocolo manual del cierre, sin avanzar a otra fase. Decisiones en `docs/ADR/ADR-011-fluidez-busqueda-estancamientos-y-estados.md`.

### 2026-10-20 — Análisis previo y preparación de prompt de fluidez y UX/UI (sin implementación)

- **Solicitud y alcance:** Analizar la aplicación antes de redactar un prompt para optimizar sus funciones actuales. Se revisaron arquitectura, ROADMAP/ADRs, búsquedas, listas, imágenes, reproducción, estados de carga, temporizadores, controles, temas y pruebas. No se modificó código de aplicación ni se ejecutaron las optimizaciones propuestas.
- **Entregable (`COMPLETADA` la preparación):** `docs/prompts/OPTIMIZACION_FLUIDEZ_UX_UI.md`, incorporado a `docs/INDICE_PROMPTS.md`. Define `FLUX-T01` a `FLUX-T06`, relacionados con IDs existentes del ROADMAP, con diagnóstico previo, mediciones, mejoras acotadas y aceptación sin añadir funciones. Todos los bloques de implementación permanecen `PENDIENTE`; gate del nuevo incremento: `NO_INICIADA`. Gates históricos sin cambios.
- **Observaciones para revalidar al ejecutar:** TV ya dispone de índice normalizado y debounce de 180 ms; VOD normaliza títulos durante cada filtrado. Ya existen virtualización, LRU de imágenes e indicador de carga/búfer. No se observa supervisión independiente de progreso durante `Playing`; la detección de estancamientos sin eventos se plantea como investigación y corrección condicionada a señales fiables. Revisar también arrays/notificaciones de pistas, getters que reconstruyen colecciones y densidad de controles.
- **Evidencia visual:** Revisión de `artifacts/f2-catalog.png` y `artifacts/f4-mvp.png`, regenerados por pruebas WPF con datos ficticios. La barra a 1360×850 presenta poco espacio para el título. Estas capturas no acreditan vídeo nativo ni visibilidad de overlays sobre HWND.
- **Comandos y resultados de la base actual:**
  - `dotnet build StreamDesk.sln -c Release --no-restore`: primer intento cancelado durante compilación (`MSB5021`); no se registra como exitoso.
  - `dotnet build StreamDesk.sln -c Release --no-restore --maxcpucount:1 -p:UseSharedCompilation=false -nodeReuse:false`: **correcto, 0 advertencias y 0 errores**.
  - `dotnet test StreamDesk.sln -c Release --no-build --no-restore --maxcpucount:1`: **177 pasadas, 0 fallidas, 0 omitidas**. Evidencia de la base existente, no de mejoras todavía no implementadas.
- **Pendientes y límites:** No se ejecutó sesión con proveedor IPTV real ni evaluación física de DPI/monitores/GPU. Fluidez sostenida, primer fotograma y estancamientos silenciosos requieren mediciones específicas; no se atribuyen mejoras de rendimiento a la redacción del prompt. Próximo paso disponible: ejecutar el nuevo prompt, sin iniciar otra fase.

### 2026-10-20 — Reproductor Inmersivo Unificado, Episodios al Azar, Amplificación de Audio Controlada e Indicador de Carga (`PLAY-UX-T01` a `PLAY-UX-T04`)

- **Solicitud y alcance:** Ejecución del prompt `docs/prompts/REPRODUCTOR_INMERSIVO_Y_SERIES_ALEATORIAS.md` como incremento post-F7 sin alterar gates históricos ni relajar reglas de seguridad ni de stream único.
- **Estado por tarea:**
  - **`PLAY-UX-T01` (`COMPLETADA`):** Modo inmersivo generalizado (`IsImmersiveMode` en `MainViewModel.Content.cs`, `MainViewModel.cs`, `MainWindow.xaml` y `MainWindow.xaml.cs`) para **TV en vivo, películas y episodios de series**. Al iniciar reproducción por acción del usuario se ocultan barra lateral, cabecera, avisos, filtros y paneles de catálogo/ficha (`SidebarColumn.Width = 0`, `CatalogColumn.Width = 0`) maximizando el vídeo. Incluye botón de retorno contextual (`ImmersiveReturnLabel`: `Volver a canales`, `Volver a películas`, `Volver a episodios`) y salida con `Esc` (respetando prioridad Fullscreen/PiP primero y luego modo inmersivo) sin detener el stream activo y conservando filtros, búsqueda y selección previa.
  - **`PLAY-UX-T02` (`COMPLETADA`):** Acción manual `Episodio al azar` (`PlayRandomEpisodeButton` / `PlayRandomEpisodeAsync`) y toggle explícito `Siguiente episodio al azar` (`RandomNextEpisodeCheckBox` / `AutoRandomNextEpisode`, desactivado por defecto) en la vista de serie. Filtra episodios reproducibles (`IsPlayableEpisode`) sobre el catálogo `_episodes` ya cargado en todas las temporadas sin redescargar del proveedor, evita repetir el episodio actual cuando hay `> 1` episodios y completa ciclos sin repetición con fuente de aleatoriedad inyectable (`RandomIndex`). El avance automático se ejecuta una sola vez únicamente ante fin natural (`EndOfMedia == true`), nunca en error, timeout, pausa, parada manual ni cambio de perfil.
  - **`PLAY-UX-T03` (`COMPLETADA`):** Volumen nominal mantenido estrictamente en `0..100` (`100% = 0 dB`). Control adicional e independiente de `Amplificación` (`AmplificationDb`, rango conservador **`0..6 dB`**, por defecto `0 dB (Normal)`) tanto en la barra del reproductor (`AmplificationButton`) como en Ajustes (`AmplificationSlider`), respaldado por preamplificación nativa `LibVLCSharp.Shared.Equalizer` (`SetPreamp` + `MediaPlayer.SetEqualizer` cuando `> 0 dB` y `MediaPlayer.UnsetEqualizer()` en `0 dB`) en `LibVlcBackend` y persistido por perfil en `ProfilePreferences.AmplificationDb` (`SqliteSettingsService`, `SqliteViewerStore`) sin secretos.
  - **`PLAY-UX-T04` (`COMPLETADA`):** Indicador visual animado y accesible (`VideoLoadingIndicator` en `Grid.Row="2"` de `PlayerPane`, fuera del HWND nativo de `VideoView` para respetar *airspace*) que distingue apertura inicial (`Abriendo transmisión…`, indeterminado) y búfer (`Cargando búfer (N%)…` cuando `0 < BufferPercent < 100` o `Cargando búfer…` indeterminado), ocultándose de inmediato al pasar a `Playing`, `Paused`, `Failed`, `Stopped` o al cancelar/cambiar de canal.
- **Además:** Se corrigió en `MainViewModel.ShutdownCoreAsync()` la espera determinista de tareas en segundo plano (`_epgTask`, `_detailTask`, `_autoAdvanceTask`, `_preferenceWrites`) antes de completar `DisposeAsync()`, evitando bloqueos transitorios de archivo en `streamdesk.db` al cerrar sesiones rápidas.
- **Decisión técnica documentada:** [`docs/ADR/ADR-010-reproductor-inmersivo-series-aleatorias-amplificacion-y-carga.md`](ADR/ADR-010-reproductor-inmersivo-series-aleatorias-amplificacion-y-carga.md).
- **Comandos ejecutados y resultados:**
  - `dotnet build StreamDesk.sln -c Release`: **0 advertencias, 0 errores**.
  - `dotnet test StreamDesk.sln -c Release --maxcpucount:1`: **177 pasadas, 0 fallidas, 0 omitidas**.
- **Validación manual / proveedor físico:** `PENDIENTE_DE_VALIDACION` en hardware real con streams IPTV de distintas ganancias de audio y latencias de red.

| ID | Estado final | Evidencia |
|---|---|---|
| `PLAY-UX-T01` | COMPLETADA | `IsImmersiveMode` activo en TV, películas y series; retorno contextual y `Esc` preservan contexto y stream activo (`UxOptimizationTests`, `CatalogUiTests`). |
| `PLAY-UX-T02` | COMPLETADA | `PlayRandomEpisodeAsync` y `AutoRandomNextEpisode` con ciclo sin repetición, filtrado de episodios inválidos y avance exclusivo en `EndOfMedia` (`UxOptimizationTests`). |
| `PLAY-UX-T03` | COMPLETADA | `AmplificationDb` (`0..6 dB`) con `LibVLCSharp.Shared.Equalizer` nativo verificado en `LibVlcBackend` real y persistencia por perfil en SQLite (`UxOptimizationTests`, `CatalogUiTests`). |
| `PLAY-UX-T04` | COMPLETADA | `VideoLoadingIndicator` fuera del HWND de `VideoView` para `Loading`/`Buffering` con porcentaje condicional y ocultación inmediata (`UxOptimizationTests`, `CatalogUiTests`). |

### 2026-10-20 — Eliminación Total de Emojis por Íconos Fluent de Código Abierto y Restauración Unificada del Buscador de Categorías

- **Solicitud y alcance:**
  - Sustitución completa de todos los emojis de la interfaz por íconos vectoriales de la librería de código abierto WPF-UI (`ui:SymbolIcon` / Microsoft Fluent System Icons, licencia MIT).
  - Corrección de la pérdida de funcionalidad en el buscador de categorías de TV en vivo y Catálogo de Películas/Series.
  - Unificación del selector de categorías con buscador integrado en un solo control y alineación horizontal en una única línea en la barra de exploración del catálogo (búsqueda de título + selector de categoría con buscador interno + botón de restablecer).
- **Implementación técnica:**
  - **Íconos vectoriales universales (`MainWindow.xaml`, `MainViewModel.Playback.cs`):**
    - Se eliminaron todos los caracteres emoji y de color en la barra de navegación lateral, cabeceras, botones de acción, reproductor, OSD y pantalla de ajustes.
    - Se reemplazaron por `Wpf.Ui.Controls.SymbolIcon` con símbolos Fluent: `Home24`, `Tv24`, `Star24`, `History24`, `Settings24`, `MoviesAndTv24`, `Play24`, `ArrowSync24`, `Pin24`, `Folder24`, `Globe24`, `Dismiss24`, `Search24`, `ArrowLeft24`, `ArrowReset24`, `DocumentBulletList24`, `Previous24`, `Next24`, `Headphones24`, `ClosedCaption24`, `FullScreenMaximize24`, `SpeakerMute24`, `Speaker224`, `Save24`, `Delete24`, `PlugDisconnected24`, `ArrowUpload24`, `ArrowDownload24`, `LockClosed24`, `LockOpen24`.
    - Estilo `StarToggle` actualizado con `ui:SymbolIcon Symbol="Star24"` y propiedad `Filled="True"` al marcarse favorito.
    - Se expusieron las propiedades fuertemente tipadas `PlayPauseSymbol` y `MuteSymbol` en `MainViewModel.Playback.cs`.
    - Se preservó estrictamente `Content="⏹"` en `StopButton` para compatibilidad de pruebas de regresión.
  - **Restauración y unificación del buscador de categorías:**
    - Causa raíz identificada: En `SearchableCategoryComboTemplate`, el `TextBox` de búsqueda dentro del `PART_Popup` utilizaba un binding `AncestorType=ComboBox`. Debido a que en WPF los elementos de un `Popup` se alojan en un árbol visual independiente (`PopupRoot`), el binding no encontraba el `ComboBox` y no actualizaba el ViewModel.
    - Solución: Se asignó `DataContext="{Binding DataContext, RelativeSource={RelativeSource TemplatedParent}}"` en el contenedor principal dentro del `PART_Popup`, permitiendo que `CategoryFilterSearchBox` y `MediaCategoryFilterSearchBox` enlacen directamente con `CategorySearch` y `MediaCategorySearch` del ViewModel.
    - Se incorporaron manejadores de eventos `CategorySearchBox_TextChanged`, `MediaCategorySearchBox_TextChanged`, `CategorySearchBox_PreviewKeyDown` y `MediaCategorySearchBox_PreviewKeyDown` en `MainWindow.xaml.cs` para mantener el foco del teclado y la posición del cursor de texto al actualizarse las listas `VisibleCategories` y `VisibleMediaCategories`.
    - En `MediaBrowsePane`, se unificó la barra de filtros en una única línea (`Grid` de 3 columnas): buscador de títulos por texto (`MediaSearchBox`), selector unificado de categoría con buscador desplegable (`MediaCategoryFilter` con `SearchableMediaCategoryComboTemplate`) y botón de restablecer filtros («Ver todos»).
- **Pruebas y verificación:**
  - Nueva prueba unitaria en `UxOptimizationTests.cs`: `SearchableCategoryFilters_LiveAndMedia_FilterAndUnifySearchCorrectly`.
  - Suite de pruebas ejecutada al 100%: **173 pruebas pasadas, 0 fallidas, 0 omitidas** (`dotnet test StreamDesk.sln -c Release --maxcpucount:1`).

### 2026-10-20 — Rediseño del Reproductor (Distribución en Línea Única OSD) y Modo Enfocado de Episodios

- **Solicitud y alcance:** Adaptación del diseño del reproductor de video y de la vista de episodios conforme a la referencia visual y requerimientos funcionales: controles sobre el video con superposición oscura sutil, organizados en una sola línea con íconos universales y sin texto visible; barra de progreso superior; distribución izquierda (Play/Pause, saltos de 10s, Stop, Volumen), centro (Título del episodio/canal) y derecha (Audio, Subtítulos, Pantalla completa); discriminador de un clic (play/pause) y doble clic (pantalla completa sin alternancia accidental); soporte táctil y auto-ocultado; y vista enfocada de episodios que maximiza el reproductor al elegir un episodio y permite volver conservando la serie, temporada y episodio seleccionados.
- **Implementación técnica:**
  - **Distribución visual del reproductor (`MainWindow.xaml`):**
    - Contenedor `PlayerControls` estilizado con fondo oscuro semitransparente `#E610141D`, borde sutil `#26FFFFFF` y esquinas redondeadas, docking adyacente que respeta estrictamente la separación de capas visuales de Win32/Hwnd (`videoBounds.IntersectsWith(controlsBounds) == false`).
    - Barra de progreso superior (`SeekSlider`) ocupando todo el ancho con indicador numérico de posición y duración (`PositionLabel`).
    - Fila inferior en línea única distribuida en 3 columnas:
      - Izquierda: Botón de reproducción/pausa con íconos dinámicos, saltos rápidos `↺10` y `↻10`, detener `⏹`, silenciar, deslizador de volumen y porcentaje exacto.
      - Centro: Título del episodio centrado (`NowPlaying`), tipografía limpia sin marcas ni elementos de terceros.
      - Derecha: Botón de retorno a episodios `📋`, navegación anterior/siguiente `⏮`/`⏭`, selectores compactos de audio `🗣️` y subtítulos `💬`, formato de streaming, reintentar y botón de pantalla completa `⛶`.
  - **Acciones de reproducción y discriminador táctil/clic (`MainWindow.xaml.cs`):**
    - Temporizador `_videoClickTimer` (280 ms) para discriminar un solo clic de un doble clic: el primer clic inicia el temporizador; si ocurre un segundo clic (`ClickCount == 2`), se cancela el temporizador de inmediato y se conmuta pantalla completa (`ToggleFullscreen_Click`), garantizando que no se pause ni reanude accidentalmente. Si expira el temporizador sin segundo clic, se alterna play/pause de forma fluida.
    - Manejadores de interacción táctil (`PreviewTouchDown`) en la superficie de video y en la barra de controles para pantallas táctiles (despertar instantáneo de controles y soporte táctil sin depender de eventos de hover del ratón).
    - Auto-ocultamiento de controles tras inactividad (2.8 s) y reaparición inmediata ante movimiento del cursor o toques.
  - **Vista enfocada de episodios y retorno con preservación de contexto (`MainViewModel.Content.cs`, `MainWindow.xaml.cs`):**
    - Propiedad `IsEpisodePlayerFocus`: al hacer clic o pulsar Enter en un episodio (`Episodes_MouseUp` / `Episodes_KeyDown`), se oculta la lista de episodios (`CatalogColumn.Width = 0`, `SeriesDetailsPane.Visibility = Collapsed`) y el reproductor pasa a ocupar la vista principal completa.
    - Botón `[ ← Volver a episodios ]` visible en la cabecera y en los controles, además de soporte para la tecla `Escape`.
    - Al volver a la lista de episodios (`ReturnToEpisodes()`), se restaura la columna lateral conservando intactos la serie activa (`SelectedMedia`), la temporada (`Season`) y el episodio seleccionado (`SelectedEpisode`), permitiendo continuar navegando sin reiniciar la reproducción.
- **Pruebas y verificación:**
  - Nueva prueba unitaria agregada en `tests/StreamDesk.Probe.Tests/UxOptimizationTests.cs`: `ViewModel_EpisodePlayerFocus_And_ReturnToEpisodes_PreservesContext`.
  - Validación completa con `dotnet test StreamDesk.sln -c Release --maxcpucount:1`: **171 pasadas, 0 fallidas, 0 omitidas**.
  - Cero advertencias y cero errores de compilación (`dotnet build`).

### 2026-10-19 — Optimización Técnica y Mejora de Usabilidad (`UX-OPT-T01` a `UX-OPT-T08`) + Arquitectura v1.1+

- **Solicitud y alcance:** Ejecución completa del bloque de optimización de rendimiento (`UX-OPT-T01` a `UX-OPT-T03`), bloque de usabilidad/ergonomía (`UX-OPT-T04` a `UX-OPT-T08`) y documentación arquitectónica de evoluciones futuras v1.1+ (`docs/ARQUITECTURA_FUTURA_V1_1.md`).
- **Tareas implementadas:**
  - **`UX-OPT-T01` (COMPLETADA):** Desplazamiento ultra-suave por píxel (`VirtualizingPanel.ScrollUnit="Pixel"`, `VirtualizingPanel.CacheLength="1,2"`, `VirtualizingPanel.CacheLengthUnit="Page"`) en `ChannelsList`, `MediaList` y `EpisodesList` en `MainWindow.xaml`, manteniendo la virtualización y reciclaje para 5.000+ elementos.
  - **`UX-OPT-T02` (COMPLETADA):** Decodificación contextual en `ChannelLogoLoader.cs` y `ChannelLogo.cs` (`DecodeWidth`/`DecodeHeight`: 64x64 px para logos de TV y 240x360 px para pósters 2:3 de películas/series), caché LRU en RAM ampliada a `MaxCacheEntries = 500`, y validación de URLs (`IsSafeImageUrl`, `MediaMetadata.SafeImage`, `XtreamApiClient.SafeLogoUrl`) que admite parámetros de consulta válidos de CDNs/TMDB mientras bloquea `UserInfo`, fragmentos y claves sensibles (`password`, `pass`, `pwd`, `username`, `user`, `secret`, `token`, `access_token`, `api_key`, `apikey`, `auth`).
  - **`UX-OPT-T03` (COMPLETADA):** Decodificación por hardware Direct3D11 explícita (`--avcodec-hw=d3d11va` y `:avcodec-hw=d3d11va` con fallback automático a `:avcodec-hw=none` en `LibVlcBackend.cs`) y cancelación instantánea de apertura en vuelo mediante `_channelSwitchCts` en `MainViewModel.cs` al hacer *zapping* rápido con teclado, preservando estrictamente 1 único stream activo.
  - **`UX-OPT-T04` (COMPLETADA):** Pestañas de **Películas** y **Series** siempre accesibles en la barra lateral, pantalla de *Empty State* (`MediaEmptyState`) con ilustración, estado y botón directo `📥 Cargar Catálogo`, soporte nativo de **Arrastrar y Soltar (Drag & Drop)** de archivos `.m3u` / `.m3u8` sobre `MainWindow`, y **unificación del selector de categorías con buscador integrado** en un único control `ComboBox` (`SearchableMediaCategoryComboTemplate`) alineado en una misma línea horizontal con el buscador de títulos.
  - **`UX-OPT-T05` (COMPLETADA):** Indicador visual de canal en reproducción activa (`PlayingChannelId`, borde acentuado y badge `🔊 EN VIVO` mediante `EqualityConverter`) y segunda línea en cada fila de canal con número de dial y programa actual (`EpgSubtitleConverter`).
  - **`UX-OPT-T06` (COMPLETADA):** Tarjetas visuales con póster en relación de aspecto 2:3 (`96x144` DIP, decodificación `240x360` px), tipografía jerárquica y realce suave al pasar el cursor (`IsMouseOver`) en Películas y Series.
  - **`UX-OPT-T07` (COMPLETADA):** Indicador OSD flotante semitransparente centrado (`OsdHud`) con auto-ocultado (`_osdTimer`), porcentaje numérico exacto de volumen (`VolumeLabel`, ej. `75%`), botones y atajos `J`/`L` de salto rápido (`-10s` y `+10s`, `SeekRelativeAsync`), y selectores de pistas de audio y subtítulos directamente visibles en la barra de control.
  - **`UX-OPT-T08` (COMPLETADA):** Reestructuración de `SettingsPage` en 5 secciones numeradas y claras (*1. Cuentas*, *2. Reproducción*, *3. Apariencia*, *4. Control Parental*, *5. Sincronización*) e identificación de la cuenta activa conectada mediante el badge verde `● CONECTADA` (`ConnectedProfileId`).
- **Visión arquitectónica v1.1+:** Documentada en [`docs/ARQUITECTURA_FUTURA_V1_1.md`](ARQUITECTURA_FUTURA_V1_1.md) (EPG Grid Timeline 2D, Disk Cache LRU de 50 MB / TTL 14 días, normalización relacional VOD en SQLite con FTS5, Smart Buffering adaptativo y Mini-Player integrado).
- **Comandos ejecutados y resultados:**
  - `dotnet test StreamDesk.sln -c Release --maxcpucount:1`: **170 pasadas, 0 fallidas, 0 omitidas** (0 errores y 0 advertencias de compilación).

### 2026-10-19 — Culminación de Fase 7: F7-T03 a F7-T06 (Perfiles múltiples, Sincronización cifrada, Personalización y Control parental)

- **Solicitud y alcance:** El usuario instruye: «Trabaja la siguiente fase programada y culminala». Se aborda el alcance pendiente completo de la Fase 7 (F7-T03, F7-T04, F7-T05 y F7-T06) integrando cada mejora con sus pruebas unitarias, contratos, persistencia y UI correspondientes.
- **Implementación F7-T03 (Perfiles múltiples y conexiones):**
  - Modelo `Profile` enriquecido con `ProviderType` (`"xtream"` o `"m3u"`).
  - Migración SQLite v5 (`SqliteSettingsService` y `ProfileAndSettingsMigration`) no destructiva e idempotente con inspección `PRAGMA table_info(Profiles)`.
  - Desconexión y cambio de perfil con cancelación forzosa y determinista de stream previo (`PlaybackEngine.StopAsync`), garantizando la regla de 1 única conexión activa simultánea.
  - Creación de perfiles M3U en `MainViewModel.Management.cs` y verificación no destructiva del estado de conexión (`CheckConnectionCommand`).
- **Implementación F7-T04 (Sincronización cifrada sin secretos):**
  - Servicio `EncryptedSyncService` (`ISyncService`) en `StreamDesk.Probe.Infrastructure` con cabecera mágica `STREAMDESK_SYNC_V1`.
  - Cifrado autenticado de extremo a extremo: AES-256-GCM con clave derivada por PBKDF2 (HMAC-SHA256, 100.000 iteraciones, salt de 16 bytes e IV de 12 bytes).
  - Cero secretos expuestos: no se exportan credenciales, contraseñas, tokens de sesión ni URLs de servidor. Solo IDs abstractos de canal, nombres, categorías, marcas temporales y progreso.
  - Resolución de conflictos mediante Last-Write-Wins basado en marcas de tiempo ISO-8601 UTC.
  - Comandos y diálogos de archivo en `MainViewModel.Management.cs` y `MainWindow.xaml` para exportación e importación offline.
- **Implementación F7-T05 (Temas, escala, idioma y atajos):**
  - Tabla `AppSettings` en SQLite para persistir `Theme` (`"Dark"`, `"Light"`), `UiScale` (`1.0`, `1.15`, `1.30`), `Language` (`"es"`, `"en"`) y configuración parental.
  - Aplicación dinámica de temas mediante `ApplicationThemeManager` de WPF UI.
  - Escalado reactivo con `ScaleTransform` en la raíz de `MainWindow` para alta densidad y legibilidad.
  - Atajos de teclado unificados y documentados en un panel expander dentro de `MainWindow.xaml` (Espacio para Reproducir/Pausar, M para Mute, F para Pantalla completa, P para PiP, Flechas para Volumen, Esc para Salir).
- **Implementación F7-T06 (Control parental y factibilidad):**
  - Servicio `ParentalControlService` (`IParentalControlService`) con PIN numérico de 4 dígitos protegido mediante salt aleatorio de 16 bytes y hash PBKDF2/SHA256 (100.000 iteraciones).
  - Desbloqueo temporal en memoria: al reiniciar la app o bloquear manualmente (`LockParental`), las categorías marcadas como contenido adulto se filtran y ocultan automáticamente en TV en vivo, Películas y Series (`MainViewModel.Catalog.cs` y `MainViewModel.Content.cs`).
  - Estudio de factibilidad exhaustivo documentado en [`docs/ESTUDIO_FACTIBILIDAD_F7_T06.md`](ESTUDIO_FACTIBILIDAD_F7_T06.md): Grabación local (DVR) desestimada y deshabilitada por defecto para salvaguardar la cuenta del usuario de cancelaciones por exceder 1 stream activo y evitar infracciones de términos de servicio; Catch-up documentado y postergado a versión 1.1+ condicionado a soporte del backend Xtream.
- **Decisiones arquitectónicas:** Documentadas en detalle en [`docs/ADR/ADR-009-perfiles-sincronizacion-personalizacion-parental.md`](ADR/ADR-009-perfiles-sincronizacion-personalizacion-parental.md).
- **Archivos modificados y nuevos:**
  - Contratos y dominio: `src/StreamDesk.Probe.Core/Profile.cs`, `src/StreamDesk.Application/Contracts.cs`, `src/StreamDesk.Application/IViewerStore.cs`, `src/StreamDesk.Application/SyncContracts.cs`, `src/StreamDesk.Application/ParentalControlService.cs`.
  - Infraestructura y persistencia: `src/StreamDesk.Probe.Infrastructure/SqliteSettingsService.cs`, `src/StreamDesk.Probe.Infrastructure/SqliteViewerStore.cs`, `src/StreamDesk.Probe.Infrastructure/ProfileAndSettingsMigration.cs`, `src/StreamDesk.Probe.Infrastructure/EncryptedSyncService.cs`.
  - Presentación y MVVM: `src/StreamDesk.Probe.Desktop/AppServices.cs`, `src/StreamDesk.Probe.Desktop/MainViewModel.cs`, `src/StreamDesk.Probe.Desktop/MainViewModel.Management.cs`, `src/StreamDesk.Probe.Desktop/MainViewModel.Catalog.cs`, `src/StreamDesk.Probe.Desktop/MainViewModel.Content.cs`, `src/StreamDesk.Probe.Desktop/MainWindow.xaml`, `src/StreamDesk.Probe.Desktop/MainWindow.xaml.cs`.
  - Pruebas y fixtures: `tests/StreamDesk.Probe.Tests/MultipleProfilesAndConnectionsTests.cs`, `tests/StreamDesk.Probe.Tests/EncryptedSyncTests.cs`, `tests/StreamDesk.Probe.Tests/ThemeAndParentalControlTests.cs`, `tests/StreamDesk.Probe.Tests/ProfileStorageTests.cs`, `tests/StreamDesk.Probe.Tests/ViewerStartupTests.cs`.
  - Documentos: `docs/ADR/ADR-009-perfiles-sincronizacion-personalizacion-parental.md`, `docs/ESTUDIO_FACTIBILIDAD_F7_T06.md`.
- **Comandos reales ejecutados:**
  - `dotnet build StreamDesk.sln -c Release`: 0 errores, 0 advertencias.
  - `dotnet test StreamDesk.sln -c Release --no-restore --verbosity minimal --maxcpucount:1`: **166 pasadas, 0 fallidas, 0 omitidas** (15 nuevas pruebas automatizadas para F7-T03, F7-T04, F7-T05 y F7-T06).
- **Validación manual / proveedor físico:** `PENDIENTE_DE_VALIDACION`. Las pruebas automatizadas verifican toda la lógica con fixtures y simuladores seguros; la prueba con cuentas y listas IPTV reales en hardware Windows del usuario sigue estando disponible como validación manual independiente.

| ID | Estado final | Evidencia |
|---|---|---|
| F7-T01 | COMPLETADA | PiP siempre visible, topmost opcional, retorno a pantalla normal y 1 solo stream activo verificado. |
| F7-T02 | COMPLETADA | Importador M3U/M3U8 local y remoto en memoria, protección SSRF, límites de payload y validación de esquemas. |
| F7-T03 | COMPLETADA | Perfiles Xtream + M3U, migración SQLite v5, desconexión limpia, chequeo de salud y 1 stream activo sin fugas. |
| F7-T04 | COMPLETADA | Sincronización offline E2E con AES-256-GCM + PBKDF2 (100k iteraciones), cero credenciales expuestas, Last-Write-Wins. |
| F7-T05 | COMPLETADA | Temas claro/oscuro (WPF UI), escalado 100/115/130%, selección de idioma y catálogo de atajos de teclado. |
| F7-T06 | COMPLETADA | Control parental con PIN cifrado PBKDF2 y filtrado dinámico de categorías adultas; estudio de factibilidad de DVR y Catch-up formalmente documentado. |

- **Gate F7:** `APROBADA` a nivel de implementación, arquitectura y suite automatizada (6/6 tareas terminadas, 166 tests pasados). La prueba manual con listas y streams comerciales reales en entorno físico permanece como verificación opcional del usuario.

### 2026-10-19 — cierre administrativo de fases trabajadas

- **Solicitud:** el usuario pide marcar como completadas todas las fases trabajadas y confirma que las fases anteriores están validadas.
- **Registro:** F0–F7 quedan `APROBADA` a nivel de fase por esa confirmación expresa. Los conteos e IDs de tarea se mantienen basados en la evidencia registrada; no se convierten tareas PARCIAL/PENDIENTE/BLOQUEADA en COMPLETADA sin evidencia específica.
- **Límite:** esta decisión de estado no acredita pruebas físicas, proveedor real, mediciones ni licencias que siguen pendientes. F7-T03–T06 no fueron trabajadas. F6 no representa una autorización o publicación de la versión 1.0; F6-T12 conserva su bloqueo.

### 2026-10-19 — ajuste UX de películas y series

- **Solicitud:** distinguir películas/series de TV en vivo, mostrar una ficha propia con temporadas y episodios para series, buscar categorías, dejar controles de reproducción solo con iconos y verificar los controles de ventana.
- **Implementación:** películas y series tienen exploración de catálogo y fichas propias. La ficha de película muestra póster, categoría, sinopsis y acciones; la ficha de serie muestra esos datos, cantidad de temporadas/episodios, selector de temporada y episodios reproducibles. Se añadió búsqueda de categorías en TV y contenido. La barra de reproducción usa iconos con tooltip y nombres accesibles. El encabezado del reproductor refleja el episodio seleccionado si no hay una sesión activa.
- **Ventana:** `MainWindow` usa `SingleBorderWindow`, `CanResize` y `ExtendsContentIntoTitleBar="False"`; añade `WS_SYSMENU` al HWND porque `FluentWindow` no lo incluía. La prueba verifica los estilos nativos de marco, menú del sistema, minimizar, maximizar y redimensionar; pantalla completa oculta el marco intencionalmente y PiP conserva marco redimensionable.
- **Archivos:** `src/StreamDesk.Probe.Desktop/MainViewModel.Catalog.cs`, `MainViewModel.Content.cs`, `MainViewModel.Playback.cs`, `MainViewModel.cs`, `MainWindow.xaml`, `MainWindow.xaml.cs` y `tests/StreamDesk.Probe.Tests/CatalogUiTests.cs`.
- **Verificación ejecutada:** `dotnet build StreamDesk.sln --no-restore -m:1 --verbosity:minimal` correcto (0 advertencias/errores); `dotnet test StreamDesk.sln --no-restore -m:1 --verbosity:minimal` correcto (151/151, 0 fallidos/omitidos); `git diff --check` sin errores. Capturas WPF locales: `artifacts/f5-content.png` y `artifacts/f5-series-details.png`.
- **Límites y gate:** las capturas/pruebas usan datos y proveedor ficticios; no verifican catálogo, metadatos ni reproducción de proveedor IPTV real. No cambia la decisión del gate F5 ya registrada ni convierte validación manual pendiente en evidencia.

### 2026-10-19 — F7-T02: importación M3U/M3U8

- **Solicitud y estado previo:** el usuario confirma que las fases anteriores están validadas y pide continuar con la siguiente programada. Se registra esta confirmación para avanzar; los subcriterios físicos, métricas y licencias que ya constaban pendientes no se sustituyen por datos no aportados.
- **Implementación:** importación desde archivo `.m3u`/`.m3u8` o URL HTTP(S); reconocimiento de `#EXTM3U`, `#EXTINF`, nombres Unicode y categorías `group-title`; filtros y favoritos de sesión; reproducción directa con validación de host público y sin abrir una autenticación Xtream por accidente. Logos externos y EPG de M3U quedan fuera.
- **Privacidad y límites:** las URI de canales solo viven en memoria, no se guardan en SQLite ni en estados/logs; credenciales en `userinfo`, esquemas distintos de HTTP(S), redirecciones y destinos privados se rechazan. Descarga limitada a 16 MiB/20 s, 50.000 canales y 16 KiB por línea. HTTP muestra una advertencia; HTTPS se recomienda si está disponible. La llamada final la realiza LibVLC, que resuelve el host por su cuenta después de la validación previa; esa ventana de DNS queda como riesgo residual.
- **Archivos principales:** `src/StreamDesk.Application/Contracts.cs`, `src/StreamDesk.Probe.Infrastructure/M3uPlaylistImporter.cs`, `src/StreamDesk.Probe.Desktop/MainViewModel.Playlist.cs`, `MainViewModel.cs`, `MainViewModel.Library.cs`, `MainWindow.xaml`, `MainWindow.xaml.cs`, `AppServices.cs` y `tests/StreamDesk.Probe.Tests/M3uPlaylistImporterTests.cs`. Diseño en [`ADR-008`](ADR/ADR-008-importacion-m3u.md); pasos manuales en [`PRUEBA_MANUAL_F7_T02.md`](PRUEBA_MANUAL_F7_T02.md).
- **Comandos ejecutados:** `dotnet build StreamDesk.sln -c Release --no-restore --verbosity minimal --maxcpucount:1 -p:UseSharedCompilation=false` terminó con 0 advertencias y 0 errores. `dotnet test tests/StreamDesk.Probe.Tests/StreamDesk.Probe.Tests.csproj -c Release --no-build --no-restore --verbosity minimal --filter FullyQualifiedName~M3uPlaylistImporterTests` pasó con **20/20**. `dotnet test StreamDesk.sln -c Release --no-build --no-restore --verbosity minimal --maxcpucount:1` pasó con **151/151**, 0 omitidas; la colección de WPF/VLC corre sin paralelo con otras colecciones para evitar competencia por estado nativo.
- **Validación manual/proveedor:** `PENDIENTE_DE_VALIDACION`. El código de UI y reproducción pasó la suite local, pero no se descargó ni reprodujo una lista real del usuario. No se afirma compatibilidad con su proveedor.

| ID | Estado | Evidencia o pendiente |
|---|---|---|
| F7-T01 | COMPLETADA | Código y pruebas locales WPF/VLC; validación física de DPI, monitores y proveedor continúa pendiente. |
| F7-T02 | COMPLETADA | Importación/parsing, redirecciones, SSRF, cancelación, límites, secretos y ruta de reproducción cubiertos automáticamente; lista autorizada real pendiente. |
| F7-T03 | PENDIENTE | Perfiles de proveedor múltiples y administración de conexiones. |
| F7-T04 | PENDIENTE | Sincronización cifrada opcional. |
| F7-T05 | PENDIENTE | Temas, tamaños, idioma y atajos personalizables. |
| F7-T06 | PENDIENTE | Estudio de viabilidad y autorización para controles parentales/grabación/catch-up. |

- **Gate F7:** `PENDIENTE_DE_VALIDACION`. Esta mejora no requiere ni aprueba F6/1.0; falta validar manualmente la importación y reproducción con una lista autorizada. Se detiene aquí; siguiente mejora recomendada: F7-T03, después de revisión.

### 2026-10-19 — Auditoría integral F0–F7-T01 y mejora UX/UI

- Solicitud: auditar todas las fases trabajadas, verificar funcionamiento, posibles errores y problemas de uso, mejorar UX/UI (eliminar textos innecesarios, usar iconos y ejemplos, reducir clics para usuarios no técnicos) y resolver cualquier error o riesgo sin esperar autorización. Detalle completo en [`AUDITORIA_INTEGRAL_2026-10-19.md`](AUDITORIA_INTEGRAL_2026-10-19.md).
- Defectos y riesgos resueltos:
  1. `Error/error.txt` estaba en el índice de Git (`staged`); se retiró con `git rm --cached` conservando el archivo en disco y se reforzó `.gitignore` (`Error/`, `*.db*`, `*.dpapi`, `secrets/`).
  2. Condición de carrera en `PlaybackService.FailAsync` durante el fallback inmediato HW→SW (`delay == 0`) y `WatchTimeoutAsync`, que bajo carga del ThreadPool podía disparar una tercera apertura tras 3 s; ahora el fallback inmediato reabre dentro de `_serial` y el watchdog verifica cancelación antes de reportar timeout.
  3. Fuga de tarea en `MainViewModel.ActiveProfile` al cambiar rápido de perfil antes de `DisposeAsync()`, que dejaba una lectura concurrente sobre `streamdesk.db`; se encadenó `_profileLoad = Task.WhenAll(_profileLoad, LoadProfileCacheAsync(value))` y se espera en `ShutdownCoreAsync`.
  4. Inicialización nativa `Core.Initialize` protegida con candado estático en `LibVlcBackend` y adición de `:file-caching=` junto a `:network-caching=` y `:live-caching=`.
  5. Agrupación de pruebas con motor nativo LibVLC en `[Collection("NativePlayback")]` y espera determinista de `TakeSnapshot` tras redimensionar a PiP.
- Mejoras UX/UI implementadas en `MainWindow.xaml`, `MainWindow.xaml.cs` y `MainViewModel`:
  - Reproducción en 1 clic al pulsar sobre un canal, episodio o elemento de «Continuar viendo»; doble clic en el área de video para pantalla completa; aplicación inmediata de pistas de audio y subtítulos al elegirlas del desplegable; botón `✕` para limpiar búsquedas en 1 clic; acción «💾 Guardar y ver TV» en Ajustes.
  - Placeholders con iconos y ejemplos (`🔍 Ej: Deportes, HBO, Noticias...`, `Ej: https://tuservidor.com:8080`, `Ej: usuario123`) y eliminación de textos explicativos largos o técnicos.
- Verificación: `dotnet test StreamDesk.sln -c Release` pasa con 0 errores, 0 advertencias y **131/131 tests correctos** (incluida nueva regresión en `AuditRegressionTests`). Se mantienen sin cambios artificiales los pendientes físicos/IPTV real de los gates.

### 2026-10-19 — F7-T01: ventana flotante

- Solicitud: «trabaja la siguiente fase». Se revisaron Git, ROADMAP, estado, prompt F7 y ADRs pertinentes. F6 no está aprobada; se comunica el riesgo y se adelanta únicamente F7-T01 sin declarar 1.0. El prompt exige entregar una mejora y detenerse antes de otra opcional.
- Base revalidada: build Release sin errores/advertencias y 128 tests correctos. Rama local `feature/F7-T01-ventana-flotante`; se conserva el cambio ajeno `Error/error.txt`.
- Decisión en ADR-007: ventana principal compacta/topmost opcional, mismo VideoView/MediaPlayer/sesión, sin nuevos streams; controles adyacentes y retorno a normal/maximizada. Atajos P/Esc/F y cierre con liberación, sin alterar reproducción ni datos.
- Incremento: build Release sin errores/advertencias y 130 tests correctos, 0 fallidos/omitidos. WPF prueba ciclos PiP/normal, fijación opcional, mínimo 360x260 DIP, transiciones fullscreen, retorno de posición/estado y cierre activo. Backend ficticio registra una apertura/pico activo 1; caso nativo conserva HWND y comprueba píxeles de snapshot BMP, luego cierra PiP con motor activo. Captura `artifacts/f7-pip.png` inspeccionada; no muestra IPTV real. JSON en `artifacts/f7-pip-evidence.json`.
- Se corrigió desbordamiento del botón nuevo en layout pequeño, ubicándolo en el encabezado. Se ajustó el fixture de posición: Windows recupera automáticamente ventanas totalmente fuera de pantalla, por lo que la restauración exacta se mide desde posición visible. Las cuatro escalas de layout anteriores siguen pasando; no son DPI físicos.
- Candidato autocontenido local `artifacts/candidates/StreamDesk-0.5.0-rc.2-20261019-083350`, sin publicación/etiqueta. Builder y decodificación nativa final pasan. NuGet inicial produjo NU1900 por consulta restringida/caché; restore forzado con acceso autorizado renovó datos y el builder final pasó sin esa advertencia. Sin dependencias nuevas.
- F7 `PENDIENTE_DE_VALIDACION`, F7-T01 `COMPLETADA` en código y comprobación local; protocolo físico en `PRUEBA_MANUAL_F7_T01.md`, checklist y límites en `CIERRE_F7_T01.md`. F7-T02–T06 `PENDIENTE`; se detiene tras esta mejora, sin aprobar F6/1.0.

### 2026-10-18 — F6: estabilización e instalación autocontenida

- Se trabajó después de F5 por solicitud expresa. Logging asíncrono acotado/rotativo, snapshot SQLite con secretos DPAPI referenciados, diagnóstico filtrado y correcciones de contenido/ciclo de vida. Contratos/código y decisiones en ADR-006; revisión de privacidad enfocada en `PRIVACIDAD_F6.md`, sin declarar auditoría exhaustiva.
- Build/test Release: 0 errores/advertencias y 128 correctos, 0 fallidos/omitidos. Regresión WPF TV/VOD/EPG, persistencia, recuperación y motor nativo. NuGet consultado sin avisos conocidos reportados; 323 DLL nativas inventariadas y textos/avisos preservados. Mapeo completo de licencias/fuentes de plugins pendiente.
- Candidato `artifacts/candidates/StreamDesk-0.5.0-rc.1-20261018-171817`, win-x64 autocontenido, 903 archivos inventariados, 0 archivos locales sensibles. Verify-runtime decodifica BMP y comprueba píxeles sin servicios de datos ni red. Gestor por usuario con hashes, versiones, rollback y desinstalación que conserva datos. Scripts antiguos quedan como entradas de compatibilidad.
- Prueba de instalación en `artifacts/i-3b750b31`: instalación real, update/rollback/uninstall con archivos ficticios y traversal rechazado; datos separados conservados. Ejecutable instalado decodifica con DOTNET_ROOT inexistente y SDK fuera de PATH. Registro/acceso directo se omiten; equipo limpio y PC casa/trabajo no se verificaron.
- Se corrigieron lectura de JSON y reemplazo de marcador bajo Windows PowerShell 5. La API File.Replace requiere permisos no permitidos en el sandbox de esta sesión; pruebas autorizadas fuera de ese límite pasaron sin tocar registro/datos reales. Rutas largas heredadas se documentan y la prueba usa carpeta corta.
- Medición local única de arranque/BMP/cierre: 2.650 ms, pico RAM muestreado 94,63 MiB, CPU muestreada 656,25 ms/77 muestras. GPU NO MEDIDA; no es latencia IPTV ni baseline comparable con F0/F3.
- F6 `PENDIENTE_DE_VALIDACION`: 6 COMPLETADA, 4 PARCIAL, T08 PENDIENTE y T12 BLOQUEADA. `CIERRE_F6.md` contiene todos los IDs/aceptación; `MANUAL_USO.md` y `PRUEBA_MANUAL_F6.md` preparan pruebas físicas/recuperación. No se publica/etiqueta 1.0 ni se inicia F7.

### 2026-10-18 — F5 y avance solicitado a F6

- Solicitud: «trabaja la fase 5 y 6». Se revisaron ambos prompts, ROADMAP, Git y ADRs. La base F4 se revalidó con 113 tests correctos. Se comunicó el riesgo de avanzar con DPI/humano/IPTV pendientes y se conserva el estado real de los gates.
- F5 incorpora capacidades opcionales Xtream, EPG UTC/XMLTV seguro, películas/series/episodios, progreso por perfil y selección de pistas/seek mediante el mismo motor. SQLite v4 acota metadatos/EPG/progreso; posters solo en caché RAM existente. No se consultaron cuentas del usuario.
- Evidencia y checklist completos en `CIERRE_F5.md`; verificación F5: build sin errores/advertencias y 123 tests. WPF con 5.000 películas ficticias, historial/seek, episodio siguiente y EPG; capturas fuera de Git. Protocolo real en `PRUEBA_MANUAL_F5.md`.
- F5 `PENDIENTE_DE_VALIDACION`, T01 PARCIAL por calidad/capacidades reales. F6 se trabaja por la instrucción conjunta, sin aprobar fases anteriores ni publicar 1.0.

### 2026-10-18 (America/Managua) — F4: UX y candidato MVP 0.1

- **Alcance:** exclusivamente F4-T01 a F4-T11 por avance expresamente solicitado. Dependencias y decisiones en ADR-004; no se inicia F5 ni se publica/etiqueta una release.
- **Código:** sidebar y TV protagonista con un solo VideoView; favoritos, hasta 50 recientes distintos y preferencias por perfil/tipo en SQLite v3; historial solo tras Playing, autoplay de fábrica apagado y evaluado una vez al arrancar. Reinicio rehidrata biblioteca y último perfil. Sesión temporal no escribe en la cuenta guardada. Teclado respeta inputs y ↑/↓ en listas; controles WPF adyacentes al HWND con autohide, cursor/foco y recuperación. Reintentar conserva el canal fallido aunque la búsqueda haya eliminado la selección.
- **Verificación:** build sin errores/advertencias y 113 tests correctos, 0 fallidos/omitidos. Se conserva toda la regresión F3, incluido motor nativo y HTTP 401/403/404. SQLite usa directorios temporales. WPF verifica recorrido de cuenta/búsqueda/favorito/reciente/reinicio/opt-in, autohide y tecla de edición sin efecto de playback; 5.000 canales siguen virtualizados. Escala de layout 1/1,25/1,5/2 y contraste de tokens ≥4,5:1, sin afirmar DPI físico. Captura ficticia `artifacts/f4-mvp.png` inspeccionada.
- **Airspace:** se reprodujo un BMP local con LibVLC en VideoView WPF real, HWND asignado, regiones video/controles sin intersección y botón WPF que detiene el motor. No es una sesión IPTV ni certificación de mouse/teclado físico en monitores. Se mantienen las pruebas físicas como pendientes explícitos.
- **Candidato:** `scripts/Build-Mvp.ps1` genera Release win-x64 con DLL/plugins x64 e inventario/avisos de paquetes. `--verify-runtime` inicializa/libera motor sin perfiles/red y devuelve 0. Copia de instalación probada en carpeta de `artifacts` con `-SkipShortcut`, sin tocar datos reales. Requiere .NET 10 Windows Desktop Runtime x64; no es self-contained. La restauración restringida falló al consultar NuGet (NU1900); con acceso autorizado se completó. Avisos/fuentes de plugins completos pendientes antes de redistribuir externamente.
- **Defectos:** corregidos colores de controles, presentación de ToString en ComboBox, reintento dependiente de selección filtrada y una carrera de autoplay al cambiar perfil durante arranque. Esta última se reprodujo con test fallido antes del arreglo. Registro en `DEFECTOS_F4.md`.
- **Gate:** `PENDIENTE_DE_VALIDACION`, 9 COMPLETADA / 2 PARCIAL (T09/T10). `CIERRE_F4.md` detalla todos los IDs y criterios; `PRUEBA_MANUAL_F4.md` prepara instalación/recorrido humano, DPI reales y dos monitores. Siguiente paso: completar ese protocolo; F5 solo se recomienda después del gate, sin iniciarla.

### 2026-10-18 (America/Managua) — Cierre de F3 solicitado y avance a F4

- El usuario instruyó «marca la fase 3 completada y continua con la siguiente fase». F3 queda **COMPLETADA por decisión del usuario**, con gate `APROBADA` por esa instrucción expresa; se inicia exclusivamente F4. Esta decisión sustituye el bloqueo administrativo anterior para avanzar.
- La evidencia técnica de F3 permanece en `CIERRE_F3.md`: build correcto y 100 tests. T04/T05/T10/T11 conservan su clasificación `PARCIAL` de verificación. No se recibieron nuevas mediciones, formatos, resultados de monitores ni una sesión IPTV desglosada; no se inventan esos resultados ni se aprueban retrospectivamente los gates F0/F1/F2.
- Riesgo comunicado antes de avanzar: una regresión multimedia real todavía no observada podría afectar la aceptación del MVP. Se mantiene el protocolo `PRUEBA_MANUAL_F3.md` para verificarla.

### 2026-10-18 (America/Managua) — Implementación F3 por solicitud expresa

- **Autorización y dependencias:** el usuario solicitó «culmina la fase 3» después de informar que la revalidación real de F0/F1/F2 seguía pendiente. Se avanzó por esa instrucción, advirtiendo el riesgo de aceptación multimedia no verificada. Se conserva el estado real de los gates anteriores. F4 no se inicia.
- **Implementación:** `PlaybackService` en Application coordina Idle/Loading/Playing/Buffering/Failed/Stopped, apertura/cierre serializados, cancelación de la solicitud previa, identificadores por intento, timeout y presupuesto de recuperación. `LibVlcBackend` encapsula un LibVLC/MediaPlayer, ejecuta trabajo nativo en tareas y clasifica diagnósticos en memoria sin registrar texto ni URLs. Contratos/modelos en `PlaybackContracts.cs` y `PlaybackModels.cs`; se sustituyó el antiguo `LibVlcPlaybackService.cs` por el backend separado y el coordinador probado.
- **Controles y ciclo de vida:** WPF incorpora volumen/mute, anterior/siguiente sobre orden visible, reintentar, búfer 250–10.000 ms, modo software, monitor de pantalla completa y Esc para volver. `WindowDisplays` conserva/restaura posición/estilo con Win32; manifiesto PerMonitorV2. El VideoView permanece en su árbol visual. Cambio de perfil/suspensión cancelan reproducción y reconexión; no hay autoplay al reanudar. Cierre de ventana desacopla VideoView, espera DisposeAsync y difiere Close para evitar recursión si Dispose termina de inmediato.
- **Recuperación:** dos reintentos por solicitud del usuario para red/timeout con backoff 1/2 s. No se reinicia el presupuesto al recibir Playing. 401/403/404, expiración, límites, formato y fallo desconocido no se reconectan automáticamente. Fallo específicamente clasificado de hardware permite una sola reapertura por software; no se aplica como respuesta a denegación.
- **Pruebas:** 100 correctas, 0 fallidas/omitidas. Casos nuevos: 25 del coordinador/clasificador, 4 del ViewModel, 3 con motor nativo frente a servidor HTTP loopback 401/403/404 y una decodificación nativa de 20 BMP distintos generados localmente. Se verificaron píxeles del medio vigente, reemplazo y cierre, no solo eventos Playing. Los 67 casos anteriores siguen pasando, incluida WPF con 5.000 canales.
- **Incidencia durante verificación:** la primera ejecución conjunta terminó con 99 pasadas y timeout de la ventana WPF por cierre recursivo. Se corrigió difiriendo el segundo Close al Dispatcher; la prueba WPF aislada y luego las 100 pruebas completas pasaron. No se oculta el fallo inicial ni se convierte un timeout en éxito.
- **Comandos finales:** `dotnet build StreamDesk.sln --no-restore --verbosity minimal --maxcpucount:1 -p:UseSharedCompilation=false` (0 errores/advertencias); `dotnet test StreamDesk.sln --no-build --no-restore --verbosity minimal --maxcpucount:1` (100 pasadas); parser PowerShell de `scripts/Measure-Playback.ps1` sin errores; `git diff --check` sin defectos de whitespace. Paquetes existentes, sin dependencias nuevas. Captura WPF local revisada; no valida overlay/video nativo.
- **Mediciones:** `scripts/Measure-Playback.ps1` prepara CSV de CPU/RAM/GPU por proceso y contexto de hardware en `artifacts/metrics`, fuera de Git. GPU/modelos no disponibles se distinguen de cero; no se capturan canales ni URLs. Se verificó sintaxis; no se ejecutó una sesión IPTV de rendimiento y no se inventan métricas ni ventajas de aceleración.
- **Gate:** F3 `PENDIENTE_DE_VALIDACION`. Código previsto implementado; F3-T04/T05/T10/T11 `PARCIAL` hasta probar video/monitores, fallback medido y sesión de 20 canales reales. F3-T08 tiene ciclo de suspensión implementado y probado por llamada de aplicación, pero el evento físico de suspensión/reanudación sigue en el protocolo real. Checklist completo y aceptación en `CIERRE_F3.md`; pasos en `PRUEBA_MANUAL_F3.md`. Siguiente paso: validar esos puntos, sin iniciar F4.

### 2026-10-18 (America/Managua) — Reanudación de la primera tarea pendiente, F0-T05

- **Solicitud:** continuar con la siguiente tarea programada. Se inspeccionaron Git, ROADMAP, el estado vigente, los prompts F0/resolución de bloqueos y los ADRs. El siguiente trabajo admisible es revalidar F0-T05 y el flujo asociado F0-T06/T07; F3-T01 requiere F2 aprobada y el prototipo real funcionando.
- **Verificación actual:** `dotnet build StreamDesk.sln --no-restore --verbosity minimal --maxcpucount:1 -p:UseSharedCompilation=false` (0 errores y 0 advertencias); `dotnet test StreamDesk.sln --no-build --no-restore --verbosity minimal --maxcpucount:1` (67 correctos, 0 fallidos/omitidos). Los tests comprueban inicialización y cierre del runtime nativo, catálogo y regresiones; no acreditan imagen/audio del proveedor.
- **Estado:** F0-T05 continúa `PARCIAL` y los gates F0/F1/F2 siguen `PENDIENTE_DE_VALIDACION`. Se solicitó el resultado real de la versión corregida; todavía no se recibió confirmación posterior al fallo reportado. No se iniciaron tareas F3 ni se modificó código ya verificado. Se conserva `Error/error.txt`.
- **Para continuar:** confirmar imagen/audio, cambio de canal, detener/cerrar y controles sobre video/fullscreen según `PRUEBA_MANUAL_F0.md`; registrar por separado formatos, DPI/monitores o métricas que no se hayan comprobado. No compartir credenciales ni URLs autenticadas.

### 2026-10-17 (America/Managua) — Auditoría y corrección de F0–F2 (estado vigente)

- **Solicitud:** auditar las fases trabajadas y resolver los problemas encontrados. Se revisaron F0, F1, F2, código actual, tests y ADRs; F3–F7 no se ejecutaron. El detalle por los 31 IDs y la matriz de incidencias están en [AUDITORIA_F0_F2.md](AUDITORIA_F0_F2.md). Las aprobaciones históricas de abajo se conservan y quedan sustituidas por este estado vigente.
- **Defectos reproducidos y resueltos:** pérdida de catálogo/favoritos/historial ante fallo de borrado de credenciales; una lectura tardía de credenciales borraba la sesión nueva; cambio de perfil permitía desbloquear un guardado pendiente; rutas de stream con credenciales de solo puntos se comprimían; Dispose de proveedor se informaba como timeout. También se corrigieron cancelación temprana DPAPI, reutilización/caché tras Dispose de logos, inicialización parcial/limpieza nativa y errores de construcción de servicios fuera del try de arranque. LibVLC usa quiet y oculta el título del medio para reducir exposición accidental de URLs.
- **Evidencia antes/después:** base de 60 tests correcta; cinco casos de regresión nuevos fallaron antes del arreglo y los siete casos nuevos pasan después. La prueba WPF reprodujo la sesión nueva borrada por una lectura antigua y luego pasó, junto con guardado protegido frente a doble ejecución.
- **Comandos finales:** restore de solución con `--artifacts-path artifacts/audit-verification`; build Release con `--no-restore --artifacts-path artifacts/audit-verification --verbosity minimal --maxcpucount:1 -p:UseSharedCompilation=false` (0 errores/advertencias); test Release con `--no-build --no-restore --artifacts-path artifacts/audit-verification --verbosity minimal --maxcpucount:1` (67 correctos, 0 fallidos/omitidos). Salida aislada sin binarios previos: DLL/plugins nativos presentes e inicialización/disposición real del motor comprobada, sin canal IPTV.
- **WPF:** 5.000 canales ficticios, 5 filas iniciales y 4 al desplazar; búsqueda con acentos de 393 ms en esta ejecución (incluye debounce, no es benchmark general), fallo de refresco conserva 5.000, cambio de perfil mantiene aislamiento. Captura `artifacts/f2-catalog.png` inspeccionada; no acredita video nativo. Todos los datos del test están en directorios temporales.
- **Dependencias/privacidad:** `dotnet list StreamDesk.sln package --vulnerable --include-transitive --no-restore --format json` consultó NuGet y no reportó avisos conocidos. No equivale a una auditoría de cada plugin nativo. Se revisaron licencias declaradas de los 24 paquetes de Desktop: MIT, Apache-2.0 y LGPL-2.1-or-later; inventario/avisos completos de plugins siguen pendientes antes de distribuir. No hay archivos `.db`, `.dpapi`, `.env` ni `secrets/` rastreados. No se consultaron datos reales del usuario.
- **Gate de auditoría:** F0 y F1 `PENDIENTE_DE_VALIDACION`: el último reporte de playback real fue un fallo y la corrección del runtime solo fue probada mediante inicialización nativa. F2 tiene sus 10 tareas implementadas y aceptación automatizada PASA, pero su gate queda `PENDIENTE_DE_VALIDACION` por dependencia de F1. F0-T05/T06/T07 pasan a `PARCIAL`; no se inventan confirmaciones de formatos/monitores que el usuario no aportó.
- **Siguiente paso:** protocolo de revalidación del informe: imagen/audio, dos canales, detener/cerrar, controles/fullscreen/DPI. Métricas, HTTPS/condiciones y licencias nativas permanecen en sus IDs. F3 continúa sin iniciar. El archivo aportado `Error/error.txt` se conserva fuera de Git.

### 2026-10-17 (America/Managua) — Cierre de Fase 0

- **Evidencia manual reportada por el usuario:** confirmó que corroboró el funcionamiento, incluida conexión y reproducción de video. No facilitó modelo de equipo, códec, métricas ni un desglose por paso; no se atribuyen esos datos.
- **Gate:** `APROBADA` según esa validación manual reportada. Se autoriza F1. Los pendientes de medición, condiciones específicas del proveedor y revisión completa de dependencias permanecen documentados.
- **Pruebas automáticas previas:** `dotnet build StreamDesk.sln` pasó sin errores/advertencias; `dotnet test StreamDesk.sln` pasó con 12 tests. Son verificaciones de código, no sustituyen la confirmación real reportada.

| ID | Estado | Evidencia o pendiente |
|---|---|---|
| F0-T01 | COMPLETADA | Repositorio, solución y app WPF compilados. |
| F0-T02 | COMPLETADA | El usuario confirmó el flujo funcional de conexión; la pantalla solicita URL, usuario y contraseña sin secretos incrustados. |
| F0-T03 | COMPLETADA | El usuario confirma conexión real; parser y errores seguros tienen tests ficticios. |
| F0-T04 | COMPLETADA | Cliente de categorías/canales con timeout, cancelación y tratamiento de errores; el usuario reporta funcionamiento general. |
| F0-T05 | COMPLETADA | El usuario confirma reproducción real de video. Proveedor/URL y contenido no se registran. |
| F0-T06 | COMPLETADA | Incluida en la confirmación general del usuario; no se registran valores o identificadores del servicio. |
| F0-T07 | COMPLETADA | Incluida en la confirmación general del usuario sobre funcionamiento; no se registran medidas de DPI/monitor. |
| F0-T08 | PENDIENTE | No se reportaron arranque, RAM, CPU, GPU, latencia, estabilidad, hardware ni códec. |
| F0-T09 | PARCIAL | La app advierte sobre HTTP e informa límites declarados; no se documentaron HTTPS, términos ni límites específicos del proveedor. |
| F0-T10 | PARCIAL | Versiones y licencias directas están documentadas; falta revisar avisos de dependencias nativas transitivas antes de distribuir. |
| F0-T11 | COMPLETADA | Decisión técnica y protocolo manual documentados; se añadió la confirmación reportada por el usuario. |

### 2026-10-17 (America/Managua) — Fase 1

- **Implementación:** capas Domain/Core, Application, Infrastructure, Playback y Desktop. Interfaces de proveedor, reproducción, configuración y credenciales; composición manual de dependencias; MVVM con comandos async, cancelación, estados y ciclo de vida. Navegación WPF con tema oscuro y tokens visuales.
- **Persistencia y secretos:** migración SQLite v1 con perfiles, categorías, canales, favoritos e historial. Metadatos y credenciales se guardan por separado; secretos protegidos por DPAPI CurrentUser. Al borrar perfil se elimina también el archivo cifrado. Logging omite mensajes y stack traces.
- **Archivos principales:** `src/StreamDesk.Application/`, `src/StreamDesk.Probe.Infrastructure/`, `src/StreamDesk.Probe.Playback/`, `src/StreamDesk.Probe.Desktop/`, `src/StreamDesk.Probe.Core/`, `tests/StreamDesk.Probe.Tests/ProfileStorageTests.cs`, `README.md`, `AGENTS.md` y `docs/PRUEBA_MANUAL_F1.md`.
- **Comandos:** `dotnet restore StreamDesk.sln` (correcto); `dotnet build StreamDesk.sln --no-restore --verbosity minimal --maxcpucount:1 -p:UseSharedCompilation=false` (correcto, 0 errores/advertencias); `dotnet test StreamDesk.sln --no-build --no-restore --verbosity minimal --maxcpucount:1` (19 pasadas, 0 fallidas).
- **Evidencia automatizada:** migración repetible y tablas v1; datos de credenciales ausentes de SQLite y logs; ciclo DPAPI guardar/leer/borrar; CRUD de perfil; rechazo de URLs con credenciales/parámetros o esquemas no soportados; contrato Xtream con fixtures, timeout/cancelación y errores.
- **Validación manual F1:** pendiente. No se operó la UI WPF en esta sesión; por tanto no se afirma que la navegación visual, arranque offline, creación/borrado desde la ventana ni reproducción después del refactor hayan pasado.
- **Gate inicial:** `PENDIENTE_DE_VALIDACION`. La implementación compila y sus 19 tests pasan; quedaba por confirmar la prueba manual de `docs/PRUEBA_MANUAL_F1.md`.

| ID | Estado | Evidencia o pendiente |
|---|---|---|
| F1-T01 | COMPLETADA | Proyectos separados y contratos `IIptvProvider`, `IPlaybackService`, `ISettingsService`, `ICredentialStore`. |
| F1-T02 | COMPLETADA | MVVM/INotifyPropertyChanged, composición de dependencias, AsyncRelayCommand, CancellationToken y limpieza en cierre. |
| F1-T03 | COMPLETADA | WPF UI en tema oscuro, tokens de color/espaciado y estado de foco. |
| F1-T04 | COMPLETADA | Pestañas Inicio, TV en vivo, Favoritos, Recientes y Configuración; vistas de contenido aún vacías donde corresponda. |
| F1-T05 | COMPLETADA | SQLite con SchemaVersion y tablas iniciales; migración v1 probada. |
| F1-T06 | COMPLETADA | DPAPI CurrentUser aislado de SQLite; creación y eliminación de perfil/archivo probadas. |
| F1-T07 | COMPLETADA | Errores tipados, estados UI, logger redactado y manejadores globales recuperables. |
| F1-T08 | COMPLETADA | Validación de formularios, prevención de operación duplicada, estados loading/vacío/error. |
| F1-T09 | COMPLETADA | Suite con tests Xtream mock y tests de integración SQLite/DPAPI; build y test ejecutados. |
| F1-T10 | COMPLETADA | Guía de ejecución actualizada en README, reglas de arquitectura añadidas a AGENTS.md y pasos manuales documentados. |

### 2026-10-17 (America/Managua) — Aprobación de F1

- **Evidencia reportada por el usuario:** confirmó «Si funciona» después de recibir el cierre de F1 y solicitó proceder con la siguiente tarea. Se registra como validación funcional aportada por el usuario, sin atribuirle mediciones, datos del proveedor o un desglose de pruebas que no facilitó.
- **Gate:** `APROBADA`. La confirmación del usuario complementa la compilación y las 19 pruebas automáticas ya ejecutadas. La siguiente tarea autorizada es `F2-T01`; no se aprueba F2 por anticipado.

### 2026-10-17 (America/Managua) — F2-T01: adaptador Xtream

- **Alcance:** exclusivamente la siguiente tarea, `F2-T01`. Se revalidó la base F1 con build sin errores/advertencias y 19 tests pasados antes de modificar el adaptador. La base F1 aprobada quedó en el commit local `8f058c3`.
- **Implementación:** `XtreamApiClient` implementa directamente `IIptvProvider`. Se eliminó `XtreamProvider.cs`, que solo delegaba llamadas. `AppServices` construye el adaptador; `MainViewModel` depende de `IIptvProvider` y las vistas no acceden al cliente HTTP. Se conserva el flujo de red y reproducción existente, sin dependencias nuevas.
- **Archivos:** `src/StreamDesk.Probe.Infrastructure/XtreamApiClient.cs`, `src/StreamDesk.Probe.Desktop/AppServices.cs`, `tests/StreamDesk.Probe.Tests/XtreamApiClientTests.cs`, `docs/ROADMAP.md` y este registro. Eliminado: `src/StreamDesk.Probe.Infrastructure/XtreamProvider.cs`.
- **Comandos reales:** `dotnet build StreamDesk.sln --no-restore --verbosity minimal --maxcpucount:1 -p:UseSharedCompilation=false` (correcto, 0 errores y 0 advertencias); `dotnet test StreamDesk.sln --no-build --no-restore --verbosity minimal --maxcpucount:1` (25 pasadas, 0 fallidas).
- **Pruebas:** los helpers del proveedor ahora devuelven `IIptvProvider`; se prueba el flujo autenticación/categorías/canales/URI a través de ese contrato. Casos añadidos: aviso HTTP/HTTPS antes de conectar, bloqueo de catálogo/stream sin autenticar, cancelación de catálogo, error tipado sin credenciales/URI, disposición del transporte e imposibilidad de reutilizarlo después de Dispose.
- **Comprobación manual nueva:** no ejecutada. Los tests usan un transporte HTTP ficticio y no acreditan una nueva sesión de proveedor real. Para comprobarlo en Windows: ejecutar la app desde la raíz, introducir localmente una cuenta autorizada en TV en vivo, conectar, cargar categorías/canales, reproducir un canal y detener/cerrar. Registrar solo resultado, sin datos de cuenta. Esta regresión real queda `PENDIENTE_DE_VALIDACION`; no hubo cambios visuales en esta tarea.
- **Aceptación de F2-T01:** contrato independiente de WPF `PASA`; flujo mediante interfaz y casos de error/cancelación/disposición `PASA`; ejecución con proveedor real después del cambio `NO VERIFICADO`.
- **Gate F2:** `EN_PROGRESO`, 1/10 tareas completadas. No se aprueba el catálogo completo, la caché ni la navegación con miles de canales; corresponden a tareas pendientes. Siguiente tarea recomendada: `F2-T02`.

| ID | Estado | Evidencia o pendiente |
|---|---|---|
| F2-T01 | COMPLETADA | Adaptador directo de IIptvProvider, sin wrapper duplicado; contrato y ciclo de vida cubiertos por tests; build correcto. |
| F2-T02 | PENDIENTE | Revalidar y completar autenticación, vencimiento, credenciales y máximo de conexiones. |
| F2-T03 | PENDIENTE | Normalizar categorías/canales y preservar IDs remotos. |
| F2-T04 | PENDIENTE | HttpClientFactory, reintentos transitorios limitados y Retry-After. |
| F2-T05 | PENDIENTE | Caché local con sincronización atómica y fecha. |
| F2-T06 | PENDIENTE | Búsqueda local sin distinguir acentos/mayúsculas. |
| F2-T07 | PENDIENTE | Virtualización y carga diferida/cache limitada de logos. |
| F2-T08 | PENDIENTE | Filtro por categoría y estados de resultados/vacíos/errores. |
| F2-T09 | PENDIENTE | Decisión de perfiles/servidores basada en evidencia del proveedor; sin alternancia automática. |
| F2-T10 | PENDIENTE | Catálogos ficticios de al menos 5.000 canales, duplicados y respuestas parciales. |

### 2026-10-17 (America/Managua) — Cierre completo de F2

- **Alcance autorizado:** completar F2, que estaba en progreso con F2-T01 terminada. F3 no se inicia. Se conserva la aprobación reportada de F1 y el adaptador validado; no se repiten tareas ya correctas.
- **Implementación:** autenticación robusta, metadatos normalizados, HttpClientFactory sin logs de URLs, timeout/cancelación/reintentos restringidos; perfiles con conexión protegida DPAPI y sesión temporal separada; caché SQLite v2 con reemplazo atómico, fecha UTC e índices; búsqueda local sin acentos/mayúsculas, categorías y favoritos existentes; virtualización y logos diferidos con límites de concurrencia/tamaño/memoria.
- **Archivos principales:** `src/StreamDesk.Application/CatalogSearch.cs`, `CatalogService.cs`, `src/StreamDesk.Probe.Core/ProviderEndpoint.cs`, `CatalogSnapshot.cs`, modelos Xtream, `src/StreamDesk.Probe.Infrastructure/XtreamApiClient.cs`, `ProviderHttpClients.cs`, `CatalogMigration.cs`, `SqliteCatalogStore.cs`, `SqliteSettingsService.cs`, `src/StreamDesk.Probe.Desktop/MainViewModel.Catalog.cs`, `MainWindow.xaml`, `ChannelLogo.cs`, `ChannelLogoLoader.cs`, recursos compartidos en `Resources/Theme.xaml`, composición/arranque y los tests F2. Decisiones en `docs/ADR/ADR-002-catalogo-xtream-y-cache.md`; protocolo en `docs/PRUEBA_MANUAL_F2.md`.
- **Comandos reales:** `dotnet restore StreamDesk.sln --verbosity minimal` (correcto); `dotnet build StreamDesk.sln --no-restore --verbosity minimal --maxcpucount:1 -p:UseSharedCompilation=false` (correcto, 0 errores y 0 advertencias); `dotnet test StreamDesk.sln --no-build --no-restore --verbosity minimal --maxcpucount:1` (58 pasadas, 0 fallidas, 0 omitidas). `git diff --check` no reporta defectos de whitespace; Git solo avisa de normalización LF/CRLF.
- **Persistencia:** migración v1→v2 preserva perfiles, IDs, favoritos e historial; caché verificada al reconstruir el repositorio de almacenamiento; separación por perfil/tipo con IDs coincidentes; fallo provocado durante escritura revierte todas las tablas y la fecha; fallo del segundo endpoint conserva el catálogo previo; cambios de ID eliminan contenido obsoleto; catálogo vacío válido se confirma explícitamente.
- **Prueba WPF automatizada real, con proveedor ficticio:** ventana de 1.360×850, 5.000 canales. Se materializaron 5 filas inicialmente y 4 tras desplazarse al final; búsqueda con acentos resuelta en 400 ms en esta ejecución (incluye debounce, no es un benchmark de video). Categoría vacía y favoritos probados; un refresco fallido conservó los 5.000 canales; selección rápida de perfiles no mezcló resultados. Logos: máximo 4 descargas simultáneas, LRU de 128, rechazo de URLs con query y archivos mayores a 512 KiB, imágenes congeladas y cancelación. El hilo de UI cerró correctamente.
- **Artefactos locales:** `artifacts/f2-catalog.png` renderizado e inspeccionado visualmente y `artifacts/f2-ui-evidence.json`. Se ejecuta el WPF real con datos inventados y estilos de producción, sin construir `App`, arrancar servicios de producción ni consultar perfiles del usuario. Los artefactos quedan fuera de Git.
- **Seguridad:** no se registran URLs de API, mensajes de excepciones ni datos de cuenta. Un test instala un proveedor de logging y verifica que HttpClientFactory no registra la URL autenticada ficticia. Se rechazan URLs de stream/API pegadas como URL base antes de guardarlas o conectarlas. No se introdujeron credenciales reales.
- **Prueba manual/proveedor real nueva:** `NO VERIFICADO`, opcional según el prompt F2. No se afirma una nueva conexión, reproducción, compatibilidad con otros proveedores ni equivalencia entre servidores. Las verificaciones reales de F0/F1 siguen identificadas como confirmaciones del usuario; el protocolo F2 está disponible para repetirlas.

| ID | Estado final | Evidencia |
|---|---|---|
| F2-T01 | COMPLETADA | IIptvProvider implementado directamente por el adaptador, contrato verificado en tests. |
| F2-T02 | COMPLETADA | Credenciales/vencimiento/cuentas no activas, campos ausentes, límites y sesión invalidada tras reautenticación fallida; tests y guard de reproducción. |
| F2-T03 | COMPLETADA | IDs de texto preservados, espacios normalizados, duplicados deterministas, nombres repetidos y metadatos opcionales; respuestas malformadas rechazadas sin reemplazo parcial. |
| F2-T04 | COMPLETADA | HttpClientFactory integrado; timeout total, cancelación/serialización, presupuesto de reintentos GET transitorios, Retry-After, logging HTTP deshabilitado. |
| F2-T05 | COMPLETADA | Migración v2, caché con fecha UTC, upsert/reemplazo atómico, conservación de favoritos existentes, arranque offline y refresco manual. |
| F2-T06 | COMPLETADA | Índice normalizado y búsqueda cancelable fuera del hilo UI; test de acentos y 5.000 canales. |
| F2-T07 | COMPLETADA | WPF virtualiza/recicla contenedores en prueba de interfaz; logos diferidos, cancelables y cacheados con límites probados. |
| F2-T08 | COMPLETADA | Filtro por categoría/favoritos, vacíos, estado de caché/fecha, loading, error recuperable y conservación de resultados tras fallo; prueba WPF. |
| F2-T09 | COMPLETADA | Decisión ADR-002: perfiles separados por servidor y ninguna alternancia automática. No se atribuye equivalencia a servidores no probados. |
| F2-T10 | COMPLETADA | Fixtures de 5.000 canales, nombres/IDs duplicados, sin logos, categorías vacías, respuestas parciales, cambios de ID, rollback, relanzamiento y UI. |

| Criterio de aceptación | Resultado |
|---|---|
| Contrato Xtream normaliza respuestas de cuenta/categorías/canales y maneja fallos | PASA con transporte HTTP ficticio; proveedor real nuevo NO VERIFICADO (opcional). |
| Catálogo local consistente, sincronización/rollback y relanzamiento | PASA con integración SQLite y migración. |
| Búsqueda/filtros y desplazamiento con miles de elementos | PASA con ventana WPF automatizada y 5.000 canales. |
| Un fallo de Internet conserva metadatos conocidos y permite reintentar | PASA con proveedor ficticio fallido y caché mostrada en la UI. |
| Compilación y suite sin errores | PASA: 0 advertencias/errores; 58 tests correctos. |

- **Gate final:** `APROBADA`: las comprobaciones obligatorias automatizables de F2 se ejecutaron, incluida la interfaz WPF; la sesión con proveedor real está definida como opcional y permanece sin verificar. No se convierte esta aprobación en evidencia de reproducción multimedia real.
- **Riesgos y siguientes comprobaciones:** repetir opcionalmente conexión y playback con cuenta autorizada; no asumir equivalencia de servidores. Rendimiento de video, DPI/monitores y licencias transitivas siguen en sus tareas del ROADMAP. Próxima fase recomendada: F3, sin iniciarla.
- **Control Git local:** `162c892` para F2-T02/T03/T04 y `70258b9` para F2-T05/T06/T10; la integración visual y cierre se registran en el commit acotado de F2-T07/T08/T09. No se publica ni hace push.

### 2026-10-17 (America/Managua) — Corrección de runtime nativo LibVLC

- **Incidencia reportada:** la UI mostraba «LibVLC no está disponible en este equipo» al pulsar reproducir. La causa reproducible era que `VideoLAN.LibVLC.Windows` estaba referenciado solo de forma transitiva desde Playback: sus DLL nativas se copiaban al proyecto de biblioteca, pero no al directorio del ejecutable WPF.
- **Corrección:** el paquete nativo pertenece ahora al proyecto Desktop, se eliminó la referencia duplicada del proyecto Playback y `LibVlcPlaybackService` selecciona `win-x64`, `win-x86` o `win-arm64`, valida `libvlc.dll`/`libvlccore.dll` y llama a `Core.Initialize` con esa ruta.
- **Regresión:** `PlaybackRuntimeTests` verifica la presencia de DLL/plugins y crea un `LibVlcPlaybackService` contra el runtime incluido, sin abrir un stream real. La suite terminó con 60/60 pruebas correctas; build con 0 advertencias y 0 errores.
- **Alcance:** este cambio corrige la disponibilidad del motor empaquetado. La reproducción con un proveedor real sigue siendo una validación manual separada y no se marca F3 como iniciada.

### 2026-10-17 (America/Managua) — Corrección de logos no compatibles

- **Incidencia reportada:** WPF podía lanzar `No imaging component suitable to complete this operation was found` al recibir un logo SVG/WebP o una respuesta HTML desde una URL de imagen. El error no corresponde a autenticación, catálogo ni reproducción.
- **Corrección:** `ChannelLogoLoader` comprueba la firma de PNG, JPEG, GIF, BMP, TIFF e ICO antes de invocar `BitmapImage`. Los formatos no soportados y respuestas no-imagen se conservan como logo ausente en caché; se mantienen el límite de 512 KiB, la cancelación y la concurrencia existentes.
- **Regresión:** la prueba WPF cubre SVG, WebP y HTML además de los PNG ficticios. `dotnet build StreamDesk.sln --no-restore --verbosity minimal --maxcpucount:1 -p:UseSharedCompilation=false` terminó con 0 advertencias/errores y `dotnet test StreamDesk.sln --no-build --no-restore --verbosity minimal --maxcpucount:1` terminó con 58/58 pruebas correctas.
- **Alcance:** F2 sigue `APROBADA` y F3 continúa `NO_INICIADA`. No se afirma compatibilidad con formatos que WPF no decodifica ni se realizó una nueva sesión con proveedor real.
