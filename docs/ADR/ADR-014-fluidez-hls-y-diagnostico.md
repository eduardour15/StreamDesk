# ADR-014 — Fluidez HLS y diagnóstico de reproducción

- Fecha: 2026-10-24 (America/Managua).
- Alcance: incremento autorizado de F3-T05/T07/T10/T11; conserva el trabajo de ADR-013.
- Estado: implementado y verificado automáticamente; proveedor real pendiente de validación.

Se elige mejorar la reserva por protocolo, aprovechar la adaptación HLS de VLC y mostrar observaciones numéricas de la sesión. No se agrupan automáticamente canales por nombres parecidos: las dos denominaciones encontradas en el catálogo no acreditan equivalencia ni distintas calidades.

«Priorizar fluidez» se activa por defecto en la aplicación, con una caché mínima en vivo de 5000 ms. La preferencia manual se conserva. En HTTP de TV se pasan también parámetros del demux adaptativo: retraso objetivo de 20–30 s y máximo de almacenamiento de 30 s. Solo tienen efecto cuando VLC usa ese demux; HLS puede limitar la reserva según los segmentos publicados. El watchdog para fuentes `.m3u8` tiene en cuenta esa reserva. VOD y archivos locales conservan su comportamiento.

La calidad automática usa la adaptación nativa `nearoptimal`; «Ahorro» solicita `lowest`. La selección es una preferencia, no prueba de variantes ni de resolución efectiva. Una señal única no se transcodifica ni ahorra datos por reducir su tamaño de presentación. No hay consultas de manifiesto ni streams adicionales fuera del reproductor. Al conectar se prefiere `m3u8` únicamente si la cuenta lo declara y fluidez está activa. La elección manual de formato sigue disponible. «Aplicar al canal actual» guarda y reabre la misma entrada por el flujo serializado; en Ahorro selecciona HLS si está declarado.

El diagnóstico usa diferencias del contador de lectura de VLC y fotogramas reportados en intervalos monotónicos, con valores ausentes cuando no hay estadísticas útiles. Algunos demux publican estadísticas parciales: no representa la velocidad total de Internet ni atribuye la causa a la red del usuario. Registra recargas y tiempo de espera observados desde el inicio de esa sesión; pausa y seek no cuentan como cortes. No contiene URLs, nombres, perfiles ni datos de cuenta, y no se persiste en logs. Se presenta cada 2 s por el temporizador existente, incluso cuando la lectura de pistas de TV ya no es necesaria; no se dispara una notificación completa de estados por cada muestra.

La migración de preferencias añade columnas idempotentes a `AppSettings`, conservando búfer y modo de decodificación elegidos. Las escrituras de preferencias se encadenan a la cola existente y se esperan al cerrar, evitando que una escritura quede usando SQLite después del cierre del ViewModel.

Se comprobó VLC nativo con un master HLS local de dos variantes declaradas y vídeo MPEG-TS generado con el encoder incluido. Ahorro solicita la variante de menor ancho de banda; Automática y una playlist de calidad única producen fotogramas reutilizando el mismo `MediaPlayer`. Las dos variantes del fixture reutilizan el mismo contenido TS: esto valida selección, no una diferencia de resolución ni adaptación automática ante una conexión real variable. Ese último escenario permanece en el protocolo manual.

Fuentes primarias de la versión instalada: [selección adaptativa y reserva HLS de VLC 3.0.24](https://github.com/videolan/vlc/blob/3.0.24/modules/demux/adaptive/PlaylistManager.cpp), [opciones](https://github.com/videolan/vlc/blob/3.0.24/modules/demux/adaptive/adaptive.cpp) y [límites del búfer](https://github.com/videolan/vlc/blob/3.0.24/modules/demux/adaptive/logic/BufferingLogic.cpp).

La verificación (build sin advertencias/errores y 208 tests pasados) y el gate `PENDIENTE_DE_VALIDACION` se registran en `docs/ESTADO_FASES.md`. El build se entrega en `artifacts/fluidez/bin/Debug/net10.0-windows` para no sustituir las DLL de la instancia Debug que el usuario tenía abierta. No se actualiza esa instancia en ejecución, no se instala ni se inicia otra fase.
