# Auditoría y correcciones de F0, F1 y F2

Fecha: 2026-10-17, America/Managua. Base auditada: `7907356`, con 60 tests correctos antes de añadir regresiones. Alcance autorizado: revisar y corregir las fases existentes; F3–F7 siguen sin iniciar. Se conservó `Error/error.txt`, aportado por el usuario y no rastreado.

## Veredicto

Los defectos de código descritos abajo quedaron corregidos y la solución pasa 67 tests desde una salida Release nueva. El gate vigente de F0/F1/F2 es `PENDIENTE_DE_VALIDACION`: después del último fallo real de LibVLC reportado, se comprobó el empaquetado y la inicialización del motor, pero no se ejecutó una nueva reproducción IPTV. No basta esa inicialización para aprobar imagen/audio, cambio, liberación de conexiones ni controles encima del video.

F2 conserva 10/10 tareas implementadas y sus comprobaciones obligatorias automatizadas pasan. Su gate queda condicionado a revalidar el gate anterior de F1; la nueva sesión real de catálogo sigue siendo opcional para F2. Las aprobaciones antiguas permanecen como historial en `ESTADO_FASES.md`.

## Incidencias y resolución

Severidad: alta = pérdida de datos/sesión o gate incorrecto; media = operación inválida/error incorrecto; baja = limpieza y defensa de caminos de error. Se distingue una reproducción automatizada de un defecto observado por lectura del código.

| ID | Severidad / fase | Reproducción o evidencia | Resolución / comprobación |
|---|---|---|---|
| A01 | Alta, F1-T06 | Guardar perfil con catálogo/favoritos/historial; provocar IOException al borrar credenciales. Antes, se restauraba solo Profiles después del DELETE CASCADE y desaparecía CatalogSync. Test falló antes del arreglo. | `ProfileService.DeleteAsync` elimina primero el secreto, conserva backup solo en memoria y lo restaura si SQLite falla. Pruebas de fallo del secreto y trigger SQLite verifican perfil, credenciales, catálogo, favoritos e historial intactos. |
| A02 | Alta, F1-T02/F2-T05 | Retrasar GetAsync de credenciales de A, cambiar/conectar B y completar la lectura A ignorando cancelación. La prueba WPF reprodujo ConnectionSummary=null para B. | Token/versión se validan antes de tocar el estado de autenticación; una conexión temporal inicia una versión nueva y cancela filtros previos. WPF conserva B, su formato y 20 canales; solo se crea el proveedor vigente. |
| A03 | Media, F1-T08 | `LoadProfileCacheAsync` ponía IsBusy=false aunque CreateProfileAsync/Delete estuvieran pendientes. Evidencia en código y regresión WPF con SaveAsync retenido. | El estado de operaciones de perfiles se mantiene separado del de red/catálogo. Cambiar de perfil conserva el bloqueo; segundo intento de guardado no duplica SaveAsync. |
| A04 | Media, F0-T03/F2-T01 | Autenticar con usuario ficticio `.` o `..` y construir ruta. UriBuilder comprimía segmentos de la ruta. Ambos casos fallaron antes. | Los segmentos variables se escapan; los puntos se codifican y se conserva la ruta ya validada. Los dos casos y el test existente de slash escapado pasan. No se admite texto de usuario sin escapar en Path/Query. |
| A05 | Media, F2-T04 | Iniciar una petición ficticia y llamar Dispose al adaptador. Se producía XtreamApiException(RequestTimedOut). Test falló antes. | La cancelación por cierre conserva OperationCanceledException, como la cancelación del llamante; el timeout real conserva su error tipado. |
| A06 | Baja, F2-T07 | El loader no diferenciaba uso después de Dispose, conservaba su CTS operativo y Remember podía guardar una imagen decodificada al mismo tiempo que el cierre. El contrato de uso tras Dispose falló en el test nuevo. | Cierre idempotente, rechazo explícito de uso nuevo y no inserción en caché una vez cerrado. La fila WPF tolera el cierre del loader durante Unloaded. |
| A07 | Media, F0-T06/F1-T02 | Por inspección: si crear MediaPlayer fallaba, LibVLC quedaba asignado parcialmente; si Play lanzaba, Media no se liberaba; Stop podía impedir el resto de Dispose. | Asignación del motor solo tras inicialización completa y limpieza en catch/finally. Se verifica inicialización repetida, mismo handle, Stop, Dispose repetido y rechazo tras cierre con el runtime nativo real. Las excepciones nativas de asignación no se inyectaron. |
| A08 | Media, F1-T07 | `new AppServices(directory)` estaba fuera del try de OnStartup; un error de permisos al crear el directorio eludía el mensaje de arranque. Evidencia por inspección. | La construcción entra en el try y el logging tolera servicios todavía ausentes. Verificado por compilación e inspección; no se alteraron permisos de datos reales para provocar el fallo. |
| A09 | Baja, F1-T06 | SaveAsync DPAPI no comprobaba cancelación antes de preparar directorio/cifrado. | Se comprueba antes de empezar y antes del reemplazo final. El test cancelado no deja archivos. |
| A10 | Alta, registro/gates | Las tablas seguían APROBADA tras el reporte de fallo real de reproducción; confirmaciones generales históricas se atribuían a DPI/monitores sin evidencia específica. | Se corrige el estado vigente sin borrar historial. F0-T05/T06/T07 PARCIAL; F0/F1 pendientes de revalidación y F2 pendiente por dependencia. |

