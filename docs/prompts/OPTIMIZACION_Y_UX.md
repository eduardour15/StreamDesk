# PROMPT EJECUTABLE — OPTIMIZACIÓN TÉCNICA Y MEJORA DE USABILIDAD (UX/UI)

> **Instrucción de invocación:** Puedes ejecutar este prompt diciendo: `Ejecuta optimización y UX` o indicando una tarea específica (ej. `Ejecuta UX-OPT-T01`).
> Este documento sigue las normas de `AGENTS.md`, `docs/ROADMAP.md` y `docs/ESTADO_FASES.md`.

---

## 1. Rol y Objetivo

Eres el desarrollador senior de **StreamDesk**, reproductor IPTV nativo de escritorio para Windows (C#/.NET 10, WPF con WPF UI, LibVLCSharp, SQLite, MVVM).

**Objetivo general:**
Implementar las optimizaciones de rendimiento y mejoras de usabilidad resultantes de la auditoría técnica, asegurando que la aplicación sea intuitiva, visualmente moderna, fluida al desplazarse y eficiente en consumo de CPU/RAM, manteniendo intacta la regla de **1 única conexión activa simultánea** y la suite de pruebas libre de regresiones (166+ pruebas unitarias pasando).

---

## 2. Alcance de Implementación Inmediata (IDs Estables)

### Bloque A: Rendimiento y Optimización de Recursos
- [ ] **UX-OPT-T01: Desplazamiento ultra-suave y virtualización avanzada de listas**
  - Configurar `VirtualizingPanel.ScrollUnit="Pixel"`, `VirtualizingPanel.CacheLength="1,2"` y `VirtualizingPanel.CacheLengthUnit="Page"` en las listas de canales, películas, series y episodios.
  - Eliminar saltos bruscos (*jank*) al usar la rueda del ratón o el touchpad en pantallas de alta tasa de refresco (60/120 Hz).
- [ ] **UX-OPT-T02: Decodificación adaptativa y robustez en ChannelLogoLoader**
  - Permitir decodificación contextual de imágenes: 64x64/96x96 px para logos de TV y 240x360 px para pósters de películas y series en `ChannelLogoLoader.cs`.
  - Flexibilizar el filtro de URLs para admitir parámetros de consulta (`QueryString`) válidos (ej. firmas temporales de CDNs o dimensiones de TMDB) sin comprometer secretos ni exponer `UserInfo`.
  - Ampliar la caché en memoria RAM a un límite equilibrado (500 elementos) para evitar descargas HTTP cíclicas durante el scroll de catálogos masivos.
- [ ] **UX-OPT-T03: Optimización del motor multimedia y aceleración por hardware**
  - Configurar decodificación por hardware Direct3D11 explícita (`--avcodec-hw=d3d11va`, `:avcodec-hw=d3d11va`) en `LibVlcBackend.cs` con fallback automático seguro a software.
  - Zapping optimizado: cancelación instantánea del socket y stream previo al cambiar rápidamente de canal con el teclado para evitar congestión de red o bloqueos del proveedor.

### Bloque B: Usabilidad, Ergonomía y Experiencia de Usuario (UX)
- [ ] **UX-OPT-T04: Descubrimiento amigable y desbloqueo de Películas/Series**
  - Mantener accesibles las pestañas de **Películas** y **Series** en la barra lateral sin deshabilitarlas artificialmente.
  - Diseñar pantallas de estado inicial (*Empty State*) atractivas con ilustración, descripción y botón directo de carga (`[📥 Cargar Catálogo]`).
  - Implementar soporte para **Arrastrar y Soltar (Drag & Drop)** de archivos `.m3u` / `.m3u8` directamente sobre la ventana de la aplicación.
- [ ] **UX-OPT-T05: Indicadores visuales de reproducción activa y EPG en lista**
  - Destacar visualmente el canal que está emitiendo en el reproductor (badge animado `🔊 EN VIVO` o borde de color acentuado) para no perderlo al explorar otras categorías o filtrar.
  - Mostrar dial/número de canal si está disponible y una segunda línea con el programa en emisión (`EPG Now`) en cada fila de canal.
- [ ] **UX-OPT-T06: Cuadrícula visual de contenido (Poster Grid) para Películas y Series**
  - Transformar la lista vertical plana de películas y series en un diseño en cuadrícula (*Poster Grid*) con relación de aspecto 2:3, tipografía legible y efecto hover suave.
- [ ] **UX-OPT-T07: Controles modernos de reproductor y OSD central (HUD)**
  - Agregar un indicador OSD semitransparente flotante en pantalla (tipo *heads-up display*) al cambiar volumen, pausar/reanudar o silenciar (especialmente en pantalla completa).
  - Incluir el porcentaje numérico exacto en el deslizador de volumen (ej. `75%`).
  - Agregar botones y atajos de salto rápido de tiempo (`-10s` y `+10s`) para películas y episodios.
  - Hacer visibles y directos los selectores de audio y subtítulos en la barra de control VOD.
- [ ] **UX-OPT-T08: Reestructuración y claridad en la pantalla de Ajustes**
  - Dividir el panel de ajustes en secciones o pestañas organizadas (*Cuentas*, *Reproducción*, *Apariencia*, *Control Parental*, *Sincronización*).
  - Identificar claramente con un badge de estado verde la cuenta que se encuentra actualmente conectada y en uso.

---

## 3. Análisis de Mejoras Futuras (Visión StreamDesk v1.1+)

El agente debe documentar y preparar la arquitectura para las siguientes evoluciones:

1. **Guía de Programación Interactiva (EPG Grid Timeline):**
   - Vista en rejilla horizontal estilo guía de televisión tradicional (canales en el eje Y, línea de tiempo por horas en el eje X) con desplazamiento virtualizado bidireccional.
2. **Caché persistente de logos en disco (Disk Cache LRU):**
   - Almacenamiento local de miniaturas en `%LocalAppData%\StreamDesk\Cache\Logos` con cuota máxima (50 MB) y purga automática por antigüedad (TTL 14 días), permitiendo arranque visual offline instantáneo.
3. **Normalización relacional de VOD en SQLite:**
   - Migrar el almacenamiento de películas/series desde blobs JSON monolíticos (`MediaCache`) hacia tablas SQLite indexadas con soporte de búsqueda instantánea FTS5 (Full Text Search), reduciendo el consumo de RAM en catálogos de más de 20.000 títulos.
4. **Zapping predictivo con búfer de precalentamiento (Smart Buffering):**
   - Análisis de latencia y ajuste dinámico del búfer de red (500 ms – 3000 ms) en función de la estabilidad de la conexión para evitar micro-congelamientos.
5. **Modo Cine / Mini-Player integrado:**
   - Capacidad de navegar por el catálogo manteniendo una pequeña ventana interna de video visible en la esquina inferior derecha antes de pasar a PiP nativo.

---

## 4. Reglas Innegociables de Ejecución Técnica

- **Regla de 1 único stream:** No abrir múltiples conexiones concurrentes con el proveedor bajo ninguna circunstancia. Detener deterministamente el stream previo antes de abrir el siguiente.
- **Seguridad y Privacidad:** Nunca imprimir credenciales, URLs autenticadas con usuario/contraseña ni tokens en logs, pantallas ni archivos. Mantener contraseñas protegidas con DPAPI `CurrentUser`.
- **Integridad de Pruebas:** Ejecutar `dotnet build StreamDesk.sln` y `dotnet test StreamDesk.sln` asegurando que los 166 tests existentes sigan pasando al 100% y agregar pruebas unitarias para cada nueva funcionalidad introducida.
- **Sin bloqueos de hilo UI:** Todas las operaciones de red, decodificación y SQLite deben realizarse asíncronamente con soporte de `CancellationToken`.

---

## 5. Formato de Entrega y Cierre

1. **Detalle de tareas implementadas:** Tareas abordadas, archivos creados/modificados y cambios en la interfaz.
2. **Comandos ejecutados y resultados:** Compilación y suite de pruebas (`dotnet test`).
3. **Pruebas manuales sugeridas:** Pasos para que el usuario verifique la fluidez del scroll, los pósters nítidos y los nuevos controles.
4. **Actualización de bitácora:** Registrar el incremento en `docs/ESTADO_FASES.md` con fecha y evidencias.
