# StreamDesk

Reproductor IPTV de escritorio para Windows, creado con .NET 10, WPF, WPF UI y LibVLCSharp. F1 agrega navegación, perfiles locales, SQLite versionado y protección DPAPI CurrentUser. La compatibilidad con un proveedor depende de la validación manual con una cuenta autorizada.

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

TV en vivo conserva el flujo de prueba: introduce la URL base, usuario y contraseña, carga el catálogo y solicita reproducción tras elegir canal. La contraseña se borra del control al conectar y no se guarda en esa sesión. Configuración permite crear y borrar perfiles locales; sus credenciales se protegen con DPAPI para el usuario actual de Windows.

## Datos locales

- `%LocalAppData%\StreamDesk\streamdesk.db`: perfiles y tablas iniciales de catálogo, favoritos e historial. No contiene contraseñas.
- `%LocalAppData%\StreamDesk\secrets\`: credenciales cifradas mediante DPAPI CurrentUser.
- `%LocalAppData%\StreamDesk\logs\streamdesk.log`: tipo de excepción e identificador técnico, sin mensajes, rutas solicitadas ni datos de cuenta.

Si el proveedor usa HTTP, usuario y contraseña pueden viajar sin cifrar. Pregunta si ofrece HTTPS; StreamDesk no cambia servidores ni elude límites de conexiones. Usa solo cuentas y contenido autorizados.

## Capas

- `StreamDesk.Probe.Core`: modelos de dominio.
- `StreamDesk.Application`: contratos y coordinación de perfiles.
- `StreamDesk.Probe.Infrastructure`: adaptador Xtream, SQLite, DPAPI y logging redactado.
- `StreamDesk.Probe.Playback`: servicio LibVLC único.
- `StreamDesk.Probe.Desktop`: arranque, composición de dependencias, MVVM y UI.
- `tests/StreamDesk.Probe.Tests`: tests unitarios del proveedor y tests de integración de almacenamiento con datos ficticios.
- `docs/ROADMAP.md` y `docs/ESTADO_FASES.md`: plan y evidencia de fases.
- `docs/PRUEBA_MANUAL_F1.md`: verificación Windows pendiente de navegación, perfiles y regresión de playback.

Los tests simulados validan contratos y persistencia, no una conexión IPTV real ni la reproducción de video.
