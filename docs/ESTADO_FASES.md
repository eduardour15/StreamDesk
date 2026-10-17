# StreamDesk — Registro de ejecucion por fases

**Estado inicial:** todas las fases pendientes. **Este archivo NO certifica trabajo ejecutado.** Codex lo actualiza en cada intervencion con hechos verificables. No reemplace historial o etiquetas con suposiciones.

| Fase | Gate | Tareas completas | Ultima revision | Bloqueos o dependencias |
|---|---|---:|---|---|
| F0 | PENDIENTE_DE_VALIDACION | 1/11 | 2026-10-17 | Faltan prueba con cuenta/stream autorizado y validación visual de WPF/LibVLC |
| F1 | NO_INICIADA | 0/10 | — | Requiere F0 aprobada |
| F2 | NO_INICIADA | 0/10 | — | Requiere F1 aprobada |
| F3 | NO_INICIADA | 0/11 | — | Requiere F2 aprobada |
| F4 | NO_INICIADA | 0/11 | — | Requiere F3 aprobada |
| F5 | NO_INICIADA | 0/9 | — | Requiere F4 aprobada; EPG/VOD condicional |
| F6 | NO_INICIADA | 0/12 | — | Requiere MVP estable |
| F7 | NO_INICIADA | 0/6 | — | Opcional despues de 1.0 |

## Bitacora de evidencias

Escribir entradas con fecha y zona horaria, fase/tarea, archivos, comandos y resumen real de salida, resultado de pruebas manuales, incidentes pendientes y gate. No escribir secretos.

<!-- ENTRADAS -->

### 2026-10-17 (America/Managua) — Fase 0

- **Implementación:** se creó el repositorio Git, `StreamDesk.sln`, cliente Xtream inicial, app WPF con formulario local, catálogo y un reproductor LibVLC activo. La UI muestra advertencia HTTP y el límite/formato que devuelva el proveedor. Las credenciales no se guardan ni se escriben en logs por la app.
- **Archivos principales:** `src/StreamDesk.Probe.Core/`, `src/StreamDesk.Probe.Desktop/`, `tests/StreamDesk.Probe.Tests/`, `.gitignore`, `README.md`, `docs/DECISION_TECNICA_F0.md` y `docs/PRUEBA_MANUAL_F0.md`.
- **Comandos:** `dotnet restore StreamDesk.sln` (correcto); `dotnet build StreamDesk.sln --no-restore --verbosity normal --maxcpucount:1` (correcto, 0 errores/advertencias); `dotnet test StreamDesk.sln --no-restore --verbosity minimal --maxcpucount:1` (12 pasadas, 0 fallidas).
- **Pruebas manuales:** ninguna. No se proporcionó un proveedor/stream autorizado para autenticar y reproducir; no se verificaron visualmente el overlay, fullscreen, DPI, monitores, liberación real del stream ni métricas. El equipo está en Windows con .NET 10; la consulta WMI del modelo/SO fue denegada.
- **Gate:** `PENDIENTE_DE_VALIDACION`. F1 no queda autorizada hasta completar los pasos pendientes del protocolo manual.

| ID | Estado | Evidencia o pendiente |
|---|---|---|
| F0-T01 | COMPLETADA | Git, solución y app WPF `net10.0-windows` compilados. |
| F0-T02 | PARCIAL | Formulario compilado; falta validar la ventana y el ingreso local. |
| F0-T03 | PARCIAL | Parser de autenticación/cuenta y errores cubierto con JSON ficticio; falta respuesta de un proveedor real. |
| F0-T04 | PARCIAL | Cliente de categorías/canales con timeout, cancelación y pruebas mock; falta validar el contrato real del proveedor. |
| F0-T05 | BLOQUEADA | Falta una prueba de reproducción con un canal autorizado. |
| F0-T06 | PARCIAL | Un solo `MediaPlayer`; cambio/detención liberan el medio; falta probar la sesión real y cierre de proceso. |
| F0-T07 | PENDIENTE | El overlay se ubicó dentro de `VideoView` siguiendo el patrón documentado; falta probar controles, fullscreen, DPI y monitor. |
| F0-T08 | PENDIENTE | Sin métricas reales ni modelo de hardware/códec. |
| F0-T09 | PARCIAL | Aviso HTTP y lectura/límite de conexiones implementados; falta confirmar HTTPS, política y límite con el proveedor. |
| F0-T10 | PARCIAL | Versiones/licencias directas documentadas; falta revisar avisos de todas las dependencias nativas antes de distribuir. |
| F0-T11 | PARCIAL | Decisión y protocolo documentados; faltan evidencias de ejecución real. |
