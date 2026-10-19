# Auditoría Integral de Fases (F0–F7-T01) y Mejora UX/UI — 2026-10-19

## 1. Resumen Ejecutivo

Se auditó la totalidad del código, pruebas, empaquetado y documentación trabajados en las fases **F0, F1, F2, F3, F4, F5, F6 y F7-T01**. Durante la auditoría técnica y funcional se detectaron y corrigieron **5 riesgos/errores reales** de seguridad de repositorio, concurrencia, ciclo de vida y reproducción nativa, además de realizar una **renovación integral de UX/UI** enfocada en usuarios no técnicos, iconografía clara, ejemplos visuales en campos (`Tag` watermarks) y reducción drástica de clics.

- **Compilación Release**: `0` advertencias, `0` errores.
- **Suite de pruebas automatizadas Release**: `131 / 131` pruebas superadas (`0` fallidas, `0` omitidas), incluyendo pruebas WPF de 5.000 canales/películas, escalado 100%–200%, contraste WCAG ≥ 4.5:1, ventana flotante (PiP) y decodificación nativa con LibVLC.

---

## 2. Estado Auditado por Fase

| Fase | Alcance Auditado | Estado Técnico / Automatizado | Pendientes Físicos / Externos (No Inventados) |
|---|---|---|---|
| **F0** | Prototipo de conexión Xtream y reproducción LibVLC | **PASA** (inicialización nativa multi-arquitectura, decodificación de 20 medios locales y HTTP loopback 401/403/404) | Confirmación manual posterior con proveedor IPTV real y métricas prolongadas (F0-T05/T06/T07/T08). |
| **F1** | Arquitectura limpia (Core, Application, Infrastructure, Playback, Desktop), SQLite v1 y DPAPI | **PASA** (10/10 tareas, separación estricta SQLite/DPAPI, redacción de logs y borrado transaccional) | Ninguno en código. |
| **F2** | Catálogo Xtream, normalización, caché SQLite v2, búsqueda sin acentos y virtualización WPF (5.000 canales) | **PASA** (10/10 tareas, reemplazo atómico, LRU de logos de 128 entradas con límite de 512 KiB y validación de cabeceras de imagen) | Ninguno en código. |
| **F3** | Motor de reproducción, watchdog de apertura, fallback HW→SW único, reconexión acotada y pantalla completa | **PASA** (resuelta condición de carrera en fallback inmediato y sincronización de inicialización nativa) | Validación física en 2 monitores reales y sesión IPTV de 20 canales (F3-T04/T05/T10/T11). |
| **F4** | UX MVP 0.1, favoritos, historial de 50 recientes, preferencias por perfil, autohide de controles y atajos | **PASA** (resuelta condición de carrera al cambiar rápido de perfil antes de cerrar/disponer SQLite) | Verificación en monitores con DPI físico real (F4-T09/T10). |
| **F5** | VOD (Películas/Series/Episodios), EPG UTC + importador XMLTV seguro, continuar viendo, seek y pistas | **PASA** (caché SQLite v4 acotada, validación XXE/entidades XMLTV, autoselección de episodio inicial y cambio de pista en 1 paso) | Prueba con catálogo VOD/EPG de proveedor real (F5-T01). |
| **F6** | Estabilización, logs rotativos acotados, backup DPAPI+SQLite, diagnóstico redactado e instalador autocontenido | **PASA** (resuelto riesgo de `Error/error.txt` en índice Git y exclusiones en `.gitignore`) | Mapeo completo de licencias de plugins nativos de terceros antes de distribución pública (F6-T08/T12). |
| **F7-T01** | Ventana flotante / Picture-in-Picture (PiP) con mismo `VideoView`/`MediaPlayer` y 1 solo stream activo | **PASA** (5 ciclos PiP↔Normal, transiciones Fullscreen↔PiP, tamaño mínimo 360×260 DIP y snapshot nativo sincronizado) | Prueba manual con proveedor IPTV real. |

---

## 3. Errores y Riesgos Detectados y Resueltos

