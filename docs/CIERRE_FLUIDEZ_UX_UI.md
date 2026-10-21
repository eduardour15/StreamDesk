# StreamDesk — Cierre del incremento de fluidez y UX/UI

Fecha: 2026-10-20. Prompt ejecutado: [OPTIMIZACION_FLUIDEZ_UX_UI.md](prompts/OPTIMIZACION_FLUIDEZ_UX_UI.md).

Se optimizaron las funciones actuales sin añadir capacidades de producto, paquetes ni migraciones de datos. Se conservaron los cambios documentales previos, el stream único, preferencias y gates históricos. Decisiones: [ADR-011](ADR/ADR-011-fluidez-busqueda-estancamientos-y-estados.md).

## Estado por bloque y trazabilidad

Los estados distinguen implementación local comprobada de criterios que todavía requieren medios/red/hardware reales.

| ID | ROADMAP relacionado | Estado | Resultado y evidencia | Pendiente |
|---|---|---|---|---|
| FLUX-T01 | F2-T06/T07/T08/T10, F4-T05, F5-T03 | COMPLETADA | Índice VOD reutilizable; debounce TV/VOD 120 ms tras medir; cancelación y publicación por sesión/pestaña/petición; vacío definitivo separado de filtrado. Búsquedas normalizadas, restricciones y escritura rápida verificadas con 20.000 títulos. WPF mantiene siete filas materializadas en la muestra de 5.000/20.000. | Evaluación subjetiva de uso diario y catálogo del proveedor, sin atribuir compatibilidad real. |
| FLUX-T02 | F3-T01/T05/T06/T07/T08/T09/T11, F6-T02 | PARCIAL | Supervisión de progreso fuera de UI; carga ante estancamiento y recuperación, pausa/seek, cancelación y dos reintentos máximos verificados con backend y reloj controlados. API nativa de progreso comprobada con el reloj de un BMP local y decodificación nativa de veinte fixtures. | Congelamiento de vídeo nativo con red controlada y pruebas físicas TS/HLS/GPU/proveedor. Un reloj que avanza no demuestra presentación de fotogramas si no hay estadísticas válidas. |
| FLUX-T03 | F1-T08, F4-T08, F3-T01/T09 | COMPLETADA | Loader existente para apertura/búfer/espera de reintento; porcentaje solo del motor. Detener cancela también el backoff. Franja fija sin salto de tamaño del vídeo entre carga, Playing y controles ocultos; loader fuera del rectángulo de vídeo. Vacío VOD con orientación para los controles existentes. Pruebas WPF/ViewModel y capturas. | Validación perceptiva con vídeo/proveedor reales incluida en el gate, sin confundir capturas WPF con píxeles nativos. |
| FLUX-T04 | F3-T03/T04, F4-T06/T07/T09/T10, F5-T06 | PARCIAL | Título con ancho propio, acciones que ajustan filas, autoocultado sin cambiar tamaño, captura/popup protegidos, pausa en búfer, navegación de categorías, temporizadores OSD independientes y limpieza al cerrar. Regresiones de fullscreen/PiP, atajos y escalas simuladas pasan. | DPI y dos monitores físicos, ratón/touchpad/táctil y evaluación humana de controles. |
| FLUX-T05 | F2-T07/T10, F5-T08, F6-T01 | PARCIAL | Menos asignaciones VOD; pistas estables sin restablecer selección pendiente; EPG sin notificaciones idénticas; cambios de audio consolidados; temporizadores inactivos sin trabajo. Imágenes compartidas con cancelación por consumidor, fallos negativos con TTL, tamaños de póster contextuales y límite de 32 MiB de píxeles estimados además de 500 entradas. Pruebas de concurrencia, cancelación y cuotas pasan. | CPU/RAM/GPU sostenidos con códec/equipo reales y ciclos largos después del calentamiento. No se afirma menor RAM total del proceso. |
| FLUX-T06 | F1-T03/T08, F4-T01/T02/T09/T10, F7-T05 | PARCIAL | Pinceles dinámicos y tema WPF UI sincronizado, incluyendo inputs, superficies, estados y botones de ventana. Revisión visual de tema claro, oscuro, loader y título. Popup con búsqueda/teclado verificado. | Contraste y navegación humana en todos los tamaños/monitores físicos. |

