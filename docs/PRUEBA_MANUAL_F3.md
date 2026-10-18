# F3 — Validación real de reproducción y rendimiento

Estado inicial: PENDIENTE_DE_VALIDACION. Introducir cuentas autorizadas exclusivamente en StreamDesk. No pegar credenciales, URLs autenticadas, bases ni logs sin revisar en un informe.

## Preparación

Cerrar versiones anteriores. Desde la raíz:

```powershell
dotnet build StreamDesk.sln
dotnet test StreamDesk.sln
dotnet run --project src/StreamDesk.Probe.Desktop/StreamDesk.Probe.Desktop.csproj
```

La suite incluye un motor nativo con imágenes locales y HTTP loopback ficticio; no reemplaza la sesión IPTV de abajo. Si se usa HTTP, confirmar el aviso de credenciales sin cifrar y consultar HTTPS al proveedor. Respetar el límite de conexiones.

## Sesión funcional

1. Conectar el perfil autorizado y actualizar el catálogo. Seleccionar un canal y reproducir. Confirmar imagen, sonido, relación de aspecto y desaparición del placeholder. Probar ts y HLS solo si están declarados por el servicio; registrar cada resultado por separado.
2. Ajustar volumen al 0/30/100 y mute; confirmar el efecto audible. Buscar/filtrar una categoría y usar Anterior/Siguiente: deben seguir el orden visible, detenerse en sus extremos y no abrir canales ocultos por el filtro.
3. Cambiar entre 20 canales disponibles sin abrir otra instancia. Comprobar un único stream activo en la información del servicio cuando esté disponible y observar que no reaparecen cuadros de un canal anterior. Registrar latencia de cambio con cronómetro.
4. Pulsar Detener mientras abre y mientras reproduce. Cambiar de perfil durante buffering. Cerrar la ventana durante reproducción y durante buffering. Confirmar fin del proceso y liberación de la conexión según la visibilidad del proveedor; no afirmar lo que el servicio no permite comprobar.
5. Interrumpir la red durante reproducción y restablecerla. Si el motor identifica red/timeout, debe usar como máximo dos reintentos y permitir detenerlos. Un error desconocido puede exigir Reintentar manual. Usar solo respuestas de denegación que aparezcan naturalmente; no provocar cargas repetidas ni modificar límites del proveedor. Los 401/403/404 ficticios ya se cubren automáticamente.
6. Elegir monitor, entrar/salir de pantalla completa y usar Esc. Confirmar restauración de posición/estilo, botones sobre video, maximizar/minimizar, tamaño pequeño, DPI 100/150/200 % disponibles y traslado entre monitores de DPI distinto. Si solo hay una pantalla, marcar multi-monitor NO VERIFICADO.
7. Suspender Windows mientras reproduce o abre el canal. Reanudar: no debe comenzar video por sí solo. Pulsar Reproducir manualmente y comprobar nueva sesión única.
8. Comparar modo automático y Video por software con el mismo canal/códec y duración. Cambiar el búfer entre 500/1000/2000 ms antes de abrir otro medio. Registrar estabilidad y latencia; no atribuir una mejora a un ajuste sin medición. Si no ocurre un fallo real de hardware, el fallback queda verificado por tests controlados, no por la sesión física.

## Captura de recursos

Con una sola instancia abierta y el canal iniciado, en otra consola PowerShell desde la raíz:

```powershell
.\scripts\Measure-Playback.ps1 -DurationSeconds 120 -IntervalSeconds 2
```

El script no inicia, cambia ni detiene canales. Guarda CSV y JSON de contexto en `artifacts/metrics`, fuera de Git, sin nombres de canales, credenciales ni URLs. CPU es porcentaje normalizado a todos los procesadores lógicos; GPU es el máximo contador de motor asociado al PID y no una utilización total universal. Un campo vacío/NO_DISPONIBLE significa que no se pudo medir, no cero. El primer cuadro/cambio y códec requieren anotación manual.

Medir una sesión en reposo, una con video estable y otra con 20 cambios. Registrar duración, Windows, CPU/GPU/RAM del equipo, códec, modo de decodificación, búfer, tiempo hasta imagen y cambios. Buscar tendencias de memoria durante cambios repetidos, sin prometer ausencia universal de fugas.

## Resultado a registrar

| Comprobación | PASA / NO PASA / NO VERIFICADO | Observación sin datos privados |
|---|---|---|
| Imagen/audio por formato admitido | | |
| Volumen/mute/anterior/siguiente | | |
| 20 cambios y conexión única | | |
| Detener/cambiar perfil/cerrar | | |
| Corte de red/recuperación/cancelación | | |
| Fullscreen/ventana/monitor/DPI/overlay | | |
| Suspender/reanudar sin autoplay | | |
| Búfer/hardware/software/fallback | | |
| CPU/RAM/GPU/códec/primer cuadro | | |

Actualizar `ESTADO_FASES.md` únicamente con resultados realmente observados. La solicitud de avanzar en desarrollo no se interpreta como aprobación automática de estas pruebas.
