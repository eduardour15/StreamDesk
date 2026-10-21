# Prompt — Optimizar fluidez, rendimiento y UX/UI de StreamDesk

## Objetivo y alcance

Actúa como desarrollador senior de C#/.NET 10, especialista en rendimiento WPF/MVVM y experiencia de reproducción con LibVLCSharp.

Analiza la implementación actual de StreamDesk, identifica fricciones y cuellos de botella y aplica mejoras justificadas para que las funciones existentes respondan con más rapidez, claridad y estabilidad. Prioriza buscar, navegar, reproducir y recuperarse de una interrupción sin bloquear la interfaz.

**El alcance es optimizar lo existente. No agregues funciones de producto.** Conserva capacidades, reglas de negocio, preferencias y comportamiento funcional del usuario. Puedes corregir defectos, mejorar feedback, distribución, accesibilidad y eficiencia interna. No conviertas esta intervención en una reescritura, un rediseño integral ni una nueva fase del producto.

Cuando se invoque el documento completo, diagnostica, prioriza e implementa las mejoras respaldadas por evidencia; no te limites a entregar recomendaciones. Si la solicitud especifica solo análisis, entrega el diagnóstico sin implementar. Si indica un ID, atiende únicamente ese bloque y sus dependencias imprescindibles.

## 1. Preparación obligatoria

1. Lee `AGENTS.md`, inspecciona `git status` y los cambios actuales y respeta el trabajo existente. No actives Ponytail salvo invocación explícita del usuario en la solicitud actual.
2. Lee `docs/ROADMAP.md`, `docs/ESTADO_FASES.md` y los ADR pertinentes, especialmente ADR-002, ADR-003, ADR-004, ADR-005, ADR-007 y ADR-010.
3. Consulta `docs/prompts/OPTIMIZACION_Y_UX.md` y `docs/prompts/REPRODUCTOR_INMERSIVO_Y_SERIES_ALEATORIAS.md` solo como antecedentes. Sus funciones ya implementadas y propuestas futuras no son una autorización para ampliar este alcance.
4. Establece una base de compilación y pruebas. Registra fallos previos y bloqueos ambientales por separado de tus cambios. No fijes una cantidad histórica de tests como criterio de éxito.
5. Inspecciona vistas, bindings, contratos, servicios y pruebas conectados a cada flujo. Verifica el código y la interfaz actuales cuando difieran de la documentación.
6. Ejecuta recorridos con perfiles, catálogos y medios ficticios/locales aislados. No leas credenciales ni modifiques los datos reales del usuario para preparar pruebas.

No cambies gates históricos ni marques como verificadas las pruebas de IPTV, DPI o hardware pendientes. Esta intervención es un incremento de calidad sobre la aplicación existente.

## 2. Contexto observado al preparar este prompt

Revisión del repositorio del **20 de octubre de 2026**. Revalida estas observaciones antes de actuar: describen el código revisado y oportunidades de investigación, no resultados de una sesión IPTV real.

| Área | Evidencia actual | Qué debes comprobar |
|---|---|---|
| Búsqueda de TV | `CatalogSearch` precalcula nombres normalizados. `ApplyFilterAsync` usa debounce de 180 ms, cancelación y trabajo de fondo. | Latencia completa hasta presentar resultados, reemplazo de colecciones, selección, scroll y consultas que se solapan. No añadir otro buscador. |
| Películas y series | `FilterMediaAsync` también espera 180 ms, pero vuelve a ejecutar `CatalogSearch.Normalize(item.Name)` sobre los títulos en cada filtrado. | Coste repetido y asignaciones con catálogos grandes; valorar índice normalizado por catálogo si la medición lo justifica. |
| Categorías y colecciones derivadas | `VisibleCategories`, `MediaCategories`, `VisibleMediaCategories`, `Episodes` y `ContinueWatching` filtran o crean arrays en getters. | Frecuencia de evaluación, trabajo en UI y pérdida de foco/cursor al renovar las listas del popup. |
| Listas e imágenes | Las listas principales tienen virtualización y reciclaje. `ChannelLogoLoader` limita a cuatro operaciones, 512 KiB por descarga y 500 entradas LRU; admite tamaños de decodificación. | Virtualización efectiva, tamaños realmente solicitados desde XAML, memoria decodificada, solicitudes duplicadas y reciclaje. No dar por aplicada una opción solo porque aparece en un documento anterior. |
| Carga de vídeo | Ya existen `IsVideoLoading`, `VideoLoadingIndicator`, progreso indeterminado y porcentaje condicional. | Calidad de transiciones, ubicación y estabilidad del layout. Mejorar ese indicador, sin duplicarlo. |
| Congelamiento | `PlaybackService` procesa `Playing`, `Buffering`, `Paused`, `Failed` y `Ended`; cancela el timeout al recibir `Playing`. No se observa supervisión independiente del progreso durante `Playing`. | Si una transmisión estancada sin nuevos eventos puede seguir figurando como reproducción normal y qué señales nativas permiten detectarlo de forma fiable. |
| Actualizaciones periódicas | La ventana tiene temporizadores de controles (250 ms) y detalles (2 s). Las lecturas de detalles reconstruyen arrays de pistas y notifican sus propiedades. | Trabajo innecesario en reposo, minimizar/restaurar, selección de pistas y contención con operaciones de reproducción. No reducir intervalos a ciegas. |
| Controles y estilo | Se conservan controles adyacentes al HWND, modo inmersivo, pantalla completa y PiP. La captura WPF del fixture a 1360×850 muestra una barra densa y título con poco espacio. `Theme.xaml` usa `StaticResource` y `ApplyTheme` sustituye recursos. | Legibilidad en distintos tamaños, botones y selectores compactos, foco y aplicación efectiva de temas. La corrección visual requiere verificar la vista. |