**Gate del incremento: PENDIENTE_DE_VALIDACION.** No se aprueba una release ni se modifica la aprobación de fases anteriores. Los bloques parciales tienen código y pruebas; los pendientes anteriores no se convierten en evidencia por ausencia de errores automatizados.

## Mediciones reproducibles

Base: compilación Release sin cambios de aplicación, suite de 177 pruebas. Se incorporó el mismo recorrido WPF de medición antes de editar la aplicación. Ejecución final: Release, .NET runtime 10.0.12, 12 procesadores lógicos reportados, mismo equipo, ventana 1360×850, fixtures inventados sin imágenes remotas, caché caliente y ocho consultas alternas por tamaño. No es una sesión IPTV ni una medición de primer fotograma.

La latencia visible se mide desde la modificación del textbox hasta resultado y layout WPF; incluye debounce, trabajo de fondo y Dispatcher. Se redujo explícitamente el debounce de 180 a 120 ms después de medir el índice y comprobar cancelación. Por ello, la mejora visible no se atribuye íntegramente al algoritmo de filtrado.

| Catálogo | Visible p50 antes / después | Visible p95 antes / después | Asignaciones durante ocho consultas antes / después | Filas materializadas antes / después |
|---|---|---|---|---|
| 5.000 | 216,74 / 184,45 ms | 247,06 / 199,41 ms | 15,77 / 8,74 MB | 7 / 7 |
| 20.000 | 230,36 / 158,84 ms | 299,95 / 197,94 ms | 46,59 / 11,24 MB | 7 / 7 |

El retraso p95 del heartbeat del Dispatcher (exceso sobre intervalo de 16 ms) pasó de 29,53 a 30,76 ms con 5.000 y de 30,91 a 25,84 ms con 20.000. Son muestras cortas, sensibles a carga/JIT/planificación; no constituyen una garantía universal ni demuestran mejora en todos los indicadores. Las asignaciones son bytes creados durante el recorrido, no memoria retenida.

Una comparación adicional ejecuta el predicado anterior de normalización y el índice caliente en el mismo proceso, con doce consultas y resultados equivalentes, excluyendo UI, red y debounce. **Es una referencia del algoritmo anterior, no otra ejecución de la aplicación anterior.**

| Elementos | Filtrado p50 predicado anterior / índice | Filtrado p95 anterior / índice | Construcción inicial del índice |
|---|---|---|---|
| 5.000 | 1,66 / 0,044 ms | 2,82 / 0,084 ms | 1,01 ms |
| 20.000 | 6,40 / 0,165 ms | 7,59 / 8,04 ms | 6,28 ms |

El coste p95 aislado muestra variación de planificación/GC: con 20.000 no mejoró la cola de esa muestra, aunque sí la mediana. No se extrapola a IPTV. CPU y memoria privada se capturaron como diagnóstico, pero no demuestran una reducción global: con 20.000, la muestra pasó de 171,88 a 281,25 ms de CPU y de 298,12 a 320,50 MB privados. La suite final y la ejecución base aislada tienen diferente calentamiento y actividad del proceso. La conclusión respaldada es menor trabajo de normalización/asignaciones y menor latencia visible en estos fixtures; consumo sostenido queda pendiente.

Artefactos locales ignorados por Git, regenerables con tests:

- `artifacts/flux-search-before.json`: medición anterior; no se sobrescribe en una ejecución normal posterior.
- `artifacts/flux-search-after.json`: medición WPF final.
- `artifacts/flux-filter-cost.json`: comparación aislada de filtrado.
- `artifacts/flux-loading.png`, `artifacts/flux-light.png`, `artifacts/flux-media-5000.png`, `artifacts/flux-media-20000.png`: revisión de layout WPF con datos inventados. No muestran ni acreditan reproducción de proveedor real.

No se cambió el panel de listas por una rejilla sin virtualización, no se añadieron índices FTS5 ni caches generales de todos los getters. La búsqueda de TV ya tenía un índice correcto y se conservó. Las operaciones de SQLite existentes se mantuvieron en segundo plano; no se identificó evidencia para migrar el esquema o reescribir la persistencia.

## Archivos de implementación y verificación