La corrección anterior del runtime (`7907356`) se revalidó desde carpetas de build nuevas: Desktop recibe `libvlc.dll`, `libvlccore.dll` y plugins. No depende de DLL sobrantes en Playback. La corrección de logos anterior se conserva: las respuestas SVG/WebP/HTML ficticias son opcionales y se omiten. No se dispuso del cuerpo del logo que produjo la traza original, por lo que no se atribuye un formato concreto al proveedor real.

El motor se inicia con `--quiet` y `--no-video-title-show`: se reduce la posibilidad de que logging nativo o el título del medio muestren rutas autenticadas. El logger de aplicación sigue guardando solo ID técnico y nombre de tipo de excepción; HttpClientFactory mantiene sus loggers deshabilitados. Esto no es evidencia de una sesión real ni un análisis de tráfico del proveedor.

La eliminación entre SQLite y DPAPI usa compensación, no una transacción distribuida: una pérdida de energía o un segundo fallo al restaurar credenciales requiere recuperación. Ante fallo de lectura del secreto se aborta sin borrar datos. No se descifra ni modifica la cuenta real durante las pruebas.

## Evidencia reproducible

Ejecutado desde la raíz:

```powershell
dotnet restore StreamDesk.sln --artifacts-path artifacts/audit-verification --verbosity minimal
dotnet build StreamDesk.sln --no-restore -c Release --artifacts-path artifacts/audit-verification --verbosity minimal --maxcpucount:1 -p:UseSharedCompilation=false
dotnet test StreamDesk.sln --no-build --no-restore -c Release --artifacts-path artifacts/audit-verification --verbosity minimal --maxcpucount:1
dotnet list StreamDesk.sln package --vulnerable --include-transitive --no-restore --format json
git diff --check
```

Build: 0 errores y 0 advertencias. Tests: 67 correctos, 0 fallidos, 0 omitidos. También se recompiló la salida Debug habitual con 0 errores/advertencias. Base previa: 60 correctos. Siete casos nuevos en `AuditRegressionTests` (cinco fallaron antes de corregir); se amplió también la prueba WPF y la de ciclo nativo. Los intentos iniciales de build aislado sin restore y consulta NuGet sin red fallaron por entorno; tras restore y acceso de red aprobado finalizaron correctamente. Correcciones registradas en el commit local `cc66ec8`, sin push.

La ventana WPF con recursos de producción y datos ficticios comprueba 5.000 canales, 5 filas materializadas inicialmente y 4 al desplazar al final, búsqueda con acentos en 393 ms en esta ejecución (incluye debounce), favoritos existentes, categoría vacía, refresco fallido conservando 5.000 canales, aislamiento de perfiles, lectura tardía de credenciales y guardado sin doble ejecución. El hilo termina. `artifacts/f2-catalog.png` fue inspeccionado; la captura no verifica video nativo.

