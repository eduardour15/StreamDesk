# Estudio de factibilidad: Control Parental, Grabación (DVR) y Catch-up (TV Archive) — F7-T06

- **Fecha:** 2026-10-19
- **Fase:** Fase 7 (Mejoras opcionales posteriores, versión 1.x)
- **Tarea:** F7-T06
- **Estado:** Aprobado / Implementado (Control Parental) y Dictamen Técnico Documentado (Grabación y Catch-up).

---

## 1. Resumen ejecutivo

Este estudio evalúa la viabilidad técnica, operativa, legal y de seguridad para tres funcionalidades avanzadas de IPTV en StreamDesk:
1. **Control Parental:** Viable y necesario. Implementado localmente mediante hash seguro (PBKDF2/SHA256) y filtrado dinámico de categorías adultas.
2. **Grabación de transmisiones (DVR):** No viable en la versión actual. Deshabilitada por defecto para proteger la cuenta del usuario contra penalizaciones por exceder el límite de 1 stream activo y evitar infracciones de términos de servicio.
3. **Catch-up / Archivo de televisión (TV Archive):** Viable condicionalmente en proveedores Xtream que expongan soporte nativo de timeshift (`tv_archive = 1`), pero postergado para versión 1.1+ por alta dependencia del backend del proveedor.

---

## 2. Análisis detallado por funcionalidad

### 2.1. Control Parental (Implementado)

#### Viabilidad técnica
- **Mecanismo:** Servicio `ParentalControlService` integrado en `StreamDesk.Application`.
- **Seguridad de credenciales:** El PIN de 4 dígitos no se almacena en texto plano. Se genera un salt criptográfico de 16 bytes (`RandomNumberGenerator`) y se deriva el hash con PBKDF2 (HMAC-SHA256, 100.000 iteraciones).
- **Persistencia:** La configuración de hash, salt y estado habilitado se almacena en la tabla `AppSettings` de SQLite (migración v5).
- **Ciclo de vida en memoria:** El desbloqueo es de sesión temporal (`IsLocked`). Al reiniciar la aplicación o bloquear manualmente, la vista oculta de inmediato las categorías marcadas como adultas.
- **Detección:** Expresiones regulares que inspeccionan nombres de categorías y grupos (`adult`, `xxx`, `18+`, `erotic`, `porn`, `adultos`, etc.).

#### Viabilidad legal y contractual
- Plenamente compatible. No interfiere con el proveedor ni altera flujos de red; opera exclusivamente en la capa de presentación del cliente para salvaguardar a menores de edad.

#### Dictamen
- **ESTADO: COMPLETADA E IMPLEMENTADA EN F7-T06.**

---

### 2.2. Grabación local / DVR (Desestimada)

#### Viabilidad técnica
- LibVLC permite duplicar o redirigir la salida mediante opciones de `--sout` o `libvlc_media_player_record`.
- Sin embargo, las transmisiones MPEG-TS de IPTV suelen sufrir jitter, pérdidas de paquetes y reconexiones que corrompen contenedores MP4/MKV si no se cuenta con un multiplexor tolerante a fallos.
- El almacenamiento en disco consume entre 1.5 GB y 4.5 GB por hora en transmisiones 1080p a 50/60 fps.

#### Restricciones operativas y de proveedor (Críticas)
- **Política de 1 stream activo:** La arquitectura innegociable de StreamDesk garantiza 1 única conexión activa simultánea. Para grabar un canal mientras el usuario visualiza otro (o incluso el mismo sin un splitter local complejo), se requeriría abrir un segundo socket HTTP al proveedor. Los paneles Xtream y proveedores IPTV cancelan o bloquean cuentas inmediatamente cuando detectan conexiones concurrentes fuera del plan contratado.
- **Términos de servicio:** La mayoría de los proveedores prohiben explícitamente el volcado masivo o automatizado de streams.

#### Dictamen
- **ESTADO: DESESTIMADA / DESHABILITADA POR DEFECTO.** No se implementará grabación de fondo en 1.x para evitar suspensiones de cuenta del usuario y respetar la regla fundamental de conexión única.

---

### 2.3. Catch-up / TV Archive (Postergado a 1.1+)

#### Viabilidad técnica
- La API Xtream Codes expone en `get_live_streams` los campos opcionales:
  - `"tv_archive"`: entero binario (`1` si el servidor almacena historial de transmisiones, `0` si solo es en vivo).
  - `"tv_archive_duration"`: días de almacenamiento (típicamente 1 a 7 días).
- La URL de reproducción para timeshift requiere parámetros específicos de consulta en el servidor (`/timeshift.php?username=...&password=...&stream=...&duration=...&start=...`).

#### Limitaciones
- **Dependencia de proveedor:** Más del 70% de las listas y servidores Xtream económicos tienen `tv_archive = 0` desactivado por el alto costo de almacenamiento en disco en sus servidores.
- **M3U estándar:** Las listas M3U estándar no tienen especificación uniforme para catch-up (algunas usan `catchup="append"`, `catchup-days="3"`, otras Flussonic, etc.).
- **Sincronización EPG:** Requiere que la guía EPG tenga precisión al segundo con la zona horaria del servidor del proveedor, lo que suele presentar desfases de zona horaria complejos.

#### Dictamen
- **ESTADO: FACTIBLE PERO POSTERGADO A 1.1+.** Se documenta como candidato para futuras revisiones cuando se incorpore un visor de guía EPG con soporte de reproducción histórica.

---

## 3. Matriz de decisión

| Funcionalidad | Viabilidad Técnica | Viabilidad Legal/Términos | Impacto en 1 Stream Activo | Decisión |
|---|---|---|---|---|
| **Control Parental (PIN)** | Alta (PBKDF2/SHA256) | Totalmente conforme | Ninguno (filtro en catálogo) | **Aprobada e Implementada** |
| **DVR / Grabación Local** | Media (Riesgo corrupción TS) | Crítica (Riesgo baneo de cuenta) | Alto (requiere streams concurrentes) | **Desestimada / Deshabilitada** |
| **Catch-up / TV Archive** | Media (Dependiente de backend) | Conforme si proveedor lo soporta | Ninguno (reemplaza stream en vivo) | **Postergada a v1.1+** |

---

## 4. Conclusión

La tarea F7-T06 cumple con la directriz del ROADMAP de anteponer un estudio de factibilidad riguroso antes de cualquier desarrollo. El Control Parental queda completamente operativo, mientras que la grabación queda descartada por seguridad del usuario y el Catch-up catalogado para una versión futura especializada.
