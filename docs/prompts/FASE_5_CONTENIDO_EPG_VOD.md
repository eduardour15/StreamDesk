# PROMPT EJECUTABLE — FASE 5: EPG peliculas y series

> **En Codex basta escribir:** `Ejecuta la fase 5`. El archivo `AGENTS.md` contiene el enrutador que carga este prompt.
> Este documento es una instruccion para el agente dentro del repositorio StreamDesk; no lo ejecutes como un script.

## Rol y objetivo

Eres el agente de desarrollo senior de StreamDesk, reproductor IPTV nativo para Windows (C#, .NET 10, WPF/WPF UI, LibVLCSharp, SQLite, MVVM). Lee `AGENTS.md`, `docs/ROADMAP.md`, `docs/ESTADO_FASES.md` y las decisiones de `docs/ADR/` antes de cambiar archivos. **Ejecuta EXCLUSIVAMENTE la Fase 5.** No te limites a generar especificaciones.

**Objetivo concreto:** Incorporar guia TV, peliculas, series y progreso de visualizacion sin perjudicar el MVP de TV en vivo.

**Precondicion:** F4 aprobada; catalogo/reproductor estables y existencia de capacidades del proveedor verificadas.

**Entrega esperada:** 0.5 candidata (si el proveedor lo permite).

## Alcance obligatorio: IDs estables

- [ ] **F5-T01** Investigar calidad real de EPG Xtream/XMLTV y mapear zona horaria correctamente.
- [ ] **F5-T02** Añadir EPG de canal actual/siguiente con caché, actualización y estados sin datos.
- [ ] **F5-T03** Integrar categorías y catálogo de películas (VOD), con búsquedas y filtros.
- [ ] **F5-T04** Implementar ficha de película, reproducción y reanudación por posición.
- [ ] **F5-T05** Integrar series, temporadas, episodios y avance por episodio.
- [ ] **F5-T06** Añadir selección de subtítulos/pistas de audio cuando los streams lo permitan.
- [ ] **F5-T07** Implementar historial/continuar viendo sin registrar información innecesaria.
- [ ] **F5-T08** Limitar almacenamiento de carátulas/caché y limpiar contenido obsoleto.
- [ ] **F5-T09** Probar metadatos inconsistentes, sinopsis vacías, streams inaccesibles y series grandes.

## Instrucciones de implementacion especificas

1. Consulta capacidades efectivamente disponibles de EPG y VOD; no inventes canales, temporadas, streaming links ni metadatos.
2. EPG actual/siguiente: integra fuente Xtream/XMLTV autorizada segun disponibilidad, IDs correctos, UTC almacenamiento y conversion local. Maneja datos ausentes.
3. Agrega peliculas VOD: categorias, busqueda, caratulas diferidas, descripcion/metadatos opcionales y ficha.
4. Reutiliza `IPlaybackService` sin duplicar motores; soporta reproduccion VOD y persistencia de progreso de forma consistente.
5. Integra series, temporadas y episodios con IDs compuestos y cuidado ante metadatos incompletos.
6. Pistas de audio/subtitulos: listar y cambiar cuando el motor y el stream realmente los ofrezcan; mensajes cuando no hay.
7. Historial y continuar viendo: guarda posicion/fecha por proveedor y tipo, sin credenciales ni datos superfluos.
8. Caratulas y EPG con cache acotado, expiracion y limpieza de contenido obsoleto sin afectar favoritos.
9. Oculta o deshabilita inteligentemente vistas no disponibles en la cuenta; no prometas catalogo VOD universal.
10. Garantiza que las nuevas funciones no alteran el limite de un unico stream por defecto.

## Pruebas y verificaciones de fase

- Fixtures sin EPG, zonas horarias/DST, XML malformado, peliculas sin poster, episodios duplicados y temporada vacia.
- Tests de progreso, seek permitido, reinicio y cambio de proveedor.
- Performance de listas VOD grandes y limite de cache.
- Regresion completa de TV en vivo y video fullscreen; prueba real solo si el proveedor habilita esas capacidades.

## Gate de aceptacion

EPG y VOD disponibles funcionan donde el proveedor los ofrece, progreso conservado y UI robusta ante datos faltantes. El MVP no se ralentiza ni se rompe si la cuenta carece de VOD/EPG.

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
