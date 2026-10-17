# Fase 0 — protocolo de validación manual

Realiza estas pruebas solo con un proveedor y canales cuya reproducción tengas autorizada. No pegues las credenciales en Codex ni las guardes en archivos del repositorio.

1. Desde la raíz, ejecuta `dotnet run --project src/StreamDesk.Probe.Desktop/StreamDesk.Probe.Desktop.csproj`.
2. Introduce localmente la URL base, el usuario y la contraseña. Confirma que la contraseña aparece enmascarada. Pulsa **Conectar** y anota si la cuenta aparece activa, sus conexiones actuales/límite y los formatos que declara. No captures la URL completa ni datos de la cuenta.
3. Si el servidor usa HTTP, confirma que aparece la advertencia. Pregunta al proveedor si admite HTTPS; no cambies el host o puerto por conjetura.
4. Pulsa **Actualizar catálogo**. Comprueba que aparecen categorías y canales. Anota errores o campos que no coincidan con la respuesta del proveedor, sin guardar respuestas con credenciales.
5. Elige un canal autorizado y un formato declarado (`ts` o `m3u8`). Pulsa **Reproducir seleccionado** y confirma imagen y audio. Mide desde el clic hasta el primer cuadro y registra el códec si VLC lo informa.
6. Cambia a otro canal autorizado. Verifica en el estado del proveedor que la cuenta no mantiene dos streams simultáneos. Pulsa **Detener** y confirma que la conexión se libera. Cierra la ventana y verifica en el Administrador de tareas que el proceso termina.
7. Prueba controles sobre el video, fullscreen y retorno, minimizar/restaurar, maximizar, redimensionar, escalado DPI y cambio de monitor. Observa si controles y video permanecen alineados y utilizables.
8. Registra modelo de PC, versión de Windows, formato/codec, tiempo a primer cuadro, tiempo de cambio, RAM/CPU/GPU en reposo y durante reproducción, incidencias y evidencia visual redactada. No incluyas usuario, contraseña, URL autenticada ni identificadores privados.

Marca el resultado de cada paso como `PASA`, `NO PASA` o `PENDIENTE`. Una prueba con JSON simulado no sustituye los pasos 2–8 ni autoriza el gate de Fase 0.
