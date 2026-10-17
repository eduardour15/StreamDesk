# PROMPT EJECUTABLE — FASE 7: Mejoras opcionales

> **En Codex basta escribir:** `Ejecuta la fase 7`. El archivo `AGENTS.md` contiene el enrutador que carga este prompt.
> Este documento es una instruccion para el agente dentro del repositorio StreamDesk; no lo ejecutes como un script.

## Rol y objetivo

Eres el agente de desarrollo senior de StreamDesk, reproductor IPTV nativo para Windows (C#, .NET 10, WPF/WPF UI, LibVLCSharp, SQLite, MVVM). Lee `AGENTS.md`, `docs/ROADMAP.md`, `docs/ESTADO_FASES.md` y las decisiones de `docs/ADR/` antes de cambiar archivos. **Ejecuta EXCLUSIVAMENTE la Fase 7.** No te limites a generar especificaciones.

**Objetivo concreto:** Agregar funciones adicionales una por una, sin afectar la version estable.

**Precondicion:** F6 version 1.0 aprobada; priorizacion del usuario para funciones extra.

**Entrega esperada:** 1.x segun funcionalidades validadas.

## Alcance obligatorio: IDs estables

- [ ] **F7-T01** Ventana flotante/Picture-in-Picture siempre visible.
- [ ] **F7-T02** Adaptador M3U/M3U8 de importación local y remota si no entró en MVP.
- [ ] **F7-T03** Perfiles de proveedor múltiples y administración de conexiones.
- [ ] **F7-T04** Sincronización cifrada de favoritos e historial entre casa y trabajo (sin sincronizar secretos innecesariamente).
- [ ] **F7-T05** Temas, tamaños, idioma y atajos personalizables.
- [ ] **F7-T06** Control parental, grabación o catch-up solo cuando sean viables y autorizados.

## Instrucciones de implementacion especificas

1. Trata cada F7-Txx como una mini-fase independiente con rama corta, pruebas y gate; NO implementes las seis funciones de golpe si dependen de requisitos inciertos.
2. PiP: prueba solucion tecnica compatible con limitaciones airspace, ventana topmost opcional, cierre/retorno al modo normal y un solo stream en uso.
3. Adaptador M3U local/remoto: parseo robusto, nombres/categorias/logos opcionales, limite de descargas, proteccion contra URL maliciosas/redirecciones y no exposicion de query params con secretos.
4. Multiples perfiles: origen/tipo aislado, ID compuesto por proveedor, credenciales cifradas por perfil, cambio sin dejar streams activos.
5. Sincronizacion opcional: disena modelo opt-in cifrado E2E, conflictos, dispositivos y privacidad; NO introduzcas servidor ni dependencias mensuales sin aprobacion de usuario.
6. Personalizacion: tema, fuente/tamano, idioma y teclado; mantener accesibilidad, configuracion portable solo sin secretos.
7. Control parental/recordings/catch-up: estudio de factibilidad primero; respetar condiciones del proveedor y derechos; grabacion deshabilitada salvo autorizacion y viabilidad demostrada.
8. Elige la primera tarea con beneficio/impacto claro, ejecútala, pruébala, informa y DETENTE para revision antes de seguir con otra opcional.

## Pruebas y verificaciones de fase

- Por cada mejora: pruebas unitarias/integracion, pruebas manuales relevantes y regresion de TV, perfiles, secretos y performance.
- En PiP y perfiles: confirmar un solo stream cuando aplique.
- En M3U: parser con URLs invalidas, redirects, lista grande y caracteres especiales; SSRF segun contexto.
- En sincronizacion: cifrado, conflictos, desactivacion/borrado de datos y costo conocido.

## Gate de aceptacion

Cada mejora se entrega como incremento verificable con documentacion minima y sin regresiones. La F7 NO es requisito para mantener o liberar la version 1.0.

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
