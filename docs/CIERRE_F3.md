# F3 — Implementación y evidencia de reproducción

Fecha: 2026-10-18 (America/Managua). Solicitud: «culmina la fase 3». Se implementó el alcance de código F3, sin iniciar F4. El gate es `PENDIENTE_DE_VALIDACION` y no se declara una sesión IPTV real que no se ejecutó.

## Dependencias y decisión

La instrucción explícita de avanzar llegó después de comunicar que F0/F1/F2 requieren revalidar el video real tras el fallo de LibVLC. Se desarrolló F3 bajo esa instrucción y con el riesgo advertido; no se aprobaron retroactivamente esos gates. Se conservan el catálogo, SQLite, DPAPI y los datos del usuario. `Error/error.txt` permanece fuera de Git.

## Implementación

- `PlaybackModels.cs`: estados Idle, Loading, Playing, Buffering, Failed y Stopped; códigos de fallo y mensajes seguros; opciones de búfer/aceleración validadas.
- `PlaybackContracts.cs` / `PlaybackService.cs`: servicio observable, serialización de operaciones, cancelación inmediata de solicitudes anteriores, IDs de intento para ignorar eventos antiguos, timeout de apertura/buffering, volumen/mute y cierre asíncrono. Se espera Stop antes de abrir el siguiente medio. El presupuesto de recuperación no se restablece por un evento Playing.
- `LibVlcBackend.cs` / `NativeFailureClassifier.cs`: un LibVLC y un MediaPlayer; llamadas nativas fuera del hilo WPF; descarga de recursos determinista; diagnósticos clasificados solo en memoria. El servicio anterior `LibVlcPlaybackService.cs` fue reemplazado por coordinador y backend y se conserva en el historial Git.
- `MainViewModel.Playback.cs`: controles y notificaciones en el contexto de UI, anterior/siguiente dentro del orden filtrado, validación de vencimiento y límite informado antes de iniciar otra reproducción. El placeholder se oculta al abrir/reproducir y los errores permiten reintentar.
- `MainWindow` / `WindowDisplays.cs` / `app.manifest`: volumen/mute, búfer, modo software, selector de monitor, modo completo/retorno con Esc y manifiesto PerMonitorV2. Se usa GetMonitorInfo/SetWindowPos y se restaura la posición/estilo previos; no se mueve el VideoView entre contenedores. Cambiar de perfil, suspender o cerrar cancela recuperación y medios. Suspender no activa autoplay al reanudar. La ventana espera DisposeAsync sin bloquear el Dispatcher.
- `scripts/Measure-Playback.ps1`: recolector de CPU/RAM/GPU del proceso, con metadatos de hardware, archivos únicos bajo `artifacts/metrics` y ausencia de URLs/canales/cuenta. GPU o WMI ausentes se marcan como no disponibles. Solo se verificó su sintaxis; las mediciones reales siguen pendientes.

La recuperación automática permite como máximo dos reintentos por solicitud del usuario, con esperas de 1/2 segundos, únicamente para red/timeout reconocidos. 401/403/404, vencimiento, límite de conexiones, formato y fallos desconocidos no se reconectan automáticamente. Un fallo específicamente identificado de hardware permite una reapertura por software; no sirve para eludir denegaciones. El búfer predeterminado es 1.000 ms, con rango 250–10.000 ms. El búfer y el modo de decodificación se aplican al abrir el siguiente canal; no se prometen mejoras de rendimiento sin medir.

## Evidencia ejecutada

```powershell
dotnet build StreamDesk.sln --no-restore --verbosity minimal --maxcpucount:1 -p:UseSharedCompilation=false
dotnet test StreamDesk.sln --no-build --no-restore --verbosity minimal --maxcpucount:1
git diff --check
```

Build: 0 advertencias, 0 errores. Suite final: 100 correctas, 0 fallidas, 0 omitidas.

