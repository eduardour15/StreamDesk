# F4 — MVP 0.1: implementación y evidencia

Fecha: 2026-10-18 (America/Managua). Alcance: F4-T01 a F4-T11. F3 se cerró por instrucción expresa del usuario; no se atribuyen resultados a las pruebas físicas que no aportó. F4 partió de 0/11 y entrega el código de MVP y un candidato local; F5 no se inicia.

## Implementación

- `MainWindow.xaml`, `Resources/Theme.xaml`: navegación lateral, Inicio con último canal, TV/favoritos/recientes sobre una misma vista virtualizada, búsqueda, categorías frecuentes y estados oscuros de selección/foco/deshabilitado. Configuración desplazable mantiene cuentas y conexión temporal. El reproductor ocupa el espacio principal; ventana mínima 760×540 DIP.
- `MainViewModel.Library.cs`, `IViewerStore.cs`, `SqliteViewerStore.cs`, `ViewerMigration.cs`: esquema v3, favoritos por perfil/tipo/ID, últimos 50 canales distintos, último perfil y autoplay por perfil apagado de fábrica. Historial solo después de Playing, una vez por solicitud, sin repetir por buffering/reintento. Migración consolida duplicados y descarta referencias retiradas; borrar perfil elimina su biblioteca/preferencias.
- `MainViewModel.Catalog.cs` / `.Playback.cs`: autenticación con DPAPI al reproducir desde caché, recientes en orden de reproducción, categorías frecuentes desde historial y recuperación de preferencias al reiniciar. La sesión temporal no escribe favoritos/historial de una cuenta guardada. Cambiar perfil o suspender no activa autoplay.
- `PlaybackShortcuts.cs` y presentación: Espacio reproduce/detiene; ←/→ canal anterior/siguiente; ↑/↓ volumen fuera de listas/inputs; F pantalla completa; M mute; Esc sale de pantalla completa. Inputs, combos, slider y botones conservan sus teclas. ↑/↓ en listas conserva selección normal.
- Barra de controles WPF **adyacente** al video nativo: no depende de overlays. Se oculta tras 3 s solo al reproducir, sin foco ni mouse en controles. Movimiento, teclado, carga o error la recuperan. El cursor se consulta periódicamente porque el HWND nativo recibe sus propios mensajes. Fullscreen oculta navegación/filtros sin sustituir VideoView. VLC no procesa sus hotkeys/mouse internos en paralelo a los controles de la aplicación.
- `Build-Mvp.ps1`, `packaging/Install-Mvp.ps1`: candidato Release win-x64 con DLL/plugins x64, inventario/avisos de paquetes y copia a carpeta nueva por usuario. Requiere **.NET 10 Windows Desktop Runtime**, no es self-contained. `--verify-runtime` comprueba inicialización/cierre nativos sin red, ventana ni lectura de perfiles/datos del usuario. Sin publicación ni etiqueta de release.

## Evidencia ejecutada

1. `dotnet build StreamDesk.sln --no-restore --verbosity minimal --maxcpucount:1 -p:UseSharedCompilation=false`: 0 errores y 0 advertencias.
2. `dotnet test StreamDesk.sln --no-build --no-restore --verbosity minimal --maxcpucount:1`: **113 correctos, 0 fallidos/omitidos**. Incluye las 100 regresiones previas, 4 tests de biblioteca, 8 casos de política de teclado y una regresión de selección de perfil durante arranque. Se amplió el caso WPF existente con el recorrido MVP y reproducción nativa alojada en ventana.
3. Biblioteca real SQLite en directorios temporales: reinicio, separación perfil/tipo, escritura fallida/cancelada, historial único/acotado, fechas recientes, migración v2→v3 e idempotencia; v1→v3 sigue cubierto por la suite previa. No se usan datos del usuario.
4. Ventana WPF real con servicios ficticios: alta de perfil, búsqueda, favorito, reciente, reinicio sin autoplay, opt-in con una apertura, suspensión/cambio de perfil sin autoplay, preservación de un VideoView, teclas de edición sin efectos de playback y controles que se ocultan/reaparecen. Las 5.000 filas siguen virtualizadas. Contraste de tokens de texto/acento contra las tres superficies ≥4,5:1; no equivale a certificar toda la accesibilidad.
5. Layout a factores 1/1,25/1,5/2 usando LayoutTransform y ventanas redimensionadas: controles dentro del espacio, sin desbordamiento. **Es simulación de escala, no cambio de DPI físico del sistema.** `artifacts/f4-mvp.png` renderizado e inspeccionado; `artifacts/f4-ui-evidence.json` identifica datos ficticios.
6. LibVLC real en VideoView WPF: BMP local generado, estado Playing, HWND asignado, áreas de video/controles sin intersección y botón WPF que detiene el medio. No se afirma imagen/audio de IPTV ni clic físico en monitores.
7. `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Build-Mvp.ps1`: candidato final en `artifacts/mvp/StreamDesk-0.1-candidate-20261018-163820`; comprobación nativa con código de salida 0. Instalador probado con `-InstallDirectory artifacts/mvp-install-20261018-163820` y `-SkipShortcut`, conservando datos locales reales intactos. La copia instalada también devuelve 0 en `--verify-runtime`; hashes de ejecutable/DLL administrada/libvlc/libvlccore coinciden. No contiene archivos .db/.dpapi/.env. El acceso directo y equipo limpio necesitan verificación manual.