Los tests existentes cubren búsqueda con 5.000 canales ficticios, virtualización, cancelación, stream único y estados de carga/búfer. Su existencia no demuestra fluidez sostenida ni detección de un estancamiento silencioso. Las capturas de `RenderTargetBitmap` sirven para revisar layout WPF; no acreditan los píxeles del vídeo nativo ni la visibilidad real de overlays sobre él.

## 3. Diagnóstico y mediciones antes de cambiar

Reconstruye estos recorridos: abrir la app, cargar caché, buscar canal/título/categoría, desplazarse, abrir ficha/episodios, iniciar reproducción, cambiar de canal, pausar/reanudar, buscar posición, volver del modo inmersivo, entrar/salir de pantalla completa y PiP, detener y cerrar.

Para cada problema registra: ubicación en archivo/método o vista, pasos de reproducción, efecto para el usuario, evidencia disponible, causa probable, cambio mínimo y forma de comprobarlo. Clasifica como **confirmado**, **hipótesis por medir** o **pendiente de validación real**. Distingue rendimiento de la aplicación, demora de red/proveedor y decodificación; un loader no soluciona por sí mismo un bloqueo del hilo UI.

Mide con fixtures reproducibles de 5.000 y 20.000 elementos; añade otros tamaños solo si aportan evidencia. Incluye nombres con acentos, categorías numerosas, búsqueda sin coincidencias, imágenes ausentes y respuestas demoradas.

- Búsqueda: tiempo desde la última tecla hasta los resultados visibles, separando debounce, filtrado y actualización WPF; latencias p50/p95 con varias repeticiones y tamaño del catálogo.
- Respuesta de UI: retraso de entrada/Dispatcher durante búsqueda y scroll, filas materializadas y pérdida de selección/foco. Evita instrumentación permanente que genere trabajo por frame.
- Recursos: CPU en reposo/reproducción, memoria administrada y privada, asignaciones/GC cuando sea posible, descargas y ocupación de caché. Distingue calentamiento de crecimiento sostenido tras repetir recorridos.
- Vídeo: apertura, cambio de canal, interrupción, aparición y retirada del loader y recuperación. Separa fixtures nativos locales, backend simulado y proveedor real.

Compara antes/después en las mismas condiciones: hardware, compilación, tamaño del catálogo y estado frío/caliente de la caché. Puedes reutilizar `scripts/Measure-Playback.ps1` para CPU/RAM/GPU; no atribuyas a ese script mediciones de primer fotograma que no recoge. No inventes porcentajes de mejora ni presupuestos universales de RAM o latencia.

Presenta un plan breve ordenado por impacto, evidencia, esfuerzo y riesgo. Después continúa con los cambios autorizados sin introducir una aprobación adicional para ajustes reversibles dentro del alcance.

## 4. Bloques de trabajo

Referencias al ROADMAP para mantener trazabilidad sin modificar sus IDs:

