# Fase 0 — decisión técnica provisional

**Fecha:** 2026-10-17. **Estado:** prototipo compilable; gate pendiente de validación real.

Se crea una solución con un núcleo pequeño para el cliente Xtream, una app WPF de prueba y pruebas unitarias separadas. El formulario no persiste credenciales. Las llamadas usan timeout y cancelación; los errores que llegan a la UI son genéricos y no contienen la URI de solicitud. La app advierte si la URL base usa HTTP. El cliente solo habilita formatos `.ts` y `.m3u8` que el servidor declare.

La app usa un solo `MediaPlayer`; al cambiar de canal o detener, detiene y libera el medio anterior. Los controles de prueba se colocan dentro de `VideoView`, siguiendo el patrón documentado por LibVLCSharp para mitigar el airspace de WPF. Falta validar visualmente overlays, fullscreen, DPI y monitores; no se concluye que la solución sea viable hasta esa prueba.

Versiones directas fijadas para el prototipo: WPF UI 4.3.0 (MIT), LibVLCSharp.WPF 3.10.2 (LGPL-2.1) y VideoLAN.LibVLC.Windows 3.0.24 (LGPL-2.1-or-later). Las licencias y avisos de las dependencias nativas transitivas aún requieren revisión antes de redistribuir. Referencias: [NuGet WPF UI](https://www.nuget.org/packages/WPF-UI/4.3.0), [NuGet LibVLCSharp.WPF](https://www.nuget.org/packages/LibVLCSharp.WPF/3.10.2), [NuGet VideoLAN.LibVLC.Windows](https://www.nuget.org/packages/VideoLAN.LibVLC.Windows/3.0.24), [licencia de LibVLCSharp](https://github.com/videolan/libvlcsharp/blob/3.x/LICENSE) y [guía WPF/airspace](https://github.com/videolan/libvlcsharp/blob/3.x/docs/getting_started.md).

La forma Xtream `user_info`, categorías, streams y formatos se implementa como contrato inicial para validarlo con un proveedor autorizado. Las pruebas usan JSON ficticio y solo acreditan parsing, clasificación de errores, timeout, cancelación y codificación de URI. No acreditan una API real ni reproducción de video. No se cambia de motor ni se registra ADR sobre airspace hasta obtener evidencia empírica.
