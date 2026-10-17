# StreamDesk — Plan maestro de trabajo

**Versión del plan:** 1.0  
**Fecha:** 17 de octubre de 2026  
**Producto:** Reproductor IPTV de escritorio para Windows, sin anuncios propios  
**Metodología:** Entregas por fases, pruebas y demostraciones de código funcional  
**Estado inicial:** Planificado (ninguna tarea se considera completada)

> Este documento es un plan de implementación, no una afirmación de que los servidores, las credenciales o la reproducción del proveedor se hayan probado. No incorporar credenciales reales en este archivo, Git, issues, capturas o prompts.

## A. Objetivo y criterios de éxito

Crear una aplicación nativa, moderna y rápida para ver TV en vivo y, si el proveedor lo ofrece, películas y series. Debe funcionar sin un backend propio, poder instalarse por separado en dos computadoras Windows y conservar en cada una sus preferencias.

**Objetivos no funcionales (por medir, no garantías):**
- La ventana aparece con rapidez y la interfaz no se bloquea al cargar miles de canales.
- El catálogo navega con virtualización y carga/caché local.
- Un solo stream activo por defecto para respetar límites de conexiones.
- Uso de aceleración de video por hardware cuando resulte compatible; medir CPU/RAM/GPU en dos equipos.
- Los problemas de servidor se comunican con mensajes comprensibles, nunca con errores técnicos sin tratar.
- Uso offline limitado a preferencias y metadatos en caché: no se promete reproducción de TV sin conexión.
- No anuncios propios, seguimiento invasivo ni servicios de pago de la aplicación.
- Los derechos de acceso y reproducción dependen de las autorizaciones y condiciones del proveedor.

**MVP estricto:** login Xtream, catálogo de categorías y TV en vivo, búsqueda, reproducción, controles básicos, pantalla completa, favoritos, recientes y persistencia local. El M3U es segundo adaptador de proveedor: se incorpora al MVP solo si no compromete la entrega del flujo principal.

## B. Stack y decisiones de arquitectura

| Área | Elección inicial | Regla |
| --- | --- | --- |
| SO | Windows 10/11 x64 | Validar compatibilidad en ambas PC; recomendar SO soportado |
| Plataforma | C# + .NET 10 LTS | Target `net10.0-windows` y paquetes fijados por versión |
| UI | WPF + WPF UI | Tema oscuro, alto DPI, accesibilidad y MVVM |
| Motor de video | LibVLCSharp + runtime VLC para Windows | No construir decodificador propio; validar overlay de controles |
| Protocolos | Xtream Codes y M3U/M3U8 | Conectores desacoplados bajo `IIptvProvider` |
| Persistencia | SQLite | Migraciones versionadas y consultas indexadas |
| Secretos | DPAPI (CurrentUser) o Windows Credential Manager | No guardarlos en SQLite ni JSON en claro |
| Logging | `Microsoft.Extensions.Logging` + sink local | Sanitización de contraseñas, URLs y datos privados |
| DI / MVVM | `Microsoft.Extensions.DependencyInjection` y `CommunityToolkit.Mvvm` | Sin lógica de negocio en code-behind |
| Pruebas | xUnit + mocks + pruebas UI (a evaluar) | Tests sin credenciales ni IPTV real por defecto |
| Distribución | Paquete `win-x64` + instalador per-user | Comprobar dependencias nativas y licencias |

### Riesgo técnico obligatorio en fase 0

El control de video WPF de LibVLCSharp puede presentar restricciones **airspace** al superponer overlays WPF. Construir primero un prototipo para verificar barra flotante, pantalla completa, reescalado, minimizar y cambiar de monitor. Si falla, decidir alternativa concreta de alojamiento/ventana de controles o evaluar otra integración de motor multimedia. No diseñar primero una interfaz basada en overlays no probados.

### Estructura orientativa del repositorio