Los tests SQLite comprueban migración v1→v2, claves por perfil/tipo, índices, rollback, preservación de favoritos y datos al fallar operaciones. DPAPI usa CurrentUser con fixtures temporales. El test nativo crea el motor y lo dispone sin abrir canales; no se usaron perfiles ni secretos reales. El inventario de archivos rastreados no contiene `.db`, `.dpapi`, `.env` ni `secrets/`.

NuGet no reportó vulnerabilidades conocidas en los paquetes consultados en esta fecha. Es un chequeo de avisos, no una garantía sobre todas las DLL/plugins incluidas. Se inspeccionaron los nuspec de los 24 paquetes resueltos de Desktop: WPF-UI y paquetes Microsoft declaran MIT; SQLitePCLRaw declara Apache-2.0; LibVLCSharp, LibVLCSharp.WPF y VideoLAN.LibVLC.Windows declaran LGPL-2.1-or-later. Falta inventariar licencias/avisos de todos los componentes nativos antes de redistribuir; F0-T10 sigue PARCIAL.

Fuentes técnicas primarias consultadas: [comportamiento de canonicalización de Uri en .NET](https://learn.microsoft.com/en-us/dotnet/api/system.uricreationoptions.dangerousdisablepathandquerycanonicalization?view=net-10.0) y [opciones de VLC 3](https://github.com/videolan/vlc/blob/3.0.x/src/libvlc-module.c). Las conclusiones de funcionamiento provienen del código y los comandos ejecutados.

## Checklist por tarea

| ID | Estado vigente | Artefacto / caso feliz y error / límite |
|---|---|---|
| F0-T01 | COMPLETADA | StreamDesk.sln y Desktop WPF; build aislado correcto. |
| F0-T02 | COMPLETADA | MainWindow: URL/usuario/PasswordBox, vaciado de contraseña tras acción; sin valores reales incrustados. |
| F0-T03 | COMPLETADA | XtreamApiClient y tests de cuenta, auth, vencimiento, límites, formatos y URI; conexión histórica reportada por el usuario. |
| F0-T04 | COMPLETADA | Categorías/canales, cancelación, timeout/reintentos, errores tipados y fixtures; carga histórica reportada. |
| F0-T05 | PARCIAL | Playback funcional implementado y motor inicializado; falta imagen/audio actual tras corrección y desglose ts/HLS si ambos se ofrecen. |
| F0-T06 | PARCIAL | Stop/Dispose y un MediaPlayer comprobados sin red IPTV; falta cambiar/detener/cerrar un canal real sin conexión sobrante. |
| F0-T07 | PARCIAL | Controles en VideoView y fullscreen en MainWindow; falta validar airspace, DPI y monitores sobre video actual. |
| F0-T08 | PENDIENTE | Sin mediciones reales de arranque/video/RAM/CPU/GPU con hardware/códec. |
| F0-T09 | PARCIAL | Aviso HTTP y límites declarados probados; HTTPS/condiciones del proveedor no comprobados. |
| F0-T10 | PARCIAL | Versiones/licencias NuGet revisadas; falta inventario/avisos completos de plugins nativos antes de distribuir. |
| F0-T11 | COMPLETADA | DECISION_TECNICA_F0, protocolos, evidencia de build/test y registro histórico corregido por auditoría. |
| F1-T01 | COMPLETADA | Cinco capas, contratos en Application y composición AppServices. |
| F1-T02 | COMPLETADA | MVVM/AsyncRelayCommand, cancelación/versiones, estado separado de guardado y limpieza mejorada; regresiones WPF/native. |
| F1-T03 | COMPLETADA | Theme.xaml compartido y estilos WPF UI; render inspeccionado. DPI sobre video se conserva en F0-T07. |
| F1-T04 | COMPLETADA | Pestañas Inicio/TV/Favoritos/Recientes/Configuración; favoritos/recientes completos corresponden a F4. |
| F1-T05 | COMPLETADA | SQLite versionado v2; migración y restricciones/índices comprobados. |
| F1-T06 | COMPLETADA | DPAPI CurrentUser, borrado compensado y tests de ambos fallos conservando datos. |
| F1-T07 | COMPLETADA | XtreamApiException, estados UI, logger sin mensajes/URI y protección del arranque. |
| F1-T08 | COMPLETADA | Validación ProfileService/ProviderEndpoint, estados loading/vacío/error y bloqueo durante guardado ante cambio de perfil. |
| F1-T09 | COMPLETADA | 67 tests actuales, integración SQLite/DPAPI, WPF y motor nativo; sin credenciales reales. |
| F1-T10 | COMPLETADA | README, AGENTS y protocolos F0/F1/F2; auditoría enlazada. |
| F2-T01 | COMPLETADA | XtreamApiClient implementa IIptvProvider; vistas aisladas del transporte. |
| F2-T02 | COMPLETADA | Tests de credenciales, cuentas expiradas/no activas, formatos y límites desconocidos/ocupados. |
| F2-T03 | COMPLETADA | IDs de texto/ceros iniciales, nombres repetidos, logos opcionales y rechazo de listas parciales malformadas. |
| F2-T04 | COMPLETADA | Factory sin logging/redirect/cookies; timeout, serialización, Retry-After, cancelación y cierre. |
| F2-T05 | COMPLETADA | SqliteCatalogStore/CatalogService, fecha UTC, upsert atómico, offline y error sin perder catálogo. |
| F2-T06 | COMPLETADA | CatalogSearch fuera de UI, debounce cancelable y pruebas de acentos con 5.000 canales. |
| F2-T07 | COMPLETADA | Virtualización real, logos limitados a 4/512 KiB/128 entradas, formatos opcionales y cierre. |
| F2-T08 | COMPLETADA | Categorías, favoritos existentes, vacío/loading/error, conservación tras refresco fallido y UI probada. |
| F2-T09 | COMPLETADA | ADR-002: cada servidor como perfil separado, sin fallback automático ni equivalencia asumida. |
| F2-T10 | COMPLETADA | Fixtures grandes/duplicados/parciales, cambios de ID, rollback, perfiles y nueva cobertura de carreras UI. |

## Aceptación y validación pendiente

| Criterio | Resultado |
|---|---|
| Build/test reproducibles y runtime incluido desde salida vacía | PASA |
| Cuenta/catálogo: caminos felices, timeout/cancelación/denegación y normalización | PASA con transporte ficticio; no nueva sesión real |
| Persistencia/secretos/rollback y errores de eliminación | PASA con SQLite/DPAPI reales y datos ficticios |
| WPF: 5.000 canales, filtros, aislamiento y cierre | PASA automatizado |
| Imagen/audio, cambio/detención real y liberación del stream actual | NO VERIFICADO después de corregir el fallo reportado |
| Controles sobre video, fullscreen, DPI y monitores | NO VERIFICADO con evidencia específica actual |
| Métricas hardware/códec y condiciones específicas del servicio | NO VERIFICADO |

Para cerrar F0/F1 y reevaluar F2, ejecutar con cuenta autorizada configurada exclusivamente en la UI:

1. Cerrar instancias anteriores y ejecutar `dotnet run --project src/StreamDesk.Probe.Desktop/StreamDesk.Probe.Desktop.csproj`. Confirmar arranque offline y navegación; crear/eliminar un perfil ficticio como en `PRUEBA_MANUAL_F1.md`.
2. Con red, conectar el perfil autorizado, actualizar catálogo y reproducir un canal. Registrar solo resultado e imagen/audio, sin URL ni cuenta. Probar ts/HLS por separado únicamente si están declarados por el servicio.
3. Cambiar a otro canal; detener y comprobar liberación de la conexión en el servicio si ofrece esa información. Cerrar mientras reproduce y verificar que el proceso termina.
4. Comprobar botones sobre video, pantalla completa/retorno, minimizar/restaurar, tamaño de ventana, DPI disponible y cambio de monitor si hay otro. Registrar por separado lo que no se pudo comprobar.
5. Para F0-T08 anotar Windows/equipo/códec, tiempo a primer cuadro, cambio, RAM/CPU/GPU y duración de sesión. Para F0-T09 confirmar HTTPS y condiciones con el proveedor. Mantener F0-T10 pendiente hasta revisar los avisos nativos de redistribución.

No se solicita enviar credenciales, bases de datos ni URLs autenticadas. El siguiente paso es esta revalidación; F3 no se inicia con esta auditoría.
