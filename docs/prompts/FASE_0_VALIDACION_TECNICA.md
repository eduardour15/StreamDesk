# PROMPT EJECUTABLE — FASE 0: Validacion tecnica

> **En Codex basta escribir:** `Ejecuta la fase 0`. El archivo `AGENTS.md` contiene el enrutador que carga este prompt.
> Este documento es una instruccion para el agente dentro del repositorio StreamDesk; no lo ejecutes como un script.

## Rol y objetivo

Eres el agente de desarrollo senior de StreamDesk, reproductor IPTV nativo para Windows (C#, .NET 10, WPF/WPF UI, LibVLCSharp, SQLite, MVVM). Lee `AGENTS.md`, `docs/ROADMAP.md`, `docs/ESTADO_FASES.md` y las decisiones de `docs/ADR/` antes de cambiar archivos. **Ejecuta EXCLUSIVAMENTE la Fase 0.** No te limites a generar especificaciones.

**Objetivo concreto:** Demostrar que el stack Windows/WPF/LibVLC puede autenticar, enumerar y reproducir al menos un canal autorizado sin exponer secretos.

**Precondicion:** Ninguna; comprobar SDK .NET, entorno de compilacion y acceso a un equipo Windows para las pruebas visuales.

**Entrega esperada:** Prototipo experimental (sin promesa de version estable).

## Alcance obligatorio: IDs estables

- [ ] **F0-T01** Crear repositorio, solución .NET y proyecto WPF mínimo compilable.
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

## Instrucciones de implementacion especificas

1. Inspecciona el repositorio y herramientas disponibles (`dotnet --info`, SDK, Windows, Git). Si hay archivos existentes, preservalos. Crea la solucion y proyecto WPF minimo ahora; no entregues solo documentos.
2. Implementa formulario local de configuracion con URL, usuario y contrasena enmascarada; no guardes ninguna credencial en claro ni incluyas secretos en fixtures.
3. Construye un adaptador inicial Xtream basado en `player_api.php` y clasifica respuestas: usuario invalido, cuenta caducada, servidor inalcanzable, formato no admitido; no supongas estructuras de datos que no hayas comprobado.
4. Implementa consulta de categorias/canales y lista minima; usa `HttpClient`, timeout, `CancellationToken`, manejo de errores y mocks para tests.
5. Integra LibVLCSharp y librerias nativas; inicia stream solo por accion del usuario. Verifica abrir, cambiar, detener y cerrar sin mantener multiples reproducciones. Prueba HLS/MPEG-TS solo si los datos realmente los ofrecen.
6. Investiga empiricamente la restriccion *airspace* de LibVLCSharp.WPF: controles flotantes, fullscreen, minimizar/restaurar, redimensionar, DPI y cambio de monitor. Si no es viable, registra ADR y prototipo alternativo antes de avanzar.
7. Realiza pruebas reales voluntarias en PC Windows: RAM/CPU/GPU, inicio a primer frame, latencia de cambio, estabilidad; documenta hardware y metodologia. No simules benchmarks ni afirmes haber probado video real sin evidencia.
8. Valida HTTPS si el servicio lo admite; si solo HTTP, informa riesgo de credenciales en transito sin registrar URLs autenticadas. Verifica limite de conexiones del proveedor y no intentes sortearlo.
9. Revisa licencias de los componentes y documenta obligaciones de redistribucion. Resume decision tecnica y pasos de ejecucion en una pagina o menos.

## Pruebas y verificaciones de fase

- `dotnet restore`, `dotnet build` y `dotnet test` si hay proyectos de pruebas; conserva salidas/resumen.
- Tests de normalizacion de respuestas con fixtures ficticios, URL incorrecta, cancelacion y timeout.
- Prueba manual guiada en Windows para conectar, reproducir, cambiar, detener, cerrar y validar controles sobre video. Si Codex carece de pantalla/Windows/servicio, marca esa evidencia PENDIENTE y la fase NO APROBADA, en lugar de inventarla.
- Revisa proceso finalizado, sin streams persistentes, URLs y passwords ausentes de logs.

## Gate de aceptacion

Existe un prototipo ejecutable y compilable en Windows; carga canales, reproduce al menos uno autorizado, cambia y detiene correctamente; se determino de manera comprobable si el diseno de overlays es viable. Sin prueba real de reproduccion/controles, la fase queda BLOQUEADA o PENDIENTE DE VALIDACION y no se autoriza F1.

## Reglas innegociables de esta ejecucion

- **Implementa, no solo documentes.** No presentes mocks como reproduccion real ni pantallas que no se pueden usar.
- **No avances automaticamente a la siguiente fase.** Limita el trabajo a los codigos de este prompt; si el usuario invoca un codigo puntual, trabaja SOLO ese codigo y sus dependencias estrictas.
- **Conserva codigo existente y cambios ajenos.** No hagas `git reset --hard`, sobrescritura masiva ni descartes trabajo del usuario. No introduzcas refactors ajenos a la fase.
- **Seguridad:** credenciales reales solo en UI/configuracion segura local. No las guardes en `.md`, `.json`, `.env` rastreado, test, telemetria, codigo, commits ni logs. Evita imprimir URLs Xtream autenticadas. Preferir HTTPS y advertir si el proveedor solo admite HTTP. No eludir restricciones del proveedor ni descargar contenido no autorizado.
- **Manejo de recursos:** UI nunca bloqueada por red/BD, `CancellationToken`, timeouts, errores tipados y limpieza de LibVLC. No abrir mas de un stream por defecto.
- **Integridad:** mocks usados para pruebas NO validan interaccion multimedia real; separa claramente resultados simulados de manuales.
- **Pruebas:** ejecuta las disponibles desde la raiz. Si faltan sistema operativo, acceso visual, SDK, credenciales o red, explica la limitacion y entrega protocolo de prueba manual; NO marques prueba como pasada.
- **Versiones/arquitectura:** usa las dependencias ya aprobadas y respeta ADRs. Justifica modificaciones incompatibles en un ADR antes de realizarlas.
- **Control Git:** pequenos cambios coherentes, evita incluir secretos en staging; registra commits solo cuando la configuracion y autorizacion del entorno lo permitan. No hagas push ni publiques releases sin instruccion expresa.

## Formato de respuesta obligatorio de Codex

1. **Fase y alcance:** ID, tareas objetivo y estado previo.
2. **Implementacion:** que funciona de verdad y principales archivos modificados.
3. **Evidencia:** comandos reales, salida resumida, pruebas automaticas, comprobaciones manuales y limites del entorno.
4. **Checklist:** tabla/lista de todos los IDs, con `COMPLETADA`, `PARCIAL`, `BLOQUEADA` o `PENDIENTE`. NUNCA marcar completada sin evidencia.
5. **Criterios de aceptacion:** cada uno `PASA`, `NO PASA` o `NO VERIFICADO`.
6. **Riesgos, tareas pendientes y como verificarlas.**
7. **Decision de gate:** `APROBADA`, `PENDIENTE DE VALIDACION` o `BLOQUEADA`, con justificacion. Una implementacion sin verificacion obligatoria no puede aprobarse sola.
8. **Siguiente paso recomendado**, sin ejecutar la fase siguiente.

Actualiza `docs/ESTADO_FASES.md` al cerrar; preserva el registro y no declares `APROBADA` por mera implementacion parcial. Detente tras el informe de esta fase.
