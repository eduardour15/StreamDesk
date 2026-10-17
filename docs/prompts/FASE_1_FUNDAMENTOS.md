# PROMPT EJECUTABLE — FASE 1: Fundamentos, arquitectura y seguridad

> **En Codex basta escribir:** `Ejecuta la fase 1`. El archivo `AGENTS.md` contiene el enrutador que carga este prompt.
> Este documento es una instruccion para el agente dentro del repositorio StreamDesk; no lo ejecutes como un script.

## Rol y objetivo

Eres el agente de desarrollo senior de StreamDesk, reproductor IPTV nativo para Windows (C#, .NET 10, WPF/WPF UI, LibVLCSharp, SQLite, MVVM). Lee `AGENTS.md`, `docs/ROADMAP.md`, `docs/ESTADO_FASES.md` y las decisiones de `docs/ADR/` antes de cambiar archivos. **Ejecuta EXCLUSIVAMENTE la Fase 1.** No te limites a generar especificaciones.

**Objetivo concreto:** Convertir la prueba tecnica en una aplicacion mantenible, navegable y segura sin introducir complejidad injustificada.

**Precondicion:** F0 aprobada; prototipo WPF/LibVLC validado y decision del problema airspace documentada.

**Entrega esperada:** Base funcional sin catalogo definitivo.

## Alcance obligatorio: IDs estables

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

## Instrucciones de implementacion especificas

1. Inspecciona el codigo experimental, preserva el flujo validado y refactoriza progresivamente; no reescribas todo sin motivo.
2. Crea capas razonables Desktop, Domain, Application, Infrastructure y Playback; comparte interfaces `IIptvProvider`, `IPlaybackService`, `ISettingsService`, `ICredentialStore` donde proceda. No introduzcas patrones vacios por estetica.
3. Integra MVVM, DI, comandos async, cancelacion y liberacion de recursos. Mantiene UI responsiva.
4. Instala WPF UI y define tokens visuales, tema oscuro predeterminado, estados de foco, tipografia y escala. Crea navegacion real Inicio, TV, Favoritos, Recientes y Configuracion.
5. Integra SQLite versionado con migracion inicial y datos ficticios; las claves incluyen proveedor/tipo para evitar colisiones. No almacenes secretos ni URL autenticada en la BD.
6. Implementa Windows DPAPI (CurrentUser) o Credential Manager con borrado seguro de perfil. Preve uso por diferentes usuarios de Windows.
7. Establece errores tipados, notificaciones de UI, logs redactados, timeouts, estados loading/vacio/error, limites de formularios y defensa ante doble pulsacion.
8. Agrega unit tests e integration tests reproducibles sin depender de IPTV real; documenta un arranque offline seguro.

## Pruebas y verificaciones de fase

- Compilacion `dotnet build` y pruebas `dotnet test` desde raiz; tests de persistencia/migracion, almacenamiento/cancelacion y navegacion/estados.
- Prueba de creacion y eliminacion de perfil de prueba; ninguna contrasena legible en SQLite/configuracion/logs.
- Arranque offline y navegacion manual en Windows; inspeccion del proceso al cerrar.
- Si la migracion de prototipo introduce regresion de playback, no cerrar fase.

## Gate de aceptacion

Aplicacion real navegable que inicia sin conexion, crea/borra perfil local y protege secretos. Compila y ejecuta tests; conserva capacidad de reproduccion del prototipo en el entorno de prueba.

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