| Bloque | Tareas existentes relacionadas |
|---|---|
| FLUX-T01 | F2-T06/T07/T08/T10, F4-T05, F5-T03 |
| FLUX-T02 | F3-T01/T05/T06/T07/T08/T09/T11, F6-T02 |
| FLUX-T03 | F1-T08, F4-T08, F3-T01/T09 |
| FLUX-T04 | F3-T03/T04, F4-T06/T07/T09/T10, F5-T06 |
| FLUX-T05 | F2-T07/T10, F5-T08, F6-T01 |
| FLUX-T06 | F1-T03/T08, F4-T01/T02/T09/T10, F7-T05 |

### FLUX-T01 — Buscadores y filtros fluidos

Archivos principales: `CatalogSearch.cs`, `MainViewModel.Catalog.cs`, `MainViewModel.Content.cs`, `MainViewModel.Library.cs` y los selectores de `MainWindow.xaml` / `MainWindow.xaml.cs`.

- Mantén búsqueda local, semántica actual por nombre, acentos/mayúsculas, categorías, favoritos, recientes y restricciones parentales. No agregues sugerencias, búsqueda remota, historial de consultas ni búsqueda difusa.
- Reutiliza nombres y categorías normalizados cuando haya coste repetido demostrado. Invalida los índices al cambiar catálogo/perfil; evita índices duplicados sin límite o normalización por elemento y pulsación.
- Conserva cancelación y la regla de que solo se publica el resultado de la consulta vigente, incluido cambiar de pestaña/perfil/categoría, borrar texto, cerrar o actualizar el catálogo durante una búsqueda.
- Ajusta el debounce con mediciones; no supongas que eliminarlo o aumentarlo mejora la respuesta. El texto, cursor y foco deben responder aunque el resultado anterior siga visible unos instantes.
- Evita limpiar y repoblar listas por cada tecla o notificar por fila. Conserva selección y scroll cuando tenga sentido; distingue un resultado final vacío de una búsqueda todavía pendiente.
- Verifica el buscador dentro del `Popup` de categorías: binding, foco, cursor, navegación por teclado y selección. No restaures el foco mediante colas ilimitadas de `Dispatcher.BeginInvoke`.
- Si hace falta feedback de filtrado, que sea discreto y local. No bloquees el reproductor ni muestres una carga global por cada tecla.

### FLUX-T02 — Reproducción, búfer y vídeo estancado

Archivos principales: `PlaybackService.cs`, `PlaybackService.Media.cs`, `PlaybackContracts.cs`, `PlaybackModels.cs`, `LibVlcBackend.cs` y `MainViewModel.Playback.cs`.

- Traza estados desde el backend hasta la vista. Diferencia apertura, reproducción, búfer, pausa, espera de reintento, fallo definitivo, fin natural, cancelación y parada sin inventar un segundo sistema de estados desconectado del servicio.
- Valida el escenario prioritario: **el vídeo estaba reproduciéndose, deja de avanzar y debe ofrecer feedback de espera en lugar de aparentar reproducción normal indefinidamente**.
- Primero determina qué eventos/indicadores soporta la versión instalada de LibVLCSharp/LibVLC. Si necesitas documentación, usa fuentes oficiales o el código/XML del paquete local. No supongas que existe una API ni que `Buffering` se emite ante todo congelamiento.
- Si se confirma el hueco, implementa la supervisión mínima necesaria en la capa de reproducción, con señales de progreso válidas y tiempo monotónico. No uses cambios visuales entre imágenes como detector. Un valor de tiempo cero/no disponible, una escena estática o un stream solo de audio no bastan para afirmar un fallo.
- Evita falsos positivos al pausar, hacer seek, arrancar, cambiar de medio, finalizar VOD, suspender o cerrar. Define y documenta umbrales, tolerancia a pausas breves y condiciones de recuperación. Hazlos verificables sin pruebas basadas en esperas arbitrarias largas.
- Reutiliza el indicador de carga existente para un estancamiento suficientemente respaldado. Al recuperar progreso válido, retíralo sin parpadeos ni estados rezagados.
- No atribuyas automáticamente la causa a Internet. Si faltan señales fiables, comunica la limitación y deja la detección no demostrada pendiente; no simules funcionamiento.
- Mantén el presupuesto existente de recuperación: como máximo dos reintentos para fallos elegibles de red/timeout, sin reiniciar el presupuesto al recibir `Playing`. No reintentes acceso denegado, expiración, límite de conexiones o formato incompatible. Preserva el fallback de hardware según su clasificación real.
- Cancela monitores, temporizadores y recuperaciones al detener, cambiar perfil/medio o disponer recursos. Ignora eventos antiguos y no reinicies un vídeo cancelado por acción del usuario.

