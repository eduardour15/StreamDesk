# ADR-012: Controles superpuestos, cursor local y atajos del reproductor

- Fecha: 2026-10-24.
- Alcance: corrección de F0-T07, F3-T04 y F4-T06/T07/T09, sin iniciar fases ni alterar gates históricos.
- Estado: implementación; evidencias y pendientes en `docs/ESTADO_FASES.md`.

## Decisión

La combinación de `PlayerControls.Visibility = Hidden`, una fila fija de estado de 34 DIP y controles adyacentes conservaba una franja inferior incluso en pantalla completa. Sustituimos esa presentación por el mecanismo incluido en LibVLCSharp.WPF **3.10.2**: `VideoView.Content` es trasladado por la biblioteca a un `ForegroundWindow` transparente, propiedad de la ventana principal y alineado al HWND nativo. Un `Grid` hermano con `ZIndex` no resolvería airspace.

`PlayerOverlay` contiene la barra inferior semitransparente, el estado, el loader, el OSD, el placeholder y la cabecera PiP. Mostrar u ocultar `PlayerChrome` no participa en la medición del vídeo; desaparece con `Collapsed`. El vídeo mantiene `Scale = 0`, su relación de aspecto y sus bandas negras naturales. Se conservan un `VideoView`, un HWND de vídeo, un `MediaPlayer` y una sesión; cambiar de modo o mostrar controles no abre medios.

**Corrección de navegación (2026-10-24):** el usuario detectó icono y controles sobre Inicio. La ventana foreground no hereda el `Collapsed` de `TvPage`, y LibVLCSharp 3.10.2 solo la oculta al descargar el host. Desktop sincroniza `Hide`/`Show` con `ShowTv`, `ShowPlayerPane`, `VideoView.IsVisible` y la minimización. No se cierra ni se recrea el foreground, no se mueve `VideoView` ni se abre otro medio. La reaparición desactiva `ShowActivated` solo durante `Show` y restaura después su valor original, para conservar el foco de la página y la política de interacción del foreground. Se comprueban Inicio inicial, navegación Inicio/Ajustes/catálogos y regreso al reproductor con el mismo HWND/medio. La regresión verifica también `IsWindowVisible` sobre el HWND real; una captura de layout WPF por sí sola no detectaría una ventana independiente filtrada sobre Inicio.

El fondo del área de interacción usa negro con alfa **1/255**. Los píxeles totalmente transparentes de una ventana layered dejan pasar la entrada al HWND inferior: la prueba de escritorio detectó que `Cursor=None` en el árbol WPF no ocultaba el cursor nativo con alfa cero. El alfa mínimo mantiene la superficie receptiva sin un hook global ni alterar el contador global de `ShowCursor`. Introduce una atenuación negra de aproximadamente 0,4 % sobre la imagen.

El cursor se controla mediante `PlayerOverlay.Cursor` y `ForceCursor`, limitado a esta superficie. A los 2,5 segundos de reproducción sin interacción se ocultan barra y cursor; movimiento y teclado recuperan los controles. Se respetan hover, captura de ratón/táctil, navegación de teclado y menús desplegables. Salir de la superficie, desactivar/minimizar, detener, pausar, cargar o cerrar restaura el cursor. La actividad considera la ventana principal y su foreground; el foco puede pasar entre ambas. Los eventos de teclado se reciben también en el overlay, que tiene otra fuente de presentación. El cierre desacopla eventos y dispone el overlay mediante `VideoView.Dispose()`.

Atajos locales al reproductor: Espacio/K pausa o reanuda la sesión activa; F alterna pantalla completa; Esc restaura fullscreen, PiP o contexto inmersivo; M silencia; ↑/↓ ajusta cinco puntos con límite 0–100; ←/→ busca ±5 segundos y J/L ±10, solo si `CanSeek`. Shift+P/N conserva anterior/siguiente sin confundirlo con seek. P (PiP) y B (amplificación) se conservan. El Sound Booster manual existente hasta 400 % no se modifica. Inputs, listas, sliders, botones y popups conservan su interacción propia; los eventos ya manejados y la repetición no duplican acciones.

## Verificación

Las pruebas de overlay usan una ventana WPF y LibVLC real con un vídeo AVI local generado (16:9, 2 fps, sin audio), comparan límites mediante coordenadas de pantalla y comprueban HWND/propietario, restauración de fullscreen/PiP y viewport estable durante carga/autohide. Las pruebas de teclado verifican pausa sin reapertura, seek/clamping y volumen. La prueba opcional de escritorio (`STREAMDESK_NATIVE_INPUT=1`) comprueba clic y teclado de Windows, estado del cursor y capturas compositadas; requiere escritorio interactivo fuera del aislamiento del proceso de tests. La evidencia de este vídeo local no acredita vídeo IPTV, códecs ni DPI de dos monitores físicos.

Fuentes primarias: [VideoView 3.10.2](https://github.com/videolan/libvlcsharp/blob/3.10.2/src/LibVLCSharp.WPF/VideoView.cs), [ForegroundWindow 3.10.2](https://github.com/videolan/libvlcsharp/blob/3.10.2/src/LibVLCSharp.WPF/ForegroundWindow.cs), [ventanas layered y hit-testing](https://learn.microsoft.com/en-us/windows/win32/winmsg/window-features#layered-windows), [atajos VLC](https://docs.videolan.me/vlc-user/desktop/3.0/en/basic/hotkeys.html), [atajos YouTube](https://support.google.com/youtube/answer/7631406?hl=es).
