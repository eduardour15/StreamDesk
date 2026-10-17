# PROMPT EJECUTABLE — FASE 3: Motor de reproduccion en vivo

> **En Codex basta escribir:** `Ejecuta la fase 3`. El archivo `AGENTS.md` contiene el enrutador que carga este prompt.
> Este documento es una instruccion para el agente dentro del repositorio StreamDesk; no lo ejecutes como un script.

## Rol y objetivo

Eres el agente de desarrollo senior de StreamDesk, reproductor IPTV nativo para Windows (C#, .NET 10, WPF/WPF UI, LibVLCSharp, SQLite, MVVM). Lee `AGENTS.md`, `docs/ROADMAP.md`, `docs/ESTADO_FASES.md` y las decisiones de `docs/ADR/` antes de cambiar archivos. **Ejecuta EXCLUSIVAMENTE la Fase 3.** No te limites a generar especificaciones.

**Objetivo concreto:** Construir un reproductor robusto para TV en vivo, con un solo stream activo, controles completos y recuperacion razonable.

**Precondicion:** F2 aprobada y flujo real prototipo F0 funcionando.

**Entrega esperada:** Motor de TV en vivo estable.

## Alcance obligatorio: IDs estables

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

## Instrucciones de implementacion especificas

1. Implementa `IPlaybackService` / adaptador LibVLC con maquina de estados Idle, Loading, Playing, Buffering, Failed y Stopped; estados observables para UI.
2. Construye URLs de stream unicamente en memoria usando el proveedor/perfil valido; nunca las incluyas en excepciones, telemetria, logs, capturas o persistencia.
3. Implementa play, stop, mute, volumen y canal siguiente/anterior dentro del orden visible y categoria activa.
4. Fullscreen/ventana con relacion de aspecto, manejo de monitor y controles usables en la solucion airspace aprobada en F0.
5. Gestiona buffering y hardware acceleration con fallback medido; no fuerces valores que ocasionen incompatibilidad.
6. Implementa cancelacion de solicitud previa y liberacion determinista del reproductor al cambiar de canal, cerrar app, salir del perfil o suspender Windows.
7. Reconecta solo errores transitorios con backoff, numero maximo de intentos y boton Reintentar; evita ciclos de reconexion infinita por credenciales invalidas o limites de cuenta.
8. Evita streams simultaneos salvo funcionalidad explicita futura que respete licencias del proveedor.
9. Agrega mensajes claros para URL no valida, canal caido, cancelacion, cuenta expirada y timeout.
10. Desacopla unit tests de medios nativos con fakes; prueba transitions de estado y ciclo de vida.

## Pruebas y verificaciones de fase

- Ejecuta build/tests y una prueba de estres de cambiar entre 20 canales (pueden simularse estados, pero prueba real tambien es necesaria para aceptacion final).
- Prueba cierre durante buffering, perdida de red, reconexion, error 401/403, suspension/reanudacion y cambio de perfil.
- Mide memoria, CPU/GPU y primer frame con hardware conocido; identifica tendencias de fuga, no prometas ausencia universal de fugas.
- Prueba fullscreen, multi-monitor y controles en video en Windows. Distingue automatizadas de manuales.

## Gate de aceptacion

Un solo stream activo; cambio de canales, parar, cerrar, fullscreen y recuperacion funcionan; tras cierre se liberan recursos; no aparecen credenciales en logs. Pruebas manuales de medios reales realizadas o pendientes explicitamente como bloqueo.

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
