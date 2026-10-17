# StreamDesk — Registro de ejecución por fases

Este archivo registra trabajo y evidencias reales. La confirmación del usuario se identifica como tal y no se completa con datos supuestos.

| Fase | Gate | Tareas completas | Última revisión | Bloqueos o dependencias |
|---|---|---:|---|---|
| F0 | APROBADA | 8/11 | 2026-10-17 | Métricas de hardware/códec, verificación de condiciones del proveedor y revisión completa de licencias siguen pendientes; no bloquean el gate funcional reportado. |
| F1 | APROBADA | 10/10 | 2026-10-17 | Validación funcional confirmada por el usuario; sin desglose manual por paso. |
| F2 | APROBADA | 10/10 | 2026-10-17 | Build y 58 tests correctos, incluida WPF con 5.000 canales ficticios. Prueba opcional con proveedor real pendiente. |
| F3 | NO_INICIADA | 0/11 | — | Sin iniciar; F2 ya aprobada |
| F4 | NO_INICIADA | 0/11 | — | Requiere F3 aprobada |
| F5 | NO_INICIADA | 0/9 | — | Requiere F4 aprobada; EPG/VOD condicional |
| F6 | NO_INICIADA | 0/12 | — | Requiere MVP estable |
| F7 | NO_INICIADA | 0/6 | — | Opcional después de 1.0 |

## Bitácora de evidencias

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

### 2026-10-17 (America/Managua) — Corrección de logos no compatibles

- **Incidencia reportada:** WPF podía lanzar `No imaging component suitable to complete this operation was found` al recibir un logo SVG/WebP o una respuesta HTML desde una URL de imagen. El error no corresponde a autenticación, catálogo ni reproducción.
- **Corrección:** `ChannelLogoLoader` comprueba la firma de PNG, JPEG, GIF, BMP, TIFF e ICO antes de invocar `BitmapImage`. Los formatos no soportados y respuestas no-imagen se conservan como logo ausente en caché; se mantienen el límite de 512 KiB, la cancelación y la concurrencia existentes.
- **Regresión:** la prueba WPF cubre SVG, WebP y HTML además de los PNG ficticios. `dotnet build StreamDesk.sln --no-restore --verbosity minimal --maxcpucount:1 -p:UseSharedCompilation=false` terminó con 0 advertencias/errores y `dotnet test StreamDesk.sln --no-build --no-restore --verbosity minimal --maxcpucount:1` terminó con 58/58 pruebas correctas.
- **Alcance:** F2 sigue `APROBADA` y F3 continúa `NO_INICIADA`. No se afirma compatibilidad con formatos que WPF no decodifica ni se realizó una nueva sesión con proveedor real.