### FLUX-T03 — Loaders y estados de espera coherentes

Archivos principales: `MainWindow.xaml`, `MainViewModel.Playback.cs`, `MainViewModel.Catalog.cs` y `MainViewModel.Content.cs`.

- Mejora `VideoLoadingIndicator`, no añadas otro loader en paralelo. Apertura inicial sin progreso fiable: indeterminado; porcentaje solo cuando lo informa el motor. No inventes avance ni interpretes búfer al 100% como primer fotograma demostrado.
- Distingue mensajes de apertura, carga de búfer y recuperación sin saturar la interfaz con textos contradictorios o varios indicadores para la misma operación.
- Conserva feedback perceptible durante un bloqueo real y elimínalo al terminar, cancelar, pausar o fallar. Una espera de reintento debe ser comprensible y no parecer una transmisión ya recuperada.
- Evita que mostrar/ocultar loaders y controles redimensione repetidamente el vídeo o desplace controles bajo el cursor. Elige una solución de layout estable acorde con la ventana disponible; no reserves espacio excesivo de forma permanente.
- Respeta airspace: el loader y controles críticos deben ser visibles con el HWND nativo activo, también en inmersivo, fullscreen y PiP. No pongas un spinner en un `Grid` sobre `VideoView` dando por hecho que quedará visible.
- Revisa cargas de catálogo, detalles, episodios e imágenes: feedback junto al área afectada, caché utilizable durante refresco, cancelación efectiva y diferencia clara entre cargando, vacío, sin coincidencias y error.
- Usa placeholders/skeletons solo donde aclaren una carga real. Prefiere soluciones ligeras, sin shimmer permanente ni efectos que eleven CPU/GPU. El lector de pantalla no debe anunciar cada variación porcentual de manera invasiva.

### FLUX-T04 — Controles de reproducción y ergonomía

Archivos principales: `MainWindow.xaml`, `MainWindow.xaml.cs`, `PlaybackShortcuts.cs`, `WindowDisplays.cs` y `Resources/Theme.xaml`.

- Conserva las acciones ya disponibles: play/pausa, detener, anterior/siguiente, volumen/mute, amplificación, seek donde esté soportado, pistas, subtítulos, formato, reintento y modos de ventana. No cambies valores predeterminados ni añadas nuevos atajos/acciones.
- Ajusta distribución, jerarquía, espacios y tamaños para que las acciones principales sean identificables y alcanzables sin recortes. Verifica especialmente el título central y los selectores de pistas en ventanas estrechas, escalado y PiP.
- Expresa estados pulsado, foco, activo, deshabilitado y no aplicable. No dejes un control que parece operativo cuando no puede ejecutar su acción ni ocultes una acción necesaria durante un error.
- Revisa autoocultado y reaparición con ratón, teclado, toque, foco, captura del slider y popups abiertos. No colapses controles durante una interacción ni hagas saltar el tamaño del vídeo con cada aparición.
- Comprueba clic frente a doble clic sobre vídeo, temporizadores OSD y teclado: una interacción no debe cancelar otra ni pausar por accidente al entrar en fullscreen.
- Durante seek, evita que las actualizaciones periódicas arrastren el thumb contra la intención del usuario. Conserva posición elegida y estado coherente tras terminar/cancelar la interacción.
- Preserva búsqueda, categoría, selección y contexto al volver del modo inmersivo o de la ficha. No recargues el catálogo ni abras otro stream por cambiar la presentación de la ventana.
- Mejora tooltips, nombres accesibles, contraste y orden de tabulación. Usa la iconografía Fluent y el sistema visual existente; evita introducir una librería o sustituir la identidad de la aplicación.

### FLUX-T05 — Menos trabajo y consumo de recursos

Archivos principales: `MainWindow.xaml.cs`, `MainViewModel.Content.cs`, `ChannelLogo.cs`, `ChannelLogoLoader.cs`, `App.xaml.cs`, `AppServices.cs`, `SqliteCatalogStore.cs` y `SqliteContentStore.cs`.

