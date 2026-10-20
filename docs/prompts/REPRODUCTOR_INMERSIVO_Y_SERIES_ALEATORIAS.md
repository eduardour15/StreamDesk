# PROMPT EJECUTABLE — REPRODUCTOR INMERSIVO, EPISODIOS ALEATORIOS Y AUDIO

> **Invocación:** `Ejecuta el prompt de reproductor inmersivo` o `Ejecuta PLAY-UX-T01` … `PLAY-UX-T04` (también se puede indicar una tarea concreta).
> Es un incremento posterior a F7; no inicia ni reabre una fase del roadmap.

## Objetivo

Mejorar la experiencia diaria de reproducción de StreamDesk a partir de la captura de referencia y del estado actual del repositorio. Al iniciar una reproducción, el contenido debe ser protagonista y aprovechar la pantalla disponible. El usuario debe poder regresar al catálogo/contexto desde un control claro. Añadir selección aleatoria de episodios, una mejora de volumen técnicamente honesta y una señal visible de carga.

## Lectura obligatoria antes de implementar

1. Lee `AGENTS.md`, `docs/ROADMAP.md`, `docs/ESTADO_FASES.md`, `docs/prompts/OPTIMIZACION_Y_UX.md` y los ADR pertinentes, como mínimo ADR-003, ADR-004, ADR-005 y ADR-007.
2. Inspecciona `git status`, los cambios recientes y el código actual. Reutiliza el modo enfocado de episodios, pantalla completa, PiP, controles, estados de reproducción y almacenamiento que ya existan; no dupliques funciones implementadas.
3. Usa la imagen como referencia de UX, no como especificación técnica. Confirma cada necesidad contra el código y no atribuyas al proveedor funciones que no hayas probado.
4. Conserva cambios existentes y el alcance de una sola tarea si el usuario pide un ID concreto. No cambies gates ya aprobados, no avances fases y no marques F7 incompleta.

## Tareas

### PLAY-UX-T01 — Modo inmersivo para todo tipo de reproducción

- Generaliza la experiencia enfocada existente a TV en vivo, películas y episodios. Al iniciar una reproducción por acción del usuario, entra en una vista inmersiva que oculta navegación, filtros y paneles de catálogo y da al vídeo el máximo espacio posible. El modo debe poder desactivarse si el usuario sale de él; no cambies el comportamiento de PiP.
- Conserva el contexto al regresar: lista y filtros de TV; catálogo/filtros y ficha de película; serie, temporada y episodio. El botón contextual `Volver a canales`, `Volver a películas` o `Volver a episodios` debe salir del modo inmersivo sin detener el stream. `Detener` sigue siendo una acción distinta.
- Conserva una sola instancia de `VideoView`, `MediaPlayer` y stream activo. No abras streams para precargar ni al volver al catálogo. Mantén relación de aspecto y evita recortar el contenido.
- Reutiliza `WindowDisplays` y las rutas actuales de pantalla completa/restauración. Respeta las reglas de airspace de `VideoView`/`HwndHost`: no dependas de un overlay WPF sobre el vídeo salvo que confirmes que funciona en el control nativo real. Mantén disponible el retorno mediante controles compatibles con esa limitación y teclado.
- Fullscreen, modo inmersivo y PiP deben tener transiciones definidas y predecibles. `Esc` y el botón de retorno deben comportarse igual en reproducción de TV, película y episodio, sin cerrar la aplicación ni detener el stream accidentalmente.

**Aceptación:** cada tipo de contenido puede entrar y salir del modo inmersivo; el vídeo continúa al volver; se restaura la selección, filtros y temporada/episodio; no se crea un segundo reproductor; la ventana se restaura correctamente al salir de fullscreen o PiP.

### PLAY-UX-T02 — Reproducir episodios al azar

- Añade una acción clara en la ficha de serie para escoger un episodio aleatorio de la serie, incluyendo temporadas disponibles. Reutiliza la lista de episodios ya cargada y filtra elementos que no tengan los datos requeridos para reproducirse; no vuelvas a descargar el catálogo por cada elección.
- Añade una opción explícita `Siguiente episodio al azar` para que, cuando el episodio termine de forma natural, pueda comenzar otro al azar. Déjala desactivada por defecto y permite iniciar la selección manual en cualquier momento.
- El cambio automático solo se aplica al fin natural del medio y si el usuario lo activó. No avances ante error, timeout, pausa, detención manual o cambio de perfil. No abras una conexión anticipada; detén/libera el stream vigente antes de iniciar el siguiente.
- Evita elegir el episodio que acaba de terminar si hay otra opción. Evita repeticiones dentro del ciclo actual cuando resulte viable con la lista cargada. La selección aleatoria debe poder probarse de forma determinista (por ejemplo, fuente de aleatoriedad inyectable).
- Actualiza el contexto visible y el estado de reproducción al cambiar de temporada/episodio. Conserva el progreso y el comportamiento existente de `Continuar viendo`.

