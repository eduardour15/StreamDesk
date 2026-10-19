# ADR-007: ventana flotante con un solo reproductor

Fecha: 2026-10-19. Estado: aceptada para implementación F7-T01; validación física pendiente.

El usuario solicita trabajar la siguiente fase después de F5/F6. Se elige únicamente F7-T01, conforme al prompt de mejoras opcionales. Se adelanta este incremento sin aprobar F6 ni declarar una versión 1.0: sus pruebas físicas y la revisión completa de licencias nativas siguen pendientes. No se agregan las otras funciones opcionales.

PiP cambia la presentación de la ventana principal: tamaño compacto, controles adyacentes al video y Topmost activado con opción de desmarcarlo. Conserva el mismo VideoView, HWND de video, MediaPlayer y sesión; entrar/salir no autentica, detiene ni abre medios. El caption nativo permite mover, redimensionar, minimizar y cerrar. Cerrar termina la aplicación y libera el reproductor; Volver/Esc restaura la ventana previa sin detenerlo.

Pantalla completa y PiP son modos excluyentes. Para pasar de uno a otro primero se restaura la ventana normal, que sigue siendo el destino de Volver/Esc. Se conserva posición/estado normal o maximizado, estilo, límites mínimos y Topmost previo. La ventana compacta se coloca dentro del área de trabajo del monitor actual, en píxeles físicos con tamaño calculado según DPI. Los controles de retorno/fijación permanecen accesibles, sin depender de overlays WPF sobre el HWND.

No hay cambios de contratos de reproducción, datos, credenciales ni dependencias. La coordinación de ventana pertenece a Desktop, como pantalla completa. Las pruebas WPF usarán fixtures y motor local para verificar continuidad, retorno, cierre y un solo stream; DPI/monitores físicos y proveedor real se registrarán por separado.

Windows puede ajustar una posición de retorno que quedó totalmente fuera de pantalla; se conserva ese comportamiento de recuperación del sistema.

Referencias primarias: [Window.Topmost](https://learn.microsoft.com/en-us/dotnet/api/system.windows.window.topmost?view=windowsdesktop-10.0), [VideoView de LibVLCSharp y asociación al HWND](https://github.com/videolan/libvlcsharp/blob/3.x/src/LibVLCSharp.WPF/VideoView.cs), [SetWindowPlacement y recuperación de ventanas fuera de pantalla](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowplacement).