- Mantén virtualización y reciclaje reales al hacer scroll, filtrar y renovar colecciones. No sustituyas listas por `WrapPanel`/grids que materialicen todo el catálogo. Cambia opciones de scroll/cache solo después de comprobar su efecto.
- Revisa reconstrucción de arrays, cálculos en getters, conversiones, LINQ repetido, notificaciones redundantes y renovación de pistas cada dos segundos. Notifica cambios reales y conserva la selección al refrescar metadatos.
- Examina las colas de audio al arrastrar volumen/amplificación: si hay acumulación, consolida valores pendientes preservando el último valor y sin retrasar parar/cambiar de medio.
- Revisa temporizadores activos sin trabajo útil, lecturas nativas, persistencia y trabajo en ventanas minimizadas. Reduce lo innecesario sin dejar de detectar interrupciones ni perder progreso o recuperación al restaurar.
- Comprueba arranque/caché y operaciones SQLite/JSON fuera del hilo UI. No añadas `Task.Run` indiscriminado ni bloqueos síncronos. Prioriza que ventana y entradas estén disponibles sin cambiar la política de autoplay.
- Verifica tamaño de decodificación realmente utilizado por cada imagen, límites de memoria decodificada y cancelación al reciclar. Si hay descargas concurrentes duplicadas, corrígelas sin que cancelar un consumidor cancele a todos los demás.
- Revisa fallos de imágenes guardados como ausencia permanente, presión de caché, suscripciones y limpieza determinista. No añadas caché de disco ni aumentes la RAM como solución automática.
- No migres a FTS5, tablas VOD nuevas, otro motor multimedia ni otra arquitectura sin necesidad demostrada para resolver el defecto dentro de este alcance. Prefiere el arreglo local respaldado por el diagnóstico.

### FLUX-T06 — Consistencia visual de los flujos existentes

- Revisa TV, películas, series, favoritos, recientes, continuar y ajustes usando datos ficticios. Corrige truncamientos relevantes, desalineación, mensajes confusos, estados inconsistentes y pérdida de foco; no reorganices toda la navegación.
- Comprueba que el tema claro/oscuro se aplica realmente a los recursos y controles visibles. No consideres un selector de tema funcionando solo porque guarda la preferencia.
- Verifica ventanas pequeñas, maximizado, escalas de UI disponibles y DPI 100–200%. Distingue una simulación de layout de pruebas con monitores físicos.
- Mantén animaciones breves y de bajo coste solo donde aporten feedback. No añadas desenfoques, sombras complejas, transiciones de layout, adornos o animaciones continuas por estilo.
- No implementes funciones mencionadas en documentos antiguos pero ausentes del código salvo corrección de una regresión demostrada de una función existente y dentro del alcance solicitado.

## 5. Reglas técnicas y límites

- Conserva .NET 10, WPF/WPF UI, MVVM, SQLite y LibVLCSharp. La lógica de reproducción pertenece a Application/Playback; el code-behind coordina presentación e interacción de ventana.
- **Un solo `VideoView`, `MediaPlayer` y stream activo.** Sin precarga de canales, conexiones especulativas, reproductores auxiliares ni reintentos paralelos.
- No agregues EPG en rejilla, recomendaciones, descargas, grabación, catch-up, nuevos modos, nuevas fuentes ni ampliaciones de catálogo. No prepares arquitectura para futuras funciones.
- No reduzcas calidad, desactives hardware o cambies el buffer por defecto para aparentar fluidez sin demostrar el efecto y respetar las preferencias del usuario.
- Red, decodificación, persistencia y trabajo pesado fuera del hilo UI; cancelación, gestión de errores y disposición determinista. No `Thread.Sleep`, `.Wait()` ni `.Result` en recorridos de interfaz.
- No expongas secretos, URLs autenticadas, mensajes nativos, datos privados ni contenido de cuenta en evidencias, logs o herramientas externas. Conserva DPAPI CurrentUser y las protecciones actuales de HTTP/URLs/imágenes. No relajes filtros de URL para mejorar la tasa de carga.
- No agregues dependencias ni infraestructura de observabilidad salvo necesidad técnica demostrada. La instrumentación de diagnóstico debe ser acotada, local y sin datos personales.
- No hagas push, releases, cambios de proveedor, borrados de datos reales ni migraciones ajenas al problema.

## 6. Validación y criterios de aceptación

