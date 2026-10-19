# F7-T01 — ventana flotante / PiP

Fecha: 2026-10-19. Solicitud: «trabaja la siguiente fase». F7 partía NO_INICIADA; F6 seguía PENDIENTE_DE_VALIDACION. Se comunicó esa dependencia y se adelanta únicamente la primera mejora opcional, sin aprobar F6 ni declarar 1.0. El prompt F7 exige detenerse tras un incremento para revisión. Rama: `feature/F7-T01-ventana-flotante`.

## Implementación

«Ventana flotante» en el encabezado de TV/VOD o P cambia la ventana principal a 480x340 DIP aproximados en el área de trabajo del monitor actual, mínimo 360x260 DIP. «Fijar» controla Topmost; controles compactos siempre visibles permiten reproducir/detener, silenciar y reintentar. Volver/Esc restaura posición, tamaño, límites, Topmost y estado normal/maximizado previos. F y P hacen exclusivas pantalla completa y PiP; Esc regresa a normal. Cerrar PiP cierra la aplicación y libera el motor. Cambiar a una sección sin reproductor restaura la ventana.

Se mantienen el mismo VideoView/árbol visual, HWND, MediaPlayer y sesión; no se autentica ni abre otro medio al cambiar presentación. Controles y encabezado están fuera del área HWND para evitar depender de overlays. No hay cambios de esquema, secretos, proveedor ni paquetes. ADR-007 registra decisiones y fuentes primarias.

Archivos principales: `WindowDisplays.cs`, `MainWindow.xaml`/`.xaml.cs`, `PlaybackShortcuts.cs`, versión del proyecto y default de `Build-Candidate.ps1`; pruebas `CatalogUiTests.PictureInPicture.cs`, ampliación del caso nativo WPF y `ShortcutTests.cs`. Manual/README/ROADMAP/estado actualizados. `Error/error.txt` se conserva fuera del commit.

## Evidencia ejecutada

- Base F6 revalidada: build Release sin errores/advertencias; 128 tests correctos.
- `dotnet build StreamDesk.sln -c Release --no-restore --verbosity minimal --maxcpucount:1 -p:UseSharedCompilation=false`: 0 errores, 0 advertencias.
- `dotnet test StreamDesk.sln -c Release --no-build --no-restore --verbosity minimal --maxcpucount:1`: **130 correctos, 0 fallidos/omitidos**, 35 s. Incluye las 128 regresiones anteriores y dos casos adicionales de teclado; la prueba WPF existente se amplió con PiP y motor nativo.
- WPF con backend ficticio: cinco ciclos de entrada/retorno, mismos objetos, una sola apertura/pico activo 1, fijación opcional, retorno exacto visible y estado maximizado/Topmost anterior, tamaño mínimo y controles sin intersección con el video. PiP ↔ fullscreen → normal, salida por cambio de sección y cierre activo con disposición única. JSON: `artifacts/f7-pip-evidence.json`.
- Caso LibVLC/WPF real: BMP local inventado, HWND conservado, captura con `TakeSnapshot` a 16x16 y píxeles esperados comprobados después de entrar en PiP, retorno sin detener el medio y cierre PiP con reproducción activa liberada. No se abrió contenido del proveedor.
- `artifacts/f7-pip.png` inspeccionada visualmente: controles y retorno caben a 360x260; área negra corresponde al backend ficticio, no es una captura de IPTV. La regresión de layout 1/1,25/1,5/2 anterior sigue pasando; DPI y monitores físicos no se verificaron.
- `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Build-Candidate.ps1`: candidato win-x64 autocontenido **0.5.0-rc.2**, verificación offline de píxeles nativos con exit 0. Salida final: `artifacts/candidates/StreamDesk-0.5.0-rc.2-20261019-083350`. No se publica ni etiqueta; `ReleaseApproved=false` y revisión nativa pendiente permanecen. Instalador F6 sin cambios funcionales; su registro/acceso directo y dos PCs siguen pendientes.
- Parser de `Build-Candidate.ps1` y `git diff --check` correctos. Sin dependencias nuevas.

Incidencias: el botón inicial en la franja del reproductor desbordó la ventana pequeña en la regresión existente; se movió al encabezado y las escalas pasaron. La prueba de retorno inicialmente esperaba coordenadas -20000 fuera de pantalla; Windows las recupera por diseño. Se corrigió el fixture a una posición visible para comprobar retorno exacto, sin deshabilitar la recuperación del SO. Publish inicial produjo NU1900: un restore forzado/sin caché con acceso autorizado renovó la consulta NuGet y el publish final pasó sin advertencias.

## Checklist

| ID | Estado | Alcance |
|---|---|---|
| F7-T01 | COMPLETADA | Código y pruebas UI/nativas locales anteriores; validación física pendiente. |
| F7-T02 | PENDIENTE | Importación M3U fuera de este incremento. |
| F7-T03 | PENDIENTE | Revalidar alcance adicional de administración de conexiones cuando se priorice; perfiles existentes conservados. |
| F7-T04 | PENDIENTE | Sin sincronización/servidor nuevo. Requiere decisión opt-in. |
| F7-T05 | PENDIENTE | Sin personalización nueva de temas/idiomas/atajos. P es el atajo fijo de PiP. |
| F7-T06 | PENDIENTE | Sin grabación/catch-up/control parental; requiere factibilidad y autorización. |

## Aceptación y pendientes

| Criterio | Resultado |
|---|---|
| Incremento compilable, recorridos WPF y regresiones | PASA. |
| PiP/retorno/fullscreen, controles, cierre y un solo motor | PASA en fixtures y motor local, con límites indicados. |
| Continuidad de imagen/audio IPTV/VOD del proveedor | NO VERIFICADO. |
| Dos monitores, DPI físicos, interacción/teclado sobre HWND nativo y suspensión física | NO VERIFICADO. |
| F6 aprobada / versión estable 1.0 | NO PASA; siguen pendientes las pruebas/licencias anteriores. |

**Gate F7: PENDIENTE_DE_VALIDACION**, 1/6 tareas completas. No se aprueba la fase completa ni se inicia otra opcional. Siguiente paso: ejecutar `PRUEBA_MANUAL_F7_T01.md` y los protocolos pendientes de F6; corregir defectos demostrados antes de priorizar F7-T02. Riesgos pendientes: diferencias de DPI/monitores, foco de teclado sobre HWND nativo y comportamiento del proveedor real; no se prometen resultados sin esa sesión.