1. **Riesgo de privacidad/higiene en Git (`Error/error.txt` en stage y `.gitignore` incompleto)**:
   - **Problema**: `Error/error.txt` estaba añadido al índice de Git (`A  Error/error.txt`) y `.gitignore` no excluía `Error/`, `*.db*`, `*.dpapi` ni `secrets/`.
   - **Solución**: Se retiró `Error/error.txt` del índice con `git rm --cached Error/error.txt` conservando intacto el archivo local en disco, y se blindó [`.gitignore`](file:///C:/Users/PROGRAMADOR2/Documents/IPTV%20PLAYER/.gitignore).
2. **Condición de carrera en fallback inmediato de hardware y watchdog (`PlaybackService.cs`)**:
   - **Problema**: En [`PlaybackService.cs`](file:///C:/Users/PROGRAMADOR2/Documents/IPTV%20PLAYER/src/StreamDesk.Application/PlaybackService.cs), cuando ocurría un fallo de decodificación por hardware (`delay == TimeSpan.Zero`), `FailAsync` delegaba la reapertura en software a una tarea en segundo plano (`ReopenAsync`) fuera del semáforo `_serial`, permitiendo que el watchdog de la sesión previa o el planificador del ThreadPool provocaran una tercera apertura o un timeout espurio tras 3 segundos.
   - **Solución**: El fallback inmediato (`delay == TimeSpan.Zero`) ejecuta ahora `await OpenAsync(session)` directamente dentro de la sección crítica `_serial`, y `WatchTimeoutAsync` verifica `!token.IsCancellationRequested` antes de invocar `FailAsync`.
3. **Fuga de tareas de carga de perfil al cambiar rápidamente de cuenta (`MainViewModel.Catalog.cs` y `MainViewModel.cs`)**:
   - **Problema**: En [`MainViewModel.Catalog.cs`](file:///C:/Users/PROGRAMADOR2/Documents/IPTV%20PLAYER/src/StreamDesk.Probe.Desktop/MainViewModel.Catalog.cs), asignar `ActiveProfile` sobrescribía `_profileLoad = LoadProfileCacheAsync(value)` sin encadenar la tarea anterior, y `ShutdownCoreAsync` no esperaba a `_profileLoad`. Al cambiar de perfil durante el arranque y cerrar la vista, la primera lectura seguía accediendo a `streamdesk.db`, bloqueando el archivo SQLite.
   - **Solución**: Se encadenó `_profileLoad = Task.WhenAll(_profileLoad, LoadProfileCacheAsync(value))`, se añadió `await _profileLoad` en `ShutdownCoreAsync` y se cancelan `_mediaFilter`, `_detailRequest` y `_epgRequest` en `DisposeCatalog()`.
4. **Inicialización nativa concurrente y caché de archivos locales en LibVLC (`LibVlcBackend.cs`)**:
   - **Problema**: `LibVLCSharp.Shared.Core.Initialize(directory)` podía ejecutarse en paralelo sin sincronización estática, y `CreateMediaOptions` configuraba `:network-caching=` y `:live-caching=` pero no `:file-caching=`.
   - **Solución**: Se protegió `Core.Initialize` con un `lock (CoreSync)` estático en [`LibVlcBackend.cs`](file:///C:/Users/PROGRAMADOR2/Documents/IPTV%20PLAYER/src/StreamDesk.Probe.Playback/LibVlcBackend.cs) y se añadió `:file-caching=`.
5. **Contención de pruebas nativas LibVLC en paralelo (`PlaybackRuntimeTests`, `NativeHttpPlaybackTests`, `CatalogUiTests`)**:
   - **Problema**: xUnit ejecutaba en paralelo tres clases que instancian LibVLC nativo simultáneamente, causando contención en el renderizador de video al tomar capturas instantáneas tras redimensionar a PiP.
   - **Solución**: Se agruparon bajo `[Collection("NativePlayback")]` y se sincronizó la captura `TakeSnapshot` tras el redimensionamiento PiP en [`CatalogUiTests.PictureInPicture.cs`](file:///C:/Users/PROGRAMADOR2/Documents/IPTV%20PLAYER/tests/StreamDesk.Probe.Tests/CatalogUiTests.PictureInPicture.cs).

---

## 4. Mejoras de UX/UI y Usabilidad Aplicadas

- **Menos clics para tareas frecuentes**:
  - **1 clic para ver TV**: Hacer clic directamente sobre cualquier canal en `TV en vivo`, `Favoritos` o `Recientes` inicia su reproducción al instante (sin necesidad de seleccionarlo primero y luego pulsar un botón inferior). Se eliminó el botón redundante `"Reproducir seleccionado"`.
  - **1 clic en episodios y "Continuar viendo"**: Hacer clic en un episodio o en un elemento de `"Continuar"` reproduce inmediatamente el contenido; al elegir serie o temporada se preselecciona automáticamente el primer episodio disponible.
  - **Doble clic en video para Pantalla Completa**: Hacer doble clic sobre el área de video alterna pantalla completa (`F` / `Esc`).
  - **Cambio de audio y subtítulos en 1 paso**: Seleccionar una pista en los desplegables `🗣️ Audio` o `💬 Subtítulos` la aplica automáticamente al cerrar el desplegable (`DropDownClosed`), eliminando los botones adicionales `"Aplicar audio"` y `"Aplicar subtítulo"`.
  - **Botón `"💾 Guardar y ver TV"` en Ajustes**: Guarda la cuenta nueva, la activa, cambia a la pestaña `TV en vivo` y descarga los canales en un solo clic.
  - **Botón rápido `✕` en buscadores**: Permite limpiar la búsqueda de canales o películas/series con 1 clic.
- **Iconografía y ejemplos visuales (Watermarks)**:
  - Navegación clara con iconos: `🏠 Inicio`, `📺 TV en vivo`, `⭐ Favoritos`, `🕘 Recientes`, `⚙️ Ajustes`, `🎬 Películas`, `🎞️ Series`, `▶️ Continuar`.
  - Campos de texto con ejemplos integrados (`Tag` placeholder):
    - Buscador de canales: `🔍 Ej: Deportes, HBO, Noticias...`
    - Buscador VOD: `🔍 Ej: Acción, Comedia, Matrix...`
    - Nombre de cuenta: `Ej: TV Sala o Mi Proveedor`
    - Servidor: `Ej: https://tuservidor.com:8080`
    - Usuario: `Ej: usuario123`
- **Eliminación de textos técnicos y explicaciones excesivas**:
  - Se reemplazaron párrafos técnicos sobre DPAPI, búfer en milisegundos, decodificación por software, advertencias extensas y notas sobre pistas declaradas por etiquetas breves, amigables y orientadas a la acción.
  - En el encabezado del reproductor se muestra ahora el programa actual (`EpgNow`) junto al nombre del canal activo para consultarlo de un vistazo sin abrir paneles adicionales.
