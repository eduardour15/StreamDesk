# Fase 0 — decisión técnica

**Fecha:** 2026-10-17. **Estado actual:** PENDIENTE_DE_VALIDACION tras auditoría F0–F2; la aprobación histórica del usuario se conserva en `ESTADO_FASES.md`, pero se debe revalidar playback después del fallo de LibVLC reportado y su corrección.

La prueba técnica usa WPF/WPF UI, un solo `MediaPlayer` LibVLC y un cliente Xtream con timeout, cancelación y errores que no exponen la URI de solicitud. El formulario no guarda credenciales. La aplicación advierte si la URL base usa HTTP y solo habilita formatos `.ts` y `.m3u8` declarados por el proveedor.

Los controles de prueba están dentro de `VideoView` para mitigar el airspace de WPF. El usuario confirmó que comprobó el funcionamiento, conexión y reproducción de video. No reportó modelo de equipo, DPI/monitor, códec o medidas; por tanto, esas observaciones no se atribuyen ni se presentan como mediciones independientes de Codex.

Versiones directas del prototipo: WPF UI 4.3.0 (MIT), LibVLCSharp.WPF 3.10.2 (LGPL-2.1-or-later, según su nuspec) y VideoLAN.LibVLC.Windows 3.0.24 (LGPL-2.1-or-later). La revisión completa de avisos de dependencias nativas transitivas sigue pendiente antes de distribuir. Referencias: [NuGet WPF UI](https://www.nuget.org/packages/WPF-UI/4.3.0), [NuGet LibVLCSharp.WPF](https://www.nuget.org/packages/LibVLCSharp.WPF/3.10.2), [NuGet VideoLAN.LibVLC.Windows](https://www.nuget.org/packages/VideoLAN.LibVLC.Windows/3.0.24), [licencia de LibVLCSharp](https://github.com/videolan/libvlcsharp/blob/3.x/LICENSE) y [guía WPF/airspace](https://github.com/videolan/libvlcsharp/blob/3.x/docs/getting_started.md).

El contrato Xtream implementado interpreta autenticación, categorías, canales y formatos como estructura inicial validada por la conexión reportada. Los tests usan JSON ficticio y acreditan parsing, errores, timeout, cancelación y codificación de URI; no reemplazan las pruebas manuales.
