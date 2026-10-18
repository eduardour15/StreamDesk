# StreamDesk — Registro de ejecución por fases

Este archivo registra trabajo y evidencias reales. La confirmación del usuario se identifica como tal y no se completa con datos supuestos.

| Fase | Gate | Tareas completas | Última revisión | Bloqueos o dependencias |
|---|---|---:|---|---|
| F0 | PENDIENTE_DE_VALIDACION | 5/11 | 2026-10-18 | Reanudada F0-T05: build y 67 tests correctos; sigue pendiente confirmar video real después de corregir LibVLC. F0-T05/T06/T07 PARCIAL. Métricas, condiciones del proveedor y avisos nativos pendientes. |
| F1 | PENDIENTE_DE_VALIDACION | 10/10 | 2026-10-17 | Código y regresiones corregidos; falta confirmar preservación del playback real actual. |
| F2 | PENDIENTE_DE_VALIDACION | 10/10 | 2026-10-17 | 67 tests en salida Release aislada, incluida WPF con 5.000 canales. Verificación técnica PASA; cierre condicionado a revalidación previa F0/F1. |
| F3 | APROBADA | 7/11 verificadas; cierre solicitado | 2026-10-18 | COMPLETADA por decisión expresa del usuario. Se conservan las verificaciones físicas/IPTV/mediciones pendientes de T04/T05/T10/T11; esta decisión no constituye evidencia de esas pruebas. |
| F4 | PENDIENTE_DE_VALIDACION | 9/11 | 2026-10-18 | MVP implementado, 113 tests y candidato win-x64 local. T09/T10 PARCIAL: DPI físicos/dos monitores y evaluación humana/IPTV pendientes. |
| F5 | NO_INICIADA | 0/9 | — | Requiere F4 aprobada; EPG/VOD condicional |
| F6 | NO_INICIADA | 0/12 | — | Requiere MVP estable |
| F7 | NO_INICIADA | 0/6 | — | Opcional después de 1.0 |

## Bitácora de evidencias

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