| Tipo de prueba | Evidencia y límite |
|---|---|
| Coordinación sin proveedor | 25 casos: transiciones, 20 solicitudes rápidas, máximo de un medio activo, cancelación posterior a apertura, timeout, cierre durante apertura, audio, opciones, presupuesto finito, rechazo de reintentos definitivos y fallback por software una sola vez. |
| Integración ViewModel | 4 casos: anterior/siguiente sobre resultados filtrados, ajustes efectivos al backend, suspensión sin autoplay, cambio de perfil durante buffering, bloqueo de cuenta vencida y límite ocupado. |
| LibVLC nativo | Inicializa, conserva el mismo MediaPlayer y se dispone. Decodifica 20 BMP distintos generados en un directorio temporal; se comprueba el color esperado en cuadros entregados a un buffer de memoria y la liberación final. Es decodificación real de fixtures locales por software; no es video IPTV, audio real, salida HWND ni medición de aceleración. |
| Red nativa local | Un servidor loopback devuelve 401/403/404; LibVLC real los clasifica y el coordinador no reabre automáticamente. No hay cuenta ni servidor IPTV real. |
| Regresión F0–F2 | Se conservan las 67 pruebas anteriores, incluidos Xtream ficticio, SQLite/DPAPI, rollback, carreras y ventana WPF con 5.000 canales. |
| Interfaz | Ventana WPF con estilos de producción, controles y cierre asíncrono; captura local inspeccionada. RenderTargetBitmap no acredita el overlay nativo ni su comportamiento en varios monitores. |
| Medición | Parser PowerShell sin errores. Recolección CPU/GPU con video/hardware real: NO VERIFICADA. |

La primera ejecución conjunta tuvo 99 correctas y un timeout de UI. El cierre llamaba de nuevo Close dentro del evento Closing cuando DisposeAsync terminaba inmediatamente. Se difirió ese segundo Close al Dispatcher, pasó la prueba aislada y luego la suite completa. Los resultados finales corresponden al código corregido.

## Checklist F3

| ID | Estado | Evidencia / pendiente |
|---|---|---|
| F3-T01 | COMPLETADA | Servicio único con estados observables y pruebas de transiciones/eventos antiguos. |
| F3-T02 | COMPLETADA | URL creada por IIptvProvider en memoria; tests de escape/formato existentes. Snapshots/excepciones/logs de aplicación no incluyen URI ni credenciales; mensajes nativos solo se clasifican en memoria. |
| F3-T03 | COMPLETADA | Play/Stop async, volumen/mute y anterior/siguiente sobre resultados visibles probados. |
| F3-T04 | PARCIAL | Fullscreen/ventana, relación natural de aspecto, monitor y DPI implementados; falta probar salida de video/controles y monitores físicos de DPI distintos. |
| F3-T05 | PARCIAL | Búfer validado y una reapertura de software ante fallo clasificado de hardware, probados con backend controlado. Falta fallo/recuperación y rendimiento medidos en GPU real. |
| F3-T06 | COMPLETADA | 20 cambios rápidos ficticios, serialización/Stop previo, eventos antiguos ignorados; 20 reemplazos nativos de fixtures comprobados. |
| F3-T07 | COMPLETADA | Backoff y máximo de dos reintentos; no se restablecen entre éxitos breves. Casos definitivos no se reabren. |
| F3-T08 | COMPLETADA | Cambio de perfil, cableado a PowerModeChanged, llamada de suspensión sin autoplay y cierre/Dispose async probados. El ciclo físico de suspensión/reanudación de Windows sigue en aceptación manual. |
| F3-T09 | COMPLETADA | Mensajes por causa; 401/403/404 nativos locales, cuenta vencida/límite, timeout, formato, dirección inválida y fallo desconocido. |
| F3-T10 | PARCIAL | Recolector y protocolo disponibles; faltan CPU/RAM/GPU, códec y tiempos reales para evaluar/optimizar. |
| F3-T11 | PARCIAL | 20 transiciones ficticias y 20 medios nativos locales, cancelación/cierre y fallos controlados probados. Falta sesión de 20 canales autorizados, interrupción/restauración de red y cierre mientras se reproduce IPTV. |

## Aceptación

| Criterio | Resultado |
|---|---|
| Un solo backend/MediaPlayer y operaciones sin superposición | PASA en tests controlados y reemplazos nativos locales. |
| Play/cambio/stop/close y errores recuperables | PASA automatizado; sesión IPTV real NO VERIFICADA. |
| Retry limitado, cancelado por stop/perfil/cierre; denegaciones sin bucle | PASA, incluido HTTP nativo local. |
| Volumen/mute y orden visible | PASA en integración; audio audible real NO VERIFICADO. |
| Video, fullscreen, monitores, DPI y suspensión física | NO VERIFICADO en entorno de uso real. |
| Medición de rendimiento con hardware/códec conocidos | NO VERIFICADO; no se publican cifras ficticias. |

Gate: `PENDIENTE_DE_VALIDACION`, 7 tareas COMPLETADAS y 4 PARCIALES. La implementación de código está entregada; el cierre funcional definitivo exige `PRUEBA_MANUAL_F3.md`. F0/F1/F2 siguen pendientes según la auditoría y no se inicia F4.