**Aceptación:** la selección manual funciona con series de varias temporadas y listas incompletas; la opción de fin natural está apagada inicialmente; activada, avanza una sola vez al azar; un error o `Detener` nunca inicia otro episodio; en todo momento existe como máximo un stream activo.

### PLAY-UX-T03 — Volumen amplificado con control de calidad

- Inspecciona la versión de LibVLC/LibVLCSharp que realmente usa el proyecto y sus opciones soportadas antes de ampliar límites. La [documentación de `MediaPlayer.Volume`](https://docs.videolan.me/libvlcsharp/api/LibVLCSharp.Shared.MediaPlayer.html) describe el volumen como porcentaje y define 100 como volumen nominal/0 dB; no asumas que extender el slider o pasar valores mayores produce amplificación limpia.
- Si el motor y los módulos redistribuidos permiten un aumento de ganancia con limitación/compresión verificable, añade un control separado y reconocible de `Amplificación`, con límites conservadores, nivel normal claramente identificado, valor visible y apagado o neutro por defecto. Prueba que los cambios se aplican y se restauran sin romper mute ni el volumen normal.
- Si no existe una ruta compatible y verificable para elevar el nivel sin recorte inaceptable, conserva el máximo nativo seguro y presenta el alcance real en Ajustes; no simules un porcentaje extra ni afirmes que preserva calidad. No añadas dependencias o plugins nativos sin revisar su inclusión y licencias.
- Si guardas esta preferencia, hazlo por perfil en ajustes locales sin secretos. No registres audio, URLs ni datos de cuenta.

**Aceptación:** los valores se limitan en UI, ViewModel y backend; el nivel normal sigue siendo estable; el estado de mute se conserva; cualquier ganancia adicional usa una ruta nativa verificada y su límite queda documentado. La calidad sonora con distintos parlantes requiere prueba manual y no se declara aprobada por tests simulados.

### PLAY-UX-T04 — Indicador de carga del vídeo

- Representa de forma clara los estados `Loading` y `Buffering`: indicador animado con texto breve al iniciar y al rebufferizar; usa porcentaje solo cuando LibVLC lo proporciona. Diferéncialos de `Paused`, `Playing` y `Failed`; al reproducir, oculta el loader y conserva los mensajes de error/reintento actuales.
- Evita parpadeos en cargas muy cortas, cargas obsoletas después de cambiar rápidamente de canal/episodio y loaders atascados tras cancelar o cerrar.
- Respeta airspace: usa una capa nativa/ventana compatible validada o un área WPF adyacente visible. Comprueba el resultado con el `VideoView` y LibVLC reales del proyecto; una prueba de ViewModel no demuestra que el indicador se vea sobre el HWND.
- Mantén accesibilidad con texto/estado anunciado y no dependas únicamente del color o la animación.

**Aceptación:** se ve carga inicial y rebufferizado, desaparece al primer estado `Playing`, no aparece tras cancelación/fallo/pausa y nunca muestra el estado de un intento anterior.

## Reglas técnicas y de privacidad

- Respeta MVVM, cancelación, disposición determinista, manejo de errores y trabajo asíncrono sin bloquear WPF.
- Mantén estrictamente un único stream activo. Ninguna mejora abre conexiones paralelas ni excede límites del proveedor.
- No guardes credenciales ni URLs autenticadas en SQLite, settings, pruebas, capturas, logs o documentación.
- No dependas de IPTV real para pruebas automatizadas; usa fixtures ficticios/locales. No confundas éxito del fixture con compatibilidad del proveedor.
- Añade pruebas dirigidas para comportamiento, cancelación, cambios de perfil, fin natural y límites. Ejecuta `dotnet build StreamDesk.sln` y `dotnet test StreamDesk.sln` según las instrucciones de `AGENTS.md`; informa resultados reales.
- Actualiza `docs/ESTADO_FASES.md` con evidencia y pendientes de este incremento sin cambiar aprobaciones históricas. Registra decisiones arquitectónicas si modifican el ciclo de vida, fullscreen/PiP o reproducción aleatoria.

## Cierre

Informa tareas completas/parciales, archivos cambiados, comandos y resultados, pasos de validación manual para video/monitores/audio, limitaciones reales de LibVLC, riesgos de airspace y gate de las fases sin alterar aprobaciones existentes. No declares calidad de audio, compatibilidad IPTV ni prueba física a partir de mocks.

## Otras mejoras candidatas (no implementar en este prompt)

- Temporizador de apagado que detenga el stream al cumplirse un tiempo elegido.
- Preferencias por perfil para pista de audio/subtítulos y ajustes de sincronización cuando el formato lo permita.
- Activar `keep-awake` durante reproducción, con restauración al parar/suspender/cerrar.
- Cola de reproducción de series con elección entre orden, siguiente manual y aleatorio.
- Guía EPG visual, caché persistente de logos y búsqueda VOD FTS5 ya descritas en `docs/ARQUITECTURA_FUTURA_V1_1.md`; priorizarlas cuando haya datos y métricas que justifiquen su costo.