```text
StreamDesk/
  AGENTS.md
  README.md
  docs/
    ROADMAP.md
    ADR/
  src/
    StreamDesk.Desktop/         # WPF: vistas, recursos visuales, MVVM
    StreamDesk.Domain/          # Entidades, contratos y reglas puras
    StreamDesk.Application/     # Casos de uso y orquestación
    StreamDesk.Infrastructure/  # Xtream, M3U, SQLite, seguridad, HTTP
    StreamDesk.Playback/        # LibVLC, ciclo de vida, estados
  tests/
    StreamDesk.UnitTests/
    StreamDesk.IntegrationTests/
    StreamDesk.UiTests/
  packaging/
  scripts/
```

La separación puede simplificarse si el MVP se vuelve innecesariamente complejo. Mantener un solo repositorio y una solución compilable desde el principio.

## C. Ejecución de fases

### Fase 0 — Prueba técnica de conexión, video y rendimiento
**Meta:** validar la viabilidad con código funcional ANTES de diseñar todo.  
**Entregable:** un ejecutable experimental que conecta con un servidor configurado por el usuario, lista unos canales y reproduce video con controles básicos.

- [x] **F0-T01** Crear repositorio, solución .NET y proyecto WPF mínimo compilable.
- [ ] **F0-T02** Crear pantalla de configuración temporal que solicite URL, usuario y contraseña sin valores incrustados en el código.
- [ ] **F0-T03** Probar el contrato Xtream `player_api.php`: autenticación, estado de cuenta, conexiones admitidas y formatos disponibles, sin registrar secretos.
- [ ] **F0-T04** Implementar consulta real de categorías y canales con timeout, cancelación y tratamiento de errores. Usar respuestas mock para tests.
- [ ] **F0-T05** Reproducir al menos una transmisión autorizada; evaluar `.ts` y `.m3u8` cuando el servicio los admita.
- [ ] **F0-T06** Demostrar detener, liberar, reproducir otro canal y cerrar la app sin conservar el stream.
- [ ] **F0-T07** Probar controls encima del video, maximizar, pantalla completa, DPI y cambio de monitor (riesgo airspace).
- [ ] **F0-T08** Medir arranque, RAM, CPU, GPU, latencia de canal y estabilidad en una sesión real; anotar modelo de hardware y códec.
- [ ] **F0-T09** Verificar modalidad de acceso segura (preferir HTTPS), límites de conexión y condiciones de uso. Mostrar aviso si únicamente admite HTTP.
- [ ] **F0-T10** Revisar versiones y licencias de WPF UI, LibVLCSharp y bibliotecas nativas antes de distribuir.
- [ ] **F0-T11** Registrar decisiones técnicas en máximo una página y adjuntar evidencia de ejecución.

**Aceptación:** la aplicación experimental se abre, carga un catálogo, inicia un canal válido, cambia a otro, termina la reproducción sin procesos colgados; queda resuelto si WPF/LibVLC soportará el diseño de controles elegido. Si se bloquea alguna condición, no avanzar ciegamente.

### Fase 1 — Fundamentos de aplicación y seguridad
**Meta:** convertir el prototipo en una base mantenible.  
**Entregable:** aplicación WPF navegable, compilable, con persistencia y estructura de pruebas.

- [ ] **F1-T01** Separar solución en proyectos/capas; definir `IIptvProvider`, `IPlaybackService`, `ISettingsService`, `ICredentialStore`.
- [ ] **F1-T02** Configurar MVVM, DI, comandos async, `CancellationToken` y gestión del ciclo de vida.
- [ ] **F1-T03** Aplicar WPF UI con tokens visuales: colores, tipografía, espaciado, estados y modo oscuro.
- [ ] **F1-T04** Implementar navegación: Inicio, TV en vivo, Favoritos, Recientes y Configuración.
- [ ] **F1-T05** Crear SQLite versionado y tablas iniciales para perfiles, canales, categorías, favoritos e historial.
- [ ] **F1-T06** Implementar almacén de secretos DPAPI/Windows, separado de SQLite; permitir borrar cuenta y credenciales.
- [ ] **F1-T07** Centralizar errores, notificaciones UI, logging redactado y manejo global de fallos recuperables.
- [ ] **F1-T08** Aplicar validaciones a formularios, prevención de doble clic y estados loading/vacío/error.
- [ ] **F1-T09** Configurar test unitarios e integración con fixtures falsos; ejecutar `dotnet build` y `dotnet test`.
- [ ] **F1-T10** Crear guía mínima de ejecución y reglas del agente en `AGENTS.md`.

