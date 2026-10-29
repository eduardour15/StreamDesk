# ADR-013 — Estabilidad y recuperación de TV en vivo

- Fecha: 2026-10-24 (America/Managua).
- Alcance: corrección de F3-T05/T07/T10/T11; no inicia otra fase.
- Estado: implementación local verificada; proveedor y hardware real pendientes de validación.

## Configuración observada

La consulta local de solo lectura, limitada a dos campos de `AppSettings`, devolvió `BufferMilliseconds = 3500` y `SoftwareDecoding = 0`. Coincide con el valor predeterminado de la aplicación. No se consultaron perfiles, credenciales ni fuentes. No se modificó la base del usuario.

La instancia abierta correspondía a la instalación de `C:\Program Files\StreamDesk`, no al ejecutable compilado del proyecto. La compilación no actualiza esa instalación ni cambia la reproducción en curso.

## Hallazgos y decisiones

1. **Recuperar el ritmo de presentación.** Las opciones de TV `no-drop-late-frames` y `no-skip-frames` impedían las estrategias normales para recuperarse del atraso. Se eliminan y se habilitan explícitamente sus alternativas en la instancia LibVLC. VLC 3.0.24 las habilita por defecto y describe el descarte de fotogramas que llegan tarde a la salida; inferimos que impedirlo puede empeorar la recuperación bajo carga. Esto no demuestra que sea la causa de los cortes del proveedor. [Fuente: opciones de VLC 3.0.24](https://github.com/videolan/vlc/blob/3.0.24/src/libvlc-module.c).
2. **Aceleración automática real.** `HardwareDecoding.Automatic` usaba exclusivamente `d3d11va`. Ahora usa `avcodec-hw=any`, el selector automático predeterminado de VLC. El modo software sigue usando `none`, y se mantiene el fallback acotado por fallo identificado de hardware. La selección automática no garantiza que una GPU concreta decodifique un códec concreto. [Fuente: módulo avcodec 3.0.24](https://github.com/videolan/vlc/blob/3.0.24/modules/codec/avcodec/avcodec.c).
3. **Conservar el búfer adaptado.** El búfer efectivo se guarda por sesión y aumenta 1500 ms en cada reintento transitorio de TV, hasta 10000 ms. Restablecer el presupuesto después de 60 s saludables no reduce el búfer aprendido. Ejemplo: 3500 → 5000, reproducción saludable, siguiente corte → 6500. Nueva sesión/canal parte de la preferencia del usuario. VOD no recibe este escalado.
4. **Supervisar salida durante los avisos de búfer.** Antes se dejaban de leer estadísticas en `Buffering`; `Playing` y `Buffering(100%)` refrescaban la marca temporal sin verificar imagen. Ahora se sigue muestreando tras el primer `Playing`. Solo el avance observado actualiza esa marca; una vez confirmado el estancamiento, `Playing` por sí solo tampoco cancela su recuperación. La vuelta de fotogramas/audio/reloj útil sí permite recuperarse sin abrir otra conexión.
5. **Respetar la caché efectiva.** La tolerancia previa al watchdog usa `max(4 s, 2 × búfer efectivo)`, en lugar del búfer inicial. Tras confirmarse el bloqueo se conservan 5 s de oportunidad de recuperación y después el presupuesto habitual de dos reintentos. Cada muestra sin avance interrumpe el cómputo de los 60 s saludables. Pausa, seek, cancelación y fin VOD conservan sus exclusiones.
6. **Separar red y reproducción.** `DemuxReadBytes` ya no se devuelve como posición en milisegundos: bytes recibidos no demuestran salida de imagen/audio ni son tiempo. Se mantienen los contadores de fotogramas/audio y el reloj como respaldo; si no hay datos útiles, la ausencia de estadísticas no prueba un congelamiento.

## Límites y consecuencias

Se mantienen un reproductor, cierre previo a reapertura, cancelación, errores definitivos sin reintento y ausencia de secretos en diagnósticos. Se conservan las dependencias fijadas. `http-reconnect`, los tres tipos de caché y los hilos automáticos continúan. Se elimina `clock-jitter=5000` por redundante con el valor predeterminado de VLC 3.0.24; no se desactiva la sincronización de reloj.

Mayor búfer puede absorber variaciones breves de entrega a costa de latencia y memoria. La opción de 10000 ms no asegura mayor fluidez si faltan datos de forma sostenida o la decodificación no alcanza la velocidad necesaria. Se conserva el valor elegido del usuario y se añade ayuda en Ajustes para probar 5000 ms y después 8000 ms. La preferencia manual se aplica al reabrir; el botón de estabilidad reabre el canal activo.

La prueba nativa AVI comprueba fotogramas cambiantes y reutilización del mismo `MediaPlayer` en automático/software/automático. Su vídeo sin comprimir no acredita aceleración H.264/HEVC, ancho de banda, HLS/TS ni estabilidad del proveedor. Evidencias y gate se registran en `docs/ESTADO_FASES.md`; protocolo en `docs/PRUEBA_MANUAL_ESTABILIDAD_TV.md`.
