# F4 — Prueba humana del candidato MVP

Estado: PENDIENTE DE VALIDACION. Usa una cuenta autorizada y no compartas capturas con servidor, usuario, contraseña o URLs de stream. Las pruebas automáticas usan datos inventados; no certifican esta sesión.

## Preparación

Desde la raíz, ejecutar `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Build-Mvp.ps1`. El script imprime la carpeta nueva en `artifacts/mvp`. Requiere SDK para compilar; el destino solo necesita Windows x64 y **.NET 10 Windows Desktop Runtime**. No es un ejecutable autocontenido.

En la carpeta generada, ejecutar `powershell -NoProfile -ExecutionPolicy Bypass -File .\Install-Mvp.ps1` para copiar a una carpeta nueva del usuario y crear acceso en Inicio. No requiere administrador ni sustituye instalaciones anteriores. Para abrir sin instalar, ejecutar `app\StreamDesk.Probe.Desktop.exe`. Mantener `libvlc` y avisos. Es un candidato local, sin publicación externa; la revisión completa de avisos/fuentes nativos sigue pendiente.

Una cuenta de Windows de prueba limpia permite evaluar primera instalación sin borrar los perfiles existentes. Cerrar/quitar el candidato conserva `%LocalAppData%\StreamDesk`; no borrar los datos del usuario para esta prueba.

## Recorrido sin instrucciones adicionales al participante

1. Pedir configurar una cuenta una sola vez. Observar si encuentra Configuración y entiende los campos. Si usa HTTP, comprobar el aviso de transporte sin cifrar y consultar si el proveedor soporta HTTPS; no cambiar el servidor automáticamente.
2. Pedir encontrar un canal por nombre/acento y categoría, reproducirlo y confirmar imagen/audio. Observar tiempo y número de interacciones; no registrar el nombre del canal ni credenciales.
3. Pedir marcar la estrella, abrir Favoritos y reproducir desde allí. Quitar/agregar la estrella y comprobar resultado inmediato.
4. Pedir ver otro canal y volver al último desde Inicio o Recientes. Un intento fallido no debe aparecer como visto. El orden refleja el inicio de reproducción.
5. Cerrar y abrir. Debe recordar perfil, favoritos y recientes, sin iniciar video de fábrica. Activar explícitamente «Reproducir el último canal al iniciar» y reiniciar: solo ese perfil/último canal. Desactivar después si no se desea consumir conexión al abrir.
6. Cambiar perfil y suspender/reanudar Windows: no debe empezar video automáticamente ni mezclar favoritos. Detener/cerrar durante carga tampoco debe dejar un stream ni reintentos activos.

## Controles, red y accesibilidad

1. Con foco en TV, probar Espacio (reproducir/detener), ←/→ (anterior/siguiente), ↑/↓ (volumen), M (mute), F (pantalla completa), Esc (restaurar). Dentro de listas ↑/↓ navega la selección. En texto/contraseña/combos/slider/botones se respetan teclas de edición y activación. Probar específicamente después de pulsar sobre la superficie de video.
2. Reproduciendo, retirar foco/mouse de la barra y esperar 3 s. La barra se oculta. Mover mouse **sobre el video nativo** y pulsar teclado: reaparece. Con foco en un control no debe desaparecer. Tab/Shift+Tab acceden a todos los controles visibles. No debe quedar un área que parezca pulsable y no responda.
3. Repetir en ventana mínima, maximizada, fullscreen y al cambiar monitor. Probar escalado real Windows 100/125/150/200% y dos monitores de DPI distintos. Revisar legibilidad, foco, orden de Tab, tooltips, popup de categorías y ausencia de recortes. F/Esc deben conservar video/aspecto y restaurar ventana.
4. Iniciar sin Internet: metadatos/favoritos/recientes locales disponibles; TV necesita red. Reproducir/actualizar debe mostrar error recuperable. Con consulta lenta debe aparecer progreso y Cancelar. Buscar algo inexistente y limpiar filtros. Probar vencimiento/timeout solo cuando exista un caso autorizado; no abrir streams extra ni forzar límites del proveedor.
5. Recorrer los pasos de `PRUEBA_MANUAL_F3.md` para confirmar que la UI no introdujo regresiones multimedia y completar las mediciones que permanecen pendientes.

## Registro sin datos de cuenta

| Caso | Resultado | Interacciones / demora observada | Incidencia y severidad |
|---|---|---|---|
| Instalar/abrir en usuario limpio | PENDIENTE | — | — |
| Alta/búsqueda/reproducción | PENDIENTE | — | — |
| Favoritos/recientes/reinicio | PENDIENTE | — | — |
| Autoplay apagado/opt-in | PENDIENTE | — | — |
| Teclado/mouse/autohide con video | PENDIENTE | — | — |
| DPI físicos/dos monitores | PENDIENTE | — | — |
| Offline/lento/error/cerrar | PENDIENTE | — | — |

Registrar modelo de equipo, resolución y escala sin usuario/servidor. Añadir defectos reproducibles a `DEFECTOS_F4.md`; actualizar `ESTADO_FASES.md` solo con evidencia aportada. F5 se recomienda después de validar el MVP, sin iniciarla en esta ejecución.