**Aceptación:** la aplicación inicia sin Internet, navega sin bloquear, permite guardar y eliminar un perfil de prueba, ningún secreto aparece en código/log/base de datos, y la suite de tests se ejecuta.

### Fase 2 — Proveedor Xtream y catálogo
**Meta:** convertir el catálogo en una experiencia usable y resistente a fallos.  
**Entregable:** autenticación y catálogo de televisión completo con búsqueda y caché.

- [ ] **F2-T01** Implementar adaptador Xtream aislado de las vistas.
- [ ] **F2-T02** Interpretar respuesta de autenticación, cuenta expirada, credenciales inválidas y máximo de conexiones.
- [ ] **F2-T03** Descargar/normalizar categorías y canales; preservar identificadores del proveedor.
- [ ] **F2-T04** Añadir `HttpClientFactory`, timeouts, cancelación y reintentos limitados solo para operaciones idempotentes.
- [ ] **F2-T05** Implementar caché local con fecha de sincronización y refresco manual.
- [ ] **F2-T06** Crear búsqueda por nombre con normalización de mayúsculas/acentos.
- [ ] **F2-T07** Usar listas virtualizadas y carga progresiva de logos; impedir que miles de registros congelen UI.
- [ ] **F2-T08** Añadir filtro por categoría y estado de vacíos/errores.
- [ ] **F2-T09** Decidir si ambos servidores ofrecidos por el proveedor representan perfiles separados o alternativas válidas; nunca intercambiarlos automáticamente sin validación.
- [ ] **F2-T10** Probar con catálogos simulados grandes, duplicados y respuestas parciales.

**Aceptación:** autenticación, categorías y miles de canales navegables; la búsqueda responde fluidamente, un fallo de Internet conserva el catálogo conocido y ofrece reintentar.

### Fase 3 — Reproductor de TV en vivo (núcleo del MVP)
**Meta:** lograr reproducción robusta en uso diario.  
**Entregable:** visor funcional con controles y recuperación ante desconexiones.

- [ ] **F3-T01** Integrar LibVLCSharp en un servicio único de reproducción con estados claros (Idle, Loading, Playing, Buffering, Failed, Stopped).
- [ ] **F3-T02** Crear URL de stream en memoria de forma correcta, sin exponer credenciales en UI/logs/telemetría.
- [ ] **F3-T03** Implementar reproducir, detener, volumen, mute y selector anterior/siguiente.
- [ ] **F3-T04** Permitir fullscreen, volver a ventana, conservar relación de aspecto y elegir monitor.
- [ ] **F3-T05** Implementar control de buffer configurable y fallback de aceleración de video cuando falle el hardware.
- [ ] **F3-T06** Permitir cambio rápido de canal con cancelación del canal anterior y sin streams duplicados.
- [ ] **F3-T07** Incorporar reintentos controlados con backoff para interrupciones transitorias, y detenerlos para fallos definitivos.
- [ ] **F3-T08** Gestionar cambio de perfil, suspensión del equipo, cierre de ventana y `Dispose()` de recursos nativos.
- [ ] **F3-T09** Mostrar error comprensible si el stream no existe, expira o el proveedor niega la conexión.
- [ ] **F3-T10** Medir CPU/RAM/GPU, arranque de video y cambios de canal; optimizar con perfiles reales.
- [ ] **F3-T11** Probar transiciones repetidas entre 20 canales, desconexión de red y cierre mientras se reproduce.

**Aceptación:** el video se reproduce de forma estable, cambia de canal, puede detenerse y cerrarse sin fuga evidente de memoria ni reproducciones paralelas; los errores son recuperables.

