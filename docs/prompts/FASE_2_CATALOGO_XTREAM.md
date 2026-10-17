# PROMPT EJECUTABLE — FASE 2: Integracion Xtream y catalogo

> **En Codex basta escribir:** `Ejecuta la fase 2`. El archivo `AGENTS.md` contiene el enrutador que carga este prompt.
> Este documento es una instruccion para el agente dentro del repositorio StreamDesk; no lo ejecutes como un script.

## Rol y objetivo

Eres el agente de desarrollo senior de StreamDesk, reproductor IPTV nativo para Windows (C#, .NET 10, WPF/WPF UI, LibVLCSharp, SQLite, MVVM). Lee `AGENTS.md`, `docs/ROADMAP.md`, `docs/ESTADO_FASES.md` y las decisiones de `docs/ADR/` antes de cambiar archivos. **Ejecuta EXCLUSIVAMENTE la Fase 2.** No te limites a generar especificaciones.

**Objetivo concreto:** Obtener y presentar categorias y miles de canales con busqueda, filtros, sincronizacion y funcionamiento offline parcial.

**Precondicion:** F1 aprobada, contratos implementados, secretos aislados, esquema SQLite listo.

**Entrega esperada:** Catalogo funcional y probado.

## Alcance obligatorio: IDs estables

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

## Instrucciones de implementacion especificas

1. Implementa adaptador Xtream independiente de ViewModels; consume cuenta/categorias/canales con tipado robusto, fixtures y manejo de valores faltantes.
2. Maneja explicitamente acceso denegado, vencimiento, errores de red y limites de conexion sin exponer credenciales ni URLs autenticadas.
3. Define mapeo entre IDs remotos y locales, categorias por proveedor/tipo, logos/EPG opcionales; no equipares servidores alternativos sin comprobar que aceptan las mismas credenciales/recursos.
4. Usa `HttpClientFactory`, cancelacion, limites de concurrencia, timeouts y reintentos restringidos a consultas idempotentes/transitorias; respeta `Retry-After` si corresponde.
5. Implementa sincronizacion incremental o reemplazo atomico de catalogo (elegir segun API), marca fecha de sincronizacion y ofrece refresco manual con estado visible.
6. Crea busqueda sin depender de la red, normaliza acentos/mayusculas, agrega filtro categoria y soporte de favoritos existentes.
7. En WPF utiliza virtualizacion real y cargas diferidas de imagenes con cache limitado; evita materializar miles de controles y bloqueos de UI.
8. Crea datos de prueba de al menos 5,000 canales, registros sin logo, nombres repetidos, categorias vacias, respuestas parciales y cambios de ID.
9. Verifica que la cuenta se configure solo desde la app. No introduzcas endpoints ni secretos reales en codigo/tests/documentacion.

## Pruebas y verificaciones de fase

- Unit tests de mapeo, duplicados, categorias, expiracion, timeouts, cancelacion y filtros con datos inventados.
- Integration tests SQLite para sincronizacion atomica, reconstruccion de indices, cache y relanzamiento.
- Prueba de interfaz con catalogo simulado >=5,000 canales, scroll virtualizado, busqueda por acentos y refresco fallido.
- Prueba opcional con proveedor real autorizado; declara si no es posible por limitaciones de entorno.

## Gate de aceptacion

Categorias/canales reales cuando el proveedor responde, catalogo local consistente, busqueda y filtrado fluidos con miles de elementos. Con Internet interrumpido se muestran metadatos cacheados sin prometer reproduccion offline.

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
