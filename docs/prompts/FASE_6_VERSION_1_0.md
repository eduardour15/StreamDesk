# PROMPT EJECUTABLE — FASE 6: Calidad seguridad e instalador

> **En Codex basta escribir:** `Ejecuta la fase 6`. El archivo `AGENTS.md` contiene el enrutador que carga este prompt.
> Este documento es una instruccion para el agente dentro del repositorio StreamDesk; no lo ejecutes como un script.

## Rol y objetivo

Eres el agente de desarrollo senior de StreamDesk, reproductor IPTV nativo para Windows (C#, .NET 10, WPF/WPF UI, LibVLCSharp, SQLite, MVVM). Lee `AGENTS.md`, `docs/ROADMAP.md`, `docs/ESTADO_FASES.md` y las decisiones de `docs/ADR/` antes de cambiar archivos. **Ejecuta EXCLUSIVAMENTE la Fase 6.** No te limites a generar especificaciones.

**Objetivo concreto:** Entregar una version 1.0 instalable, medible y confiable en dos equipos Windows.

**Precondicion:** F5 aprobada o documentada como no aplicable por capacidades del proveedor, y MVP F4/F3 estables.

**Entrega esperada:** 1.0 unicamente cuando pase release gate.

## Alcance obligatorio: IDs estables

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

## Instrucciones de implementacion especificas

1. Revisa pendientes y prioriza errores de bloqueo, reproduccion, caidas y filtracion de secretos. Congela nuevas features.
2. Ejecuta evaluaciones CPU/RAM/GPU/startup/cambio canal con escenarios comparables; elimina leaks/event handlers no liberados y operaciones que bloquean UI.
3. Valida suspension/reanudacion, Wi-Fi inestable, cambio de red, reinicios, cierre durante buffering, renovacion/expiracion de cuenta y 20 cambios de canal.
4. Ejecuta suite unitaria/integracion y pruebas UI reales; entrega reporte con comandos, ambiente y salidas, sin afirmaciones inventadas.
5. Auditoria de privacidad: secretos DPAPI, paths sensibles, logs, excepciones, dumps, XML/JSON, URLs con credenciales, portapapeles y archivos temporales. Separa almacenamiento local del repo.
6. Audita licencias y redistribucion de LibVLC y componentes nativos, incluyendo cumplimiento de avisos/documentacion aplicable.
7. Construye release reproducible `win-x64`; incluye dependencias nativas necesarias y prueba en equipo limpio SIN Visual Studio ni SDK. Evita suponer que single-file contiene plugins de VLC.
8. Empaqueta instalador preferiblemente por usuario, con version visible, opcion de instalacion/desinstalacion, ruta de datos y preservacion/borrado claro de preferencias.
9. Prueba en PC de casa y trabajo solo con autorizacion corporativa, configuracion independiente y un stream por defecto. Si Codex no puede acceder fisicamente, proporciona protocolo guiado y marca PENDIENTE.
10. Define actualizaciones (manual al menos), backup/rollback configuracion y diagnostico local redactado.
11. Crea manual de instalacion y notas release concisas, de fallos conocidos y guia de recuperacion.
12. Etiqueta 1.0 SOLO despues de confirmar el checklist de gate; jamas empaquetes credenciales propias ni datos de usuario.

## Pruebas y verificaciones de fase

- Matriz version Windows, hardware, resolucion/DPI, codec, tipo stream, red, suspension y permisos.
- Install/update/uninstall/clean install; abrir exe sin SDK; verificar runtime VLC y decodificacion.
- `dotnet build -c Release`, `dotnet test -c Release`, pruebas de seguridad y smoke real con usuario.
- Compara resultados con baseline F0/F3; evidencia medible de regresiones cerradas.
- Fallas criticas -> NO liberar 1.0; emite informe de bloqueos.

## Gate de aceptacion

Instalador reproducible funciona en dos equipos Windows o existe evidencia manual pendiente explicitamente; tests relevantes pasan; privacidad y licencias revisadas; aplicacion inicia sin herramientas de desarrollo y reproduce contenido autorizado. No publicar 1.0 si gates criticos no estan aprobados.

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