### Fase 4 — UX/UI, favoritos y versión MVP
**Meta:** que sea agradable y rápido de usar, no solo técnicamente correcto.  
**Entregable:** versión **0.1 MVP** instalable para pruebas diarias.

- [ ] **F4-T01** Completar diseño visual: navegación lateral, jerarquía, modo oscuro y estados de botones.
- [ ] **F4-T02** Diseñar vista TV en vivo con lista de canales y área protagonista de reproducción.
- [ ] **F4-T03** Implementar favoritos con persistencia por perfil y acceso directo.
- [ ] **F4-T04** Implementar recientes, último canal visto y opción de reproducirlo al iniciar (apagada por defecto).
- [ ] **F4-T05** Implementar búsqueda instantánea y accesos a categorías frecuentes.
- [ ] **F4-T06** Añadir atajos de teclado: espacio, Esc, flechas, F, M; evitar conflictos en campos de texto.
- [ ] **F4-T07** Diseñar controles de reproducción con autocultamiento, verificando la solución elegida para airspace.
- [ ] **F4-T08** Implementar estados de skeleton/placeholder, red lenta, sin resultados, timeout, expiración y sin conexión.
- [ ] **F4-T09** Validar escalado 100–200 %, resoluciones y ventanas pequeñas; navegación por teclado y contraste.
- [ ] **F4-T10** Realizar evaluación UX: alta de cuenta, localizar canal, reproducir, añadir favorito y volver al último canal.
- [ ] **F4-T11** Generar build de prueba y recopilar defectos antes de avanzar.

**Aceptación:** desde una instalación limpia, el usuario configura su cuenta una sola vez, entra a TV en vivo, encuentra un canal, lo reproduce, lo guarda en favoritos, cambia a otro y recupera sus preferencias al reiniciar.

### Fase 5 — EPG, películas y series
**Meta:** ampliar contenido sin deteriorar el MVP estable.  
**Entregable:** versión **0.5**, condicionada a la disponibilidad del servicio.

- [ ] **F5-T01** Investigar calidad real de EPG Xtream/XMLTV y mapear zona horaria correctamente.
- [ ] **F5-T02** Añadir EPG de canal actual/siguiente con caché, actualización y estados sin datos.
- [ ] **F5-T03** Integrar categorías y catálogo de películas (VOD), con búsquedas y filtros.
- [ ] **F5-T04** Implementar ficha de película, reproducción y reanudación por posición.
- [ ] **F5-T05** Integrar series, temporadas, episodios y avance por episodio.
- [ ] **F5-T06** Añadir selección de subtítulos/pistas de audio cuando los streams lo permitan.
- [ ] **F5-T07** Implementar historial/continuar viendo sin registrar información innecesaria.
- [ ] **F5-T08** Limitar almacenamiento de carátulas/caché y limpiar contenido obsoleto.
- [ ] **F5-T09** Probar metadatos inconsistentes, sinopsis vacías, streams inaccesibles y series grandes.

**Aceptación:** los módulos solo se muestran si el servicio provee datos utilizables, el catálogo no ralentiza la TV en vivo y la reproducción VOD no rompe el motor principal.

### Fase 6 — Estabilización, instalador y versión 1.0
**Meta:** distribuir una aplicación lista para utilizar en ambas computadoras.  
**Entregable:** instalador y versión **1.0** probados.

