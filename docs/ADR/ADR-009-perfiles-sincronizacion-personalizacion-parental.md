# ADR-009: perfiles múltiples, sincronización cifrada, personalización y control parental (F7-T03 a F7-T06)

- **Fecha:** 2026-10-19
- **Estado:** Aceptada e implementada para culminar la Fase 7.

---

## Contexto

La Fase 7 aborda mejoras opcionales posteriores a la versión 1.0 (F7-T01 a F7-T06). Con F7-T01 (Picture-in-Picture) y F7-T02 (Importador M3U en memoria) ya implementados, las tareas restantes requerían:
- Administración de perfiles múltiples (Xtream y M3U) con garantía estricta de un único stream activo y desconexión limpia (F7-T03).
- Mecanismo de sincronización de favoritos e historial entre equipos (hogar y trabajo) sin exponer secretos ni requerir servidores en la nube de pago (F7-T04).
- Personalización de temas (oscuro/claro), escalado de interfaz (100%, 115%, 130%), idioma y atajos de teclado (F7-T05).
- Control parental con protección criptográfica de PIN y estudio de factibilidad de grabación (DVR) y catch-up (F7-T06).

---

## Decisiones

### 1. Perfiles múltiples y gestión de conexiones (F7-T03)
- Se extiende el modelo `Profile` con el atributo `ProviderType` (`"xtream"` o `"m3u"`).
- Se aplica la migración v5 en `SqliteSettingsService` añadiendo la columna `ProviderType` a `Profiles` de forma compatible hacia atrás.
- Al desconectar o cambiar de perfil mediante `DisconnectProfileCommand` o `SwitchProfileCommand`:
  - Se detiene la reproducción activa inmediatamente (`PlaybackEngine.StopAsync`).
  - Se libera el stream en curso, garantizando que jamás coexistan dos conexiones concurrentes al proveedor.
  - Se reinicia el catálogo en memoria y se limpian los recursos asociados.
- Se implementa `CheckConnectionAsync` para verificar el estado de la conexión sin forzar una recarga destructiva del catálogo ni reautenticaciones repetitivas.

### 2. Sincronización cifrada de favoritos e historial (F7-T04)
- **Cero secretos exportados:** Los paquetes de sincronización exportan únicamente identificadores abstractos de canal, nombres de canal, nombres de categoría, marcas de tiempo de agregado y progreso de reproducción. **Bajo ninguna circunstancia** se exportan nombres de usuario, contraseñas, tokens de sesión ni URLs de servidor.
- **Criptografía robusta (E2E):** Formato binario con cabecera mágica `STREAMDESK_SYNC_V1`.
  - Derivación de clave: PBKDF2 con HMAC-SHA256, 100.000 iteraciones y salt de 16 bytes.
  - Cifrado autenticado: AES-256-GCM con IV de 12 bytes y etiqueta de autenticación (auth tag) de 16 bytes.
- **Modelo sin servidor:** La exportación e importación se realiza mediante archivos cifrados locales (transferibles por pendrive, nube personal del usuario o carpeta compartida), evitando infraestructura externa o costos recurrentes.
- **Resolución de conflictos:** Estrategia Last-Write-Wins (la marca de tiempo más reciente prevalece al fusionar favoritos e historial).

### 3. Personalización de UI, accesibilidad y atajos (F7-T05)
- Tabla `AppSettings` en SQLite (esquema v5) para almacenar: `Theme` (`"Dark"`, `"Light"`), `UiScale` (`1.0`, `1.15`, `1.30`), `Language` (`"es"`, `"en"`), y configuración parental.
- Aplicación dinámica en WPF UI:
  - Temas claro/oscuro aplicados mediante `ApplicationThemeManager.Apply(...)`.
  - Escalado de interfaz mediante `ScaleTransform` en el contenedor raíz de `MainWindow`, permitiendo legibilidad a distancia o en pantallas de alta densidad (115% y 130%).
  - Atajos de teclado estándar documentados y centralizados en la interfaz:
    - Espacio: Reproducir / Pausar
    - M: Silenciar (Mute)
    - F: Pantalla completa (Fullscreen)
    - P: Picture-in-Picture
    - Flechas Arriba/Abajo: Control de volumen
    - Escape: Salir de pantalla completa / PiP

### 4. Control parental y factibilidad (F7-T06)
- **PIN Criptográfico:** PIN numérico de 4 dígitos protegido mediante PBKDF2 (HMAC-SHA256, 100.000 iteraciones) con salt aleatorio de 16 bytes. Ni el PIN ni su valor reversible se almacenan en texto claro.
- **Estado de sesión efímero:** El estado de desbloqueo reside exclusivamente en memoria durante la sesión activa. Al cerrar o reiniciar la aplicación, el catálogo vuelve a bloquearse por defecto.
- **Filtrado reactivo:** Al estar bloqueado, se ocultan del árbol de canales, películas y series todas aquellas categorías cuyos nombres coincidan con patrones de contenido adulto (`adult`, `xxx`, `18+`, `erotic`, `porn`, `adultos`, etc.).
- **Estudio de factibilidad:** Documentado en `docs/ESTUDIO_FACTIBILIDAD_F7_T06.md`. Grabación (DVR) desestimada por violación de la regla de 1 stream activo y riesgo de suspensión de cuenta del usuario; Catch-up postergado a versión 1.1+ condicionado a soporte del backend Xtream.

---

## Consecuencias y límites

1. **Seguridad y Privacidad:** Las credenciales del usuario permanecen en DPAPI local; los archivos de sincronización son seguros incluso si se transmiten por canales no seguros gracias a AES-256-GCM.
2. **Garantía de Stream Único:** La política de 1 stream activo se mantiene sin excepciones en perfiles múltiples, desconexión y cambio de fuente.
3. **Compatibilidad:** La migración v5 de SQLite es no destructiva y compatible con perfiles creados en versiones anteriores (F1 a F6).
4. **Verificación:** Pruebas unitarias automatizadas cubren perfiles múltiples, sincronización cifrada, temas y control parental con 100% de éxito.