La primera publicación local con MSBuild paralelo falló sin errores detallados. Se fijó `--maxcpucount:1`. La restauración restringida informó NU1900 por no acceder a NuGet; con acceso autorizado se restauró y generó el candidato sin esas advertencias. No se deshabilitó la auditoría NuGet. La directiva de PowerShell se limitó al proceso de compilación mediante `-ExecutionPolicy Bypass`, sin cambios permanentes.

La revisión final reprodujo una carrera: cambiar de perfil durante la carga inicial podía evaluar autoplay sobre el nuevo perfil. El test aislado falló antes de corregirla; después se reserva una sola evaluación de arranque y se exige que continúe la sesión restaurada. La suite completa de 113 pasa. También se verifica reintentar el canal fallido con selección eliminada por filtros.

## Checklist

| ID | Estado | Evidencia / límite |
|---|---|---|
| F4-T01 | COMPLETADA | Sidebar, jerarquía, tema y estados; captura revisada, foco y tokens comprobados. |
| F4-T02 | COMPLETADA | Lista virtualizada y video protagonista; HWND nativo alojado y botones operables en prueba WPF. |
| F4-T03 | COMPLETADA | Toggle/filtro inmediato, persistencia y aislamiento perfil/tipo con SQLite real. |
| F4-T04 | COMPLETADA | Recientes/último canal, reinicio, autoplay opt-in y ausencia de autoplay al cambiar perfil/suspender. |
| F4-T05 | COMPLETADA | Búsqueda 180 ms sin acentos, categorías frecuentes y limpieza; regresión de 5.000 canales. |
| F4-T06 | COMPLETADA | Política de teclado y evento WPF de edición probados; protocolo físico sobre video pendiente en T09/T10. |
| F4-T07 | COMPLETADA | Franja adyacente, geometría con HWND real, autohide/reaparición y botón que detiene VLC. |
| F4-T08 | COMPLETADA | Progreso/cancelar, placeholders/vacíos/filtros, errores seguros/red/timeout/vencimiento y reintentar/actualizar; suites de UI/proveedor/playback. |
| F4-T09 | PARCIAL | Layout 100–200% simulado, ventana pequeña y contraste probados; faltan DPI físicos, dos monitores y evaluación completa con teclado. |
| F4-T10 | PARCIAL | Recorrido automatizado con datos ficticios/reinicio pasa; falta evaluación humana de primera instalación y reproducción IPTV autorizada. |
| F4-T11 | COMPLETADA | Candidato reproducible generado y copia de instalación probada; defectos registrados en `DEFECTOS_F4.md`. No se etiqueta release. |

## Aceptación y gate

| Criterio | Resultado |
|---|---|
| Configurar una vez, explorar, buscar y marcar favoritos | PASA en WPF con proveedor ficticio. |
| Persistencia de favoritos/recientes/preferencia y arranque sin consumo de stream por defecto | PASA en SQLite/WPF; autoplay explícito abre una sola solicitud. |
| Reproducción y controles preservados | PASA con LibVLC/local y regresiones; IPTV actual NO VERIFICADO. |
| UI estable/accesible en DPI físicos y dos monitores | NO VERIFICADO; simulación y contraste sí pasan. |
| Paquete local 0.1 evaluable | PASA generación/copia/runtime en este equipo; instalación en equipo limpio NO VERIFICADO. |

**Gate F4: PENDIENTE_DE_VALIDACION.** 9 tareas COMPLETADA y 2 PARCIAL. Ejecutar `PRUEBA_MANUAL_F4.md` y registrar resultado sin datos de cuenta. Riesgos: entrada física sobre HWND, DPI/monitor, recorrido humano y revisión completa de avisos/fuentes de plugins antes de distribución externa. No se publica ni inicia F5.