- [ ] **F6-T01** Analizar resultados de CPU/RAM/GPU y corregir ralentizaciones, bloqueos, escapes de memoria.
- [ ] **F6-T02** Probar arranque, cierre, reconexión, suspensión/reanudación, Wi-Fi inestable y desconexión de proveedor.
- [ ] **F6-T03** Ejecutar suite unitaria e integración; probar flujos UI críticos y documentar evidencia.
- [ ] **F6-T04** Ejecutar pruebas de seguridad: secretos en disco, redirecciones, logs, errores y URL de transmisión.
- [ ] **F6-T05** Comprobar versiones/licencias de dependencias nativas y obligaciones de redistribución.
- [ ] **F6-T06** Publicar `win-x64`; revisar inclusión de DLL/plugins/codec de LibVLC. No asumir que `single-file` empaqueta correctamente bibliotecas nativas.
- [ ] **F6-T07** Crear instalador preferiblemente per-user, con instalación, actualización y desinstalación limpia.
- [ ] **F6-T08** Probar en PC de trabajo y casa (con autorización de TI para equipo corporativo).
- [ ] **F6-T09** Verificar que no se requieren SDKs/herramientas de desarrollo en PCs destino.
- [ ] **F6-T10** Definir estrategia para actualización de app, copias de configuración, reporte de errores y rollback.
- [ ] **F6-T11** Entregar manual de instalación/uso breve y registro de problemas conocidos.
- [ ] **F6-T12** Marcar 1.0 únicamente al cumplir checklist de release.

**Aceptación:** el instalador funciona en ambas PCs limpias o equivalentes, reproduce contenido permitido, persiste ajustes, no expone secretos en disco o logs y se desinstala sin romper el sistema.

### Fase 7 — Mejoras opcionales posteriores
**No bloquean 1.0.**

- [ ] **F7-T01** Ventana flotante/Picture-in-Picture siempre visible.
- [ ] **F7-T02** Adaptador M3U/M3U8 de importación local y remota si no entró en MVP.
- [ ] **F7-T03** Perfiles de proveedor múltiples y administración de conexiones.
- [ ] **F7-T04** Sincronización cifrada de favoritos e historial entre casa y trabajo (sin sincronizar secretos innecesariamente).
- [ ] **F7-T05** Temas, tamaños, idioma y atajos personalizables.
- [ ] **F7-T06** Control parental, grabación o catch-up solo cuando sean viables y autorizados.

## D. Modelo mínimo de datos

- `ProviderProfiles`: identificador, alias, base URL, tipo de proveedor, estado y última validación. **Nunca contraseña en claro.**
- `Categories`: identificador local, `provider_id`, categoría remota, tipo de contenido, nombre y orden.
- `Channels`: identificador local, `provider_id`, `stream_id`, nombre, categoría, logo, EPG ID, orden.
- `Favorites`: `provider_id`, `content_type`, identificador remoto y fecha.
- `WatchHistory`: `provider_id`, tipo, identificador remoto, posición (si corresponde) y fecha.
- `EpgPrograms`: `provider_id`, canal/EPG ID, inicio UTC, fin UTC, título y fecha de actualización.
- `AppSettings`: opciones de UI, caché y reproducción (sin secretos).
- `SchemaVersions`: migraciones aplicadas.

Los IDs remotos pueden solaparse entre proveedores: toda clave de contenido debe incorporar `provider_id` y tipo. Usar índices por perfil, categoría y nombre normalizado. Normalizar horarios EPG a UTC y renderizar en hora local.

## E. Definición de terminado (aplicable a cada tarea)

Una tarea solo se marca `[x]` si:

1. Hay código o cambio verificable en el repositorio, no únicamente texto o un diseño estático.
2. Compila sin errores; los tests afectados corren con su resultado documentado.
3. Los comportamientos visibles fueron probados manualmente o con pruebas UI cuando corresponda.
4. Los casos de error, cancelación y carga están manejados.
5. No se registran ni exponen credenciales ni URLs de stream con secretos.
6. No introduce dependencias innecesarias ni bloquea el hilo de interfaz.
7. Existe un commit acotado con la tarea relacionada.
8. Codex informa: archivos modificados, comandos ejecutados, resultado, capturas/evidencia si aplica, riesgos pendientes y próximo paso.

**Puerta de fase:** no avanzar si el entregable/criterio de aceptación falla. No considerar tareas de pruebas completas cuando las dependencias faltan o se omitieron pruebas.

## F. Pruebas obligatorias