Reutiliza las pruebas de `CatalogTests`, `CatalogUiTests`, `UxOptimizationTests`, `PlaybackTests`, `PlaybackViewModelTests`, `ContentTests`, `ShortcutTests`, `ThemeAndParentalControlTests` y las regresiones relacionadas. Añade pruebas útiles para lógica/riesgos que cambies; no tests que solo comprueben constantes o copien el código.

| Escenario | Resultado requerido |
|---|---|
| Escribir rápido, borrar y cambiar filtros/perfil/pestaña | Texto y foco responden; solo se aplica la consulta vigente; no aparecen resultados de otra sesión. |
| Buscar y hacer scroll con 5.000/20.000 elementos mientras se reproduce | Virtualización efectiva y respuesta medible sin bloqueo UI; no se deteriora la reproducción por trabajo del catálogo. |
| Abrir vídeo lento y recibir búfer con/sin porcentaje | Indicador existente visible y coherente; sin progreso ficticio, salto repetido de layout ni acciones bloqueadas innecesariamente. |
| Estancamiento después de `Playing`, sin nuevo evento `Buffering` | Detección/feedback si existen señales válidas; recuperación al volver el progreso o error/reintento acotado. Documentar limitaciones no verificadas. |
| Pausa larga, seek, fin natural VOD y medios sin reloj útil | Sin falsos congelamientos, reintentos indebidos ni avance automático por error. |
| Detener, cerrar o cambiar durante carga/recuperación | Se cancelan tareas y loader anteriores; sin reproducción posterior ni recursos huérfanos. |
| Cambiar repetidamente de canal y alternar modos de ventana | Sigue existiendo un stream; eventos obsoletos ignorados y contexto preservado. |
| Interactuar con sliders, pistas, popup y doble clic | Sin autoocultado durante interacción, saltos del thumb, selección perdida ni play/pausa accidental. |
| Catálogo lento, vacío, sin coincidencias o fallido | Mensaje correcto junto al área afectada; caché conservada y controles existentes de cancelar/reintentar efectivos. |
| Repetir navegación y parar | Sin crecimiento sostenido inexplicado de memoria, tareas, suscripciones o solicitudes; CPU en reposo comparada antes/después. |
| Tema claro/oscuro, teclado y tamaños/DPI | Texto y foco legibles, controles accesibles y ningún elemento crítico recortado. |

Usa backend controlado y reloj controlable para casos de cancelación, señales tardías y estancamiento cuando corresponda. Para el comportamiento nativo usa un medio local/servidor local controlado; no pruebas deliberadas contra el proveedor real. Verifica también el resultado visual; una prueba de ViewModel no demuestra visibilidad del loader sobre HWND.

Ejecuta desde la raíz:

```powershell
dotnet build StreamDesk.sln -c Release
dotnet test StreamDesk.sln -c Release --maxcpucount:1
```

Si el entorno requiere ejecución serial o desactivar compilación compartida, registra el comando exacto y la causa. No ocultes fallos ni clasifiques tests omitidos como pasados. Cuando no puedas verificar IPTV, monitores, GPU o una condición de red, entrega pasos reproducibles y marca **PENDIENTE_DE_VALIDACION**.

Una mejora queda terminada cuando existe evidencia del defecto o coste, cambio acotado, comprobación del comportamiento y ausencia de regresiones pertinentes. Si una hipótesis no se confirma, descártala con su evidencia en lugar de implementar trabajo innecesario. No atribuyas más fluidez a un cambio puramente visual sin medirla.

## 7. Entrega y registro

1. Explica los problemas confirmados y el efecto antes/después para el usuario, con archivos/métodos y evidencia.
2. Entrega una tabla por `FLUX-T01` a `FLUX-T06` con hallazgos, cambios, estado y validación. Relaciona con los IDs pertinentes del ROADMAP sin renumerarlos; identifica hipótesis descartadas y pendientes sin marcar todo como completado.
3. Resume mediciones comparables, comandos/resultados y revisión visual. Separa pruebas simuladas, WPF, multimedia nativa local y proveedor/hardware real.
4. Actualiza `docs/ESTADO_FASES.md` con fecha, alcance y evidencia de este incremento sin alterar aprobaciones históricas ni aprobar una release.
5. Enumera validaciones manuales pendientes y limitaciones concretas. No inicies otra fase ni añadas un backlog de funciones nuevas.

El resultado esperado es la misma aplicación, con sus funciones actuales, más ágil al buscar y navegar, controles más cómodos y feedback fiable cuando el vídeo tarda o se interrumpe.
