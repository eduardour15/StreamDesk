# AGENTS.md — Instrucciones permanentes de Codex para StreamDesk

## Producto y limites

StreamDesk: IPTV de escritorio para Windows, C#/.NET 10, WPF con WPF UI, LibVLCSharp, SQLite, MVVM. El plan fuente es `docs/ROADMAP.md`. La ejecucion se controla con `docs/ESTADO_FASES.md`. No afirmar compatibilidad con proveedores ni exito de video sin pruebas reales.

## ENRUTADOR OBLIGATORIO DE PROMPTS

Cuando el usuario indique una fase, tarea o auditoria de forma corta (aunque solo diga «Fase 3», «Ejecuta fase 3», «Continua F2»), **lee los archivos del repositorio antes de implementar** y aplica estas reglas:

1. Mapea fase N (0 a 7) al documento exacto:
   - Fase 0 -> `docs/prompts/FASE_0_VALIDACION_TECNICA.md`
   - Fase 1 -> `docs/prompts/FASE_1_FUNDAMENTOS.md`
   - Fase 2 -> `docs/prompts/FASE_2_CATALOGO_XTREAM.md`
   - Fase 3 -> `docs/prompts/FASE_3_REPRODUCTOR.md`
   - Fase 4 -> `docs/prompts/FASE_4_UX_MVP.md`
   - Fase 5 -> `docs/prompts/FASE_5_CONTENIDO_EPG_VOD.md`
   - Fase 6 -> `docs/prompts/FASE_6_VERSION_1_0.md`
   - Fase 7 -> `docs/prompts/FASE_7_MEJORAS_OPCIONALES.md`
2. Siempre lee `docs/ROADMAP.md`, el prompt de la fase, `docs/ESTADO_FASES.md` y ADRs existentes pertinentes.
3. Si el usuario dice «Ejecuta F3-T06», carga el prompt de fase 3 y realiza **solo** F3-T06 y sus dependencias imprescindibles. No marques terminada toda la fase.
4. Si dice «Continua fase N», inspecciona cambios/git/tests y estado, revalida la primera tarea pendiente y prosigue **sin repetir trabajo ya correcto**.
5. Si dice «Audita fase N», sigue `docs/prompts/AUDITORIA_DE_FASE.md`, no implementes nuevas features, y clasifica evidencias. Puedes reparar defectos si el usuario solicita auditoria con correccion.
6. Si dice «Resuelve los bloqueos de fase N», sigue `docs/prompts/RESOLVER_BLOQUEOS.md`; limita el alcance a defectos reales.
7. Si dice «Muestra el estado», resume `docs/ESTADO_FASES.md` y verifica que refleje el repositorio.
8. Si no existe el prompt de la fase, **NO improvises**; reporta exactamente cual falta.
9. No avances a una fase distinta por iniciativa propia, aunque termines antes. Fases 1–6 requieren gate previo aprobado; si el usuario fuerza un salto, advierte el riesgo, analiza dependencias y no falsifiques aprobaciones.

## Reglas tecnicas y de seguridad

- Prioriza codigo compilable y funcional **desde la fase 0**; nunca presentes solo documentos como cierre de una fase de implementacion.
- Usa MVVM; no bloquees hilo UI; encapsula API Xtream, BD y VLC detras de contratos. Introduce `CancellationToken`, excepciones manejadas, disposicion determinista y estados de error.
- Los medios deben abrirse mediante un unico stream activo por defecto. No sortear restricciones ni limites del proveedor.
- Nunca codifiques credenciales reales, urls con usuario/contrasena, tokens ni secretos en codigo, repositorio, tests, documentacion, mensajes, logs ni commits. Emplea DPAPI CurrentUser o Credential Manager. Mantiene archivos locales sensitivos fuera de Git.
- Si el IPTV usa HTTP, presenta advertencia clara: usuario y contrasena podrian viajar sin cifrar; consulta si hay HTTPS soportado, no cambies los servidores a ciegas.
- Comprueba licencias/avisos de redistribucion antes de publicar, especialmente VLC/libVLC. No presupongas que las pruebas simuladas verifican video real.
- Respeta modificaciones actuales; no ejecutes `git reset --hard`, no hagas push ni publiques releases sin peticion expresa. No expongas secretos en diffs o comandos.
- Al iniciar la fase inspecciona el estado del repo. Al finalizar ejecuta `dotnet build` y `dotnet test` cuando este disponible; informa bloqueos ambientales, no inventes resultados.
- Para pruebas reales que Codex no pueda ejecutar, entrega pasos reproducibles para Windows y marca prueba PENDIENTE DE VALIDACION. Nunca conviertas ausencia de evidencia en una aprobacion.
- Por tarea, comprueba el codigo funcional, caminos de error y regresiones. Mantiene checklist con IDs del ROADMAP sin renumerar.

## Registro y gate

- Actualiza `docs/ESTADO_FASES.md` con fecha, fase, estado, evidencia real, pendientes y decision de gate.
- Estados validos de tarea: `PENDIENTE`, `EN_CURSO`, `PARCIAL`, `BLOQUEADA`, `COMPLETADA`.
- Estados validos de gate: `NO_INICIADA`, `EN_PROGRESO`, `PENDIENTE_DE_VALIDACION`, `BLOQUEADA`, `APROBADA`.
- No marques una fase `APROBADA` cuando un criterio obligatorio no se verifico; no marques tests exitosos si no los ejecutaste.
- El cierre incluye: tareas completadas vs parciales, archivos, comandos y resultados, pruebas manuales, riesgos, gate y proxima fase recomendada **sin iniciarla**.

## Convenciones de arquitectura (F1)

- `StreamDesk.Probe.Core` contiene modelos de dominio; `StreamDesk.Application` contiene contratos y coordinación; `StreamDesk.Probe.Infrastructure` implementa Xtream, SQLite, DPAPI y logging; `StreamDesk.Probe.Playback` encapsula LibVLC; `StreamDesk.Probe.Desktop` compone dependencias y presenta WPF/MVVM.
- La base local y secretos viven en `%LocalAppData%\StreamDesk`. SQLite nunca guarda usuario/contraseña ni URLs autenticadas. Las credenciales se protegen con DPAPI CurrentUser y se borran al eliminar el perfil.
- Los logs registran solo identificadores técnicos y tipo de excepción; nunca guardar mensaje, stack trace, URL de solicitud ni datos de cuenta.
- Pruebas de persistencia deben usar directorios temporales y fixtures ficticios. Ejecutar `dotnet build StreamDesk.sln` y `dotnet test StreamDesk.sln` desde la raíz.