| Caso | Resultado esperado |
| --- | --- |
| URL inválida / DNS no disponible | Error claro sin bloquear UI |
| Credenciales inválidas / cuenta expirada | Diagnóstico sin revelar contraseña |
| Respuesta lenta / timeout | Cancelar o reintentar controladamente |
| Sin Internet al iniciar | Catálogo local visible, video no disponible |
| Lista con miles de canales | Scroll y búsqueda fluidos |
| Canal inaccesible | Mensaje y posibilidad de elegir otro |
| Cambiar 20 veces de canal | Solo un reproductor activo, sin crecimiento excesivo de RAM |
| Cerrar durante buffer | Reproductor liberado, proceso termina |
| Minimizar/restaurar/maximizar | Vista consistente, controles utilizables |
| Dos monitores / escalado DPI | Video y controles correctamente alineados |
| Suspensión y reanudación de Windows | Estado controlado y recuperación |
| Reiniciar aplicación | Perfil, favoritos y recientes conservados |
| Secretos/logs | Ningún password ni URL con credenciales expuestos |
| Instalación en otra PC | Arranca sin entorno de desarrollo |
| Límite de 1 conexión | No iniciar streams simultáneos en segundo plano |

### Métricas iniciales orientativas

Medir, no prometer: arranque en frío, uso de RAM en reposo y reproduciendo 1080p, CPU/GPU, primer frame, cambio de canal, scrolling con 5.000+ elementos, fallos por hora y consumo de caché. Evaluar en hardware real y ajustar umbrales tras F0; no imponer una cifra universal de RAM porque el códec, VLC y GPU varían mucho.

## G. Reglas de trabajo para Codex (copiar a `AGENTS.md`)

```markdown
# Reglas de desarrollo — StreamDesk

- Trabaja por códigos de tarea (`F0-T01`, etc.) y no avances a la siguiente fase sin aceptación.
- Prioriza código ejecutable temprano. No sustituyas implementación por documentación, mockups o TODOs.
- Antes de empezar una tarea: inspecciona el repositorio, confirma el objetivo y limita el alcance.
- No incrustes credenciales, URLs personales con secretos, datos de cuenta ni tokens en código, tests, commits o logs.
- No subas secretos a servicios externos. Usa configuración local y DPAPI/Windows Credential Manager.
- Escribe código C# legible, con MVVM y servicios desacoplados; maneja excepciones, cancelación y limpieza de recursos.
- No bloquees el hilo UI con red, base de datos o lectura masiva.
- Cuando agregues funcionalidad, agrega pruebas relevantes de éxito y error. Usa mocks del proveedor.
- Para cambios de reproducción, realiza pruebas manuales reales de inicio/cambio/cierre si el entorno lo permite.
- Nunca declares una prueba exitosa si no la ejecutaste: indica restricciones con precisión.
- Ejecuta `dotnet build` y `dotnet test` cuando correspondan. Corrige regresiones antes de cerrar la tarea.
- No hagas rediseños visuales grandes ni introduzcas nuevas dependencias fuera de la tarea sin explicar el motivo.
- Finaliza cada entrega con: tareas completadas, archivos, comandos/tests, resultados, pendientes y siguiente tarea.
```

## H. Prompts listos para utilizar con Codex

### Prompt inicial: Fase 0

```text
Vamos a crear StreamDesk, reproductor IPTV nativo para Windows con .NET 10, WPF y LibVLCSharp.
Lee el documento StreamDesk_Plan_de_Trabajo.md y ejecuta EXCLUSIVAMENTE FASE 0 (F0-T01 a F0-T11), en orden.

Reglas críticas:
- Fase 0 NO puede terminar en solo documentación: crea un proyecto compilable y una app experimental ejecutable.
- La app debe solicitar las credenciales localmente. No incrustes datos reales en código ni fixtures.
- Implementa un cliente Xtream Codes mínimo con timeouts/cancelación, lista canales y reproduce un stream autorizado.
- Antes de diseñar overlays, valida el problema airspace de LibVLCSharp.WPF y comprueba fullscreen.
- Prueba abrir, cambiar, detener y cerrar video. Mide CPU/RAM y documenta versión/librearía/resultado.
- Usa datos falsos para pruebas automáticas; las pruebas manuales reales solo con la cuenta que el usuario configura.
- Si falla la tecnología elegida, detente y entrega diagnóstico y alternativa razonada, sin fingir éxito.
- Ejecuta build y tests que existan; indica exactamente cuáles pudiste correr.

No inicies Fase 1. Devuelve checklist con [x] solo para lo ejecutado, evidencia verificable y bloqueos.
```

