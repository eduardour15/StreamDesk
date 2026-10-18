# ADR-003: reproducción serializada y ciclo de vida

- Fecha: 2026-10-18
- Estado: aceptada para implementación F3; aceptación multimedia real pendiente.

El usuario solicita culminar F3 expresamente después de informar que los gates F0/F1/F2 necesitan revalidación real. Se implementa F3 bajo esa instrucción, sin aprobar los gates anteriores. El riesgo es que una regresión multimedia real aún no detectada afecte la aceptación final.

Application coordina estados, cancelación, timeout y presupuesto de recuperación detrás de IPlaybackService. Playback implementa IPlaybackBackend con una instancia LibVLC y un MediaPlayer. Se espera Stop antes de abrir el medio siguiente; las operaciones nativas se ejecutan fuera del hilo WPF. Cada intento tiene un identificador: eventos antiguos no cambian el canal vigente. Cerrar, cambiar perfil o suspender cancela apertura, timeout y reconexión. Reanudar Windows no inicia video automáticamente.

Solo se reconecta automáticamente un fallo clasificado como red o timeout, con un máximo de dos reintentos por solicitud del usuario y esperas de 1/2 segundos. Denegación, expiración, límite, formato, inexistencia y errores desconocidos no se reintentan automáticamente. Un fallo específicamente identificado del decodificador por hardware permite un intento adicional por software; no se usa como fallback de errores de autorización. Los diagnósticos nativos solo se clasifican en memoria: no se guardan mensajes, URLs, medios ni datos de cuenta.

Buffer de 250–10.000 ms, 1.000 ms por defecto; se aplica al abrir el siguiente medio. Aceleración automática o software elegida explícitamente. No se afirma una ventaja de rendimiento sin medir hardware real. Pantalla completa conserva el VideoView y su árbol visual para limitar regresiones de airspace; Windows posiciona la ventana en los límites físicos del monitor seleccionado. Se restaura la ventana previa al salir.

F3 reemplaza el modo quiet anterior por un consumidor del evento LibVLC.Log que sustituye su logger predeterminado; solo retiene códigos de error, nunca el mensaje original. Fuentes primarias consultadas: [LibVLC.Log](https://docs.videolan.me/libvlcsharp/api/LibVLCSharp.Shared.LibVLC.html) y [posición de ventanas en varios monitores](https://learn.microsoft.com/en-us/windows/win32/gdi/positioning-objects-on-multiple-display-monitors). El manifiesto declara PerMonitorV2; su funcionamiento con video en monitores de DPI distintos requiere prueba física.

Las pruebas de estados usan un backend ficticio. La inicialización/decodificación nativa local, si se ejecuta, se registra por separado de un proveedor real. Los benchmarks y las pruebas de 20 canales reales, red, monitor y suspensión siguen pendientes hasta aportar evidencia.