- Application: `MediaCatalogSearch.cs`, `PlaybackContracts.cs`, `PlaybackService.cs`, `PlaybackService.Progress.cs`, `PlaybackService.Media.cs`.
- Desktop: `MainViewModel.Catalog.cs`, `MainViewModel.Content.cs`, `MainViewModel.Playback.cs`, `MainViewModel.Epg.cs`, `MainWindow.xaml`, `MainWindow.xaml.cs`, `Resources/Theme.xaml`, `ChannelLogoLoader.cs`.
- Playback: `LibVlcBackend.cs`.
- Tests: `FluidityTests.cs`, `ImageFluidityTests.cs`, `SearchPerformanceTests.cs`, `CatalogUiTests.Fluidity.cs`, integración en `CatalogUiTests.cs`, fixtures de `PlaybackTests.cs` y lectura nativa en `PlaybackRuntimeTests.cs`.
- Documentación: ADR-011, este cierre y nueva entrada de `ESTADO_FASES.md`; se conservan el prompt y su índice preparados en la solicitud anterior.

Comandos finales desde la raíz:

```powershell
dotnet build StreamDesk.sln -c Release --no-restore --maxcpucount:1 -p:UseSharedCompilation=false -nodeReuse:false
dotnet test StreamDesk.sln -c Release --no-build --no-restore --maxcpucount:1
git -c core.safecrlf=false diff --check
```

Resultado: **compilación con 0 advertencias y 0 errores; 188 pruebas pasadas, 0 fallidas y 0 omitidas**. Se usó compilación serial sin servidor compartido por la cancelación `MSB5021` observada durante la preparación previa, sin atribuirla a un defecto de esta implementación. `--no-restore` usa los paquetes ya instalados y no cambia sus versiones.

Durante integración se detectaron y corrigieron una regresión al aceptar `Playing` después de una pausa señalada por el backend y la sustitución de fondo por `FluentWindow`. La prueba de estadísticas nativas se corrigió al comprobar que el módulo BMP no publica contadores de fotogramas, usando su reloj real como evidencia limitada. La lectura de progreso también revalida la sesión después del `await` nativo, con una prueba que mantiene esa lectura en vuelo mientras cambia de canal y comprueba que no publica un búfer del medio anterior. El resultado final anterior corresponde a los cambios posteriores a esas correcciones.

Para repetir solo el recorrido WPF final:

```powershell
dotnet test StreamDesk.sln -c Release --no-build --no-restore --maxcpucount:1 --filter FullyQualifiedName~WpfCatalogVirtualizes
```

La variable de test `STREAMDESK_FLUX_RUN=before` se utilizó únicamente sobre el binario previo para registrar la base. No usarla sobre el código optimizado para reconstruir una base anterior ficticia.

## Pruebas manuales pendientes y límites

1. En Windows, abrir la compilación normal y usar una cuenta autorizada introducida únicamente en sus formularios. Probar TV, película y episodio, búsquedas y categorías durante reproducción; volver del modo inmersivo y conservar selección/filtros. Registrar condiciones sin URLs autenticadas ni datos de cuenta.
2. Con un medio continuo autorizado servido desde un servidor **local controlado**, interrumpir y restaurar su entrega. Verificar loader después de avance previo, retorno al recuperar señal, y Detener/cambio/cierre durante espera y reintento. Probar vídeo congelado con audio activo, medio sin estadísticas útiles, pausa prolongada y seek. Los tests de reloj controlado no sustituyen esta prueba nativa de interrupción.
3. Confirmar loader y controles en ventana, inmersivo, fullscreen y PiP; comprobar sliders, popups, clic/doble clic y que una interacción no oculte controles. Repetir con ratón, touchpad y táctil cuando exista.
4. Probar DPI físicos 100–200%, dos monitores y escalas de UI disponibles; revisar título, errores largos, foco/teclado y contraste en ambos temas. El escalado simulado de la suite no acredita monitores físicos.
5. Con una sola instancia reproduciendo, ejecutar `./scripts/Measure-Playback.ps1 -DurationSeconds 120 -IntervalSeconds 1` y registrar hardware, códec, resolución, tipo de fuente y buffer. Comparar sesiones equivalentes, repetir navegación y al menos veinte cambios de canal, reposo, minimizar/restaurar y cierre. El script no mide primer fotograma: registrarlo por separado.

Estas verificaciones permanecen **PENDIENTE_DE_VALIDACION**. Próximo paso recomendado: completar esta validación del incremento y los pendientes técnicos previos del proyecto. No se inicia otra fase ni se publica un paquete/release.