### Prompt reutilizable para Fases 1 a 6

```text
Continúa StreamDesk con la FASE [NÚMERO] del archivo StreamDesk_Plan_de_Trabajo.md.
1. Verifica primero los criterios de aceptación de la fase anterior y cualquier pendiente.
2. Implementa cada tarea con su código F[N]-T[XX], con cambios pequeños y pruebas.
3. Mantén el proyecto compilable, sin secretos y sin regresiones en reproducción/UI.
4. No cambies tecnologías o arquitectura sin una justificación técnica comprobable.
5. Ejecuta dotnet build, dotnet test y las pruebas funcionales disponibles.
6. Entrega: tareas realizadas, archivos, evidencias, resultados, tareas no realizadas y bloqueos.
7. No avances a la fase siguiente sin mi revisión. No marques tareas como completas sin ejecución real.
```

### Prompt de revisión al finalizar cada fase

```text
Audita la fase recién trabajada de StreamDesk contra el plan.
Para cada F[N]-T[XX], clasifica: completada con evidencia / parcial / pendiente / bloqueada.
Valida compilación, tests, excepciones, privacidad de credenciales y usabilidad.
Demuestra el flujo funcional principal; enumera los pasos que no puedas probar.
No desarrolles la fase siguiente: corrige primero defectos de severidad alta y reporta pendientes.
```

## I. Estrategia Git y versiones

- Rama principal `main` siempre compilable.
- Ramas pequeñas `feature/F2-T05-cache-catalogo`, `fix/...`.
- Un commit o conjunto de commits acotados por tarea; PR o revisión por fase.
- Versionado sugerido: prototipo experimental F0, base F1, núcleo F2–F3, **v0.1 MVP F4**, **v0.5 F5**, **v1.0 F6**.
- Etiquetar releases únicamente cuando pasen los criterios de aceptación.

## J. Primeros pasos concretos

1. Crear carpeta/repo `StreamDesk`.
2. Guardar este archivo en `docs/ROADMAP.md` y las reglas anteriores en `AGENTS.md`.
3. Instalar SDK .NET 10, herramientas de compilación para WPF y Git.
4. Abrir repositorio con Codex y enviar **Prompt inicial: Fase 0**.
5. Introducir las credenciales desde la interfaz local del prototipo, no en el chat/prompts/comandos guardados.
6. Evaluar la demostración técnica antes de construir todo el sistema.

**Seguridad:** los endpoints IPTV indicados actualmente son HTTP; si el proveedor no ofrece HTTPS, las credenciales pueden viajar expuestas en URL/solicitud. Consultar posibilidad de HTTPS. Si la contraseña se reutilizó o se compartió fuera de un medio seguro, considerar su rotación. No publicar el repositorio con credenciales de una cuenta real.

## K. Fuentes técnicas verificadas (octubre de 2026)

- .NET LTS y soporte: https://learn.microsoft.com/en-us/dotnet/core/releases-and-support
- WPF UI: https://github.com/lepoco/wpfui
- LibVLCSharp getting started: https://github.com/videolan/libvlcsharp/blob/3.x/docs/getting_started.md
- Xtream Player API de referencia comunitaria: https://github.com/worldofiptvcom/xtream-codes-api/blob/master/docs/player-api/overview.md
- Publicación de .NET: https://learn.microsoft.com/en-us/dotnet/core/deploying/
- DPAPI en .NET: https://learn.microsoft.com/en-us/dotnet/standard/security/how-to-use-data-protection

La API Xtream suele requerir usuario y contraseña en parámetros de consulta y URL de reproducción; por eso es esencial no registrar esas URL y evitar HTTP sin cifrado cuando sea posible.
