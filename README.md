# StreamDesk

Reproductor IPTV de escritorio para Windows, creado con .NET 10, WPF, WPF UI y LibVLCSharp. Incluye catálogo persistente y un reproductor con estados, controles, cancelación y recuperación limitada. F3 está implementada y pendiente de aceptación multimedia real. La compatibilidad con un proveedor depende de la validación con una cuenta autorizada.

## Requisitos

- Windows x64 con .NET 10 SDK y Windows Desktop Runtime.
- Acceso de red solo para autenticar y consultar el servicio elegido. La app inicia sin Internet.

## Ejecutar

```powershell
dotnet restore StreamDesk.sln
dotnet build StreamDesk.sln
dotnet test StreamDesk.sln
dotnet run --project src/StreamDesk.Probe.Desktop/StreamDesk.Probe.Desktop.csproj
```

En Configuración crea un perfil; sus credenciales se protegen con DPAPI para el usuario actual de Windows. En TV en vivo selecciona el perfil y pulsa Actualizar catálogo. La búsqueda ignora acentos/mayúsculas y permite filtrar por categoría y favoritos existentes. Al reiniciar sin Internet se recuperan los metadatos de la caché; la reproducción necesita acceso al servicio.

El formulario de conexión temporal conserva el flujo F0/F1, sin persistir credenciales ni reemplazar la caché del perfil guardado. Para varios servidores crea perfiles independientes: la app no los intercambia automáticamente. La reproducción empieza únicamente al seleccionar un canal y solicitarla.

## Datos locales

- `%LocalAppData%\StreamDesk\streamdesk.db`: esquema v2 con perfiles, catálogo, fecha de sincronización, favoritos e historial. No contiene usuario/contraseña ni URLs autenticadas.
- `%LocalAppData%\StreamDesk\secrets\`: credenciales cifradas mediante DPAPI CurrentUser.
- `%LocalAppData%\StreamDesk\logs\streamdesk.log`: tipo de excepción e identificador técnico, sin mensajes, rutas solicitadas ni datos de cuenta.

Si el proveedor usa HTTP, usuario y contraseña pueden viajar sin cifrar. Pregunta si ofrece HTTPS; StreamDesk no cambia servidores ni elude límites de conexiones. Usa solo cuentas y contenido autorizados.

## Capas

- `StreamDesk.Probe.Core`: modelos de dominio.
- `StreamDesk.Application`: contratos, perfiles, catálogo y coordinación de reproducción/cancelación/reintentos.
- `StreamDesk.Probe.Infrastructure`: adaptador Xtream, SQLite, DPAPI y logging redactado.
- `StreamDesk.Probe.Playback`: backend nativo LibVLC único y clasificación segura de errores.
- `StreamDesk.Probe.Desktop`: arranque, composición de dependencias, MVVM y UI.
- `tests/StreamDesk.Probe.Tests`: tests del proveedor, SQLite/DPAPI e interfaz WPF con 5.000 canales ficticios.
- `docs/ROADMAP.md` y `docs/ESTADO_FASES.md`: plan y evidencia de fases.
- `docs/PRUEBA_MANUAL_F2.md`: reproducción de pruebas automáticas y protocolo opcional con proveedor real.

Para repetir la prueba WPF: `dotnet test StreamDesk.sln --filter FullyQualifiedName~CatalogUiTests`. Genera una captura y mediciones en `artifacts/`, usando exclusivamente datos inventados. Los logos visibles se descargan bajo demanda y su caché se limita a 128 entradas en memoria.

Los tests simulados validan contratos y persistencia, no una conexión IPTV real ni la reproducción de video.

En reproducción puedes ajustar volumen/mute, cambiar al canal anterior/siguiente del listado filtrado y reintentar un fallo. El búfer (250–10.000 ms) y el modo software se aplican al abrir el siguiente canal. El selector de pantalla determina dónde entra la ventana en modo completo; Esc restaura la ventana. Suspender detiene el medio y no lo reanuda automáticamente.

Las reconexiones automáticas se limitan a dos intentos para fallos reconocidos de red/timeout. Una denegación, formato no soportado o fallo desconocido requiere intervención. El motor puede intentar software una vez ante un fallo identificado de hardware. `docs/CIERRE_F3.md` contiene evidencia y pendientes; `docs/PRUEBA_MANUAL_F3.md` explica la validación física y el recolector `scripts/Measure-Playback.ps1`.

La auditoría de F0–F2 y los pasos para revalidar el video tras la corrección del runtime están en [docs/AUDITORIA_F0_F2.md](docs/AUDITORIA_F0_F2.md). LibVLC se incluye junto al ejecutable; conserva la carpeta `libvlc` al ejecutar o copiar una salida de compilación. Las pruebas inicializan el motor nativo y verifican su cierre sin abrir canales del proveedor.
