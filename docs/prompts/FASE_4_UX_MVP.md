# PROMPT EJECUTABLE — FASE 4: UX UI y MVP

> **En Codex basta escribir:** `Ejecuta la fase 4`. El archivo `AGENTS.md` contiene el enrutador que carga este prompt.
> Este documento es una instruccion para el agente dentro del repositorio StreamDesk; no lo ejecutes como un script.

## Rol y objetivo

Eres el agente de desarrollo senior de StreamDesk, reproductor IPTV nativo para Windows (C#, .NET 10, WPF/WPF UI, LibVLCSharp, SQLite, MVVM). Lee `AGENTS.md`, `docs/ROADMAP.md`, `docs/ESTADO_FASES.md` y las decisiones de `docs/ADR/` antes de cambiar archivos. **Ejecuta EXCLUSIVAMENTE la Fase 4.** No te limites a generar especificaciones.

**Objetivo concreto:** Transformar el reproductor operativo en una experiencia fluida y atractiva para uso diario; completar version 0.1.

**Precondicion:** F3 aprobada con reproduccion real, controles y ciclo de vida estable.

**Entrega esperada:** 0.1 MVP candidato.

## Alcance obligatorio: IDs estables

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

## Instrucciones de implementacion especificas

1. Define y aplica sistema visual coherente (Fluent, oscuro predeterminado, espaciado, tipografia, iconos y estados); evita estetica generica y animaciones pesadas.
2. Crea layout orientado a TV: sidebar compacta, filtros/categorias, lista virtualizada y reproductor protagonista. No tapes el video con controles inoperantes por airspace.
3. Integra favoritos persistentes por perfil + tipo/ID remoto, toggle accesible y filtros inmediatos.
4. Implementa recientes, ultimo canal y opcion *opt-in* de reproducir al iniciar (desactivada de fabrica para no consumir conexion al abrir).
5. Agrega busqueda incremental con debounce moderado sin bloquear, categorias frecuentes y atajos de navegacion.
6. Define atajos Space, Esc, flechas, F, M; documenta comportamiento, evita capturarlos dentro de inputs.
7. Controles visibles al mover mouse, autohide solo cuando sea utilizable; valida mouse/teclado, focus y accesibilidad.
8. Disena estados cargando/sin resultados/offline/timeout/vencido/stream caido, todos con acciones posibles.
9. Prueba DPI 100/125/150/200%, pantalla pequena, maximizada y dos monitores; comprueba tab-order, contraste y textos legibles.
10. Realiza recorrido de primera instalacion y tareas usuarias sin explicaciones: configurar, buscar, reproducir, marcar favorito, regresar a reciente, reiniciar.
11. Crea paquete de prueba v0.1 reproducible y registra defectos con severidad; no etiquetes release si faltan pruebas criticas.

## Pruebas y verificaciones de fase

- Tests de favoritos/recentes, rehidratacion tras reinicio y busqueda; UI smoke tests si el entorno los permite.
- Recorrido manual completo en Windows y capturas sin datos de cuenta; medir numero de interacciones basicas y lag visible.
- Verificar que autohide/no-input-conflict/no-autoplay funcionan y que los casos de error permiten recuperacion.
- Compilacion y regresion de stream/playback.

## Gate de aceptacion

El usuario instala, agrega perfil una vez, explora, busca, reproduce y marca favoritos; al reiniciar mantiene sus preferencias. UI estable, accesible y utilizable; paquete 0.1 listo para evaluacion.

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
