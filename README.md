# StreamDesk

Reproductor IPTV para Windows con .NET 10, WPF/WPF UI y LibVLCSharp. Incluye TV, favoritos, recientes y, cuando la cuenta ofrece datos, EPG/películas/series con progreso y pistas. El candidato **0.5.0-rc.2 autocontenido win-x64** agrega ventana flotante/PiP; 1.0 sigue pendiente de pruebas reales y revisión completa de licencias nativas.

## Requisitos

- Para desarrollar: Windows x64 con .NET 10 SDK. El candidato autocontenido no necesita SDK ni runtime externo.
- Acceso de red para autenticar, consultar y reproducir el servicio elegido. La app inicia sin Internet.

## Ejecutar

```powershell
dotnet restore StreamDesk.sln
dotnet build StreamDesk.sln
dotnet test StreamDesk.sln
dotnet run --project src/StreamDesk.Probe.Desktop/StreamDesk.Probe.Desktop.csproj
```

En Configuración crea un perfil; sus credenciales se protegen con DPAPI para el usuario actual de Windows. En TV en vivo selecciona el perfil y pulsa Actualizar. La búsqueda ignora acentos/mayúsculas; las estrellas guardan favoritos y la barra lateral abre Favoritos/Recientes. Inicio permite volver al último canal. Al reiniciar sin Internet se recuperan metadatos y biblioteca; la reproducción necesita acceso al servicio.

El formulario de conexión temporal está en Configuración, sin persistir credenciales ni reemplazar la caché del perfil guardado. Para varios servidores crea perfiles independientes. La reproducción requiere acción del usuario salvo que active explícitamente reproducir el último canal al iniciar, apagado por defecto. Cambiar perfil o reanudar Windows nunca dispara autoplay.

## Datos locales

- `%LocalAppData%\StreamDesk\streamdesk.db`: esquema v4 con perfiles, TV/VOD/EPG, favoritos, recientes y progreso por perfil/tipo. Sin usuario/contraseña ni URLs autenticadas.
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

Los controles están junto al video y se ocultan a los 3 segundos de reproducción sin interacción; movimiento/teclado los recupera. Espacio reproduce/detiene, ←/→ cambia canal, ↑/↓ ajusta volumen fuera de listas/inputs, F pantalla completa, M mute y Esc restaura ventana. Se respetan los campos de edición y controles con función propia.

«Ventana flotante» en el encabezado o P activa PiP con el mismo reproductor. «Fijar» permite mantenerlo encima de otras ventanas; Volver/Esc restaura la ventana anterior sin reiniciar el medio. PiP mantiene sus controles visibles; cerrar con X termina la aplicación. [Prueba física PiP](docs/PRUEBA_MANUAL_F7_T01.md).

Para generar el candidato: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Build-Candidate.ps1`. Imprime una carpeta de `artifacts/candidates`. Ejecutar su `Manage-StreamDesk.ps1 -Action Install`; admite Install/Rollback/Uninstall y conserva los datos personales. Incluye .NET Desktop y VLC, sin SDK requerido. Para reproducir el test aislado: `scripts/Test-Candidate.ps1 -Candidate <carpeta>`; usa carpetas del proyecto y omite registro/acceso directo.

En TV, «Consultar películas y series» verifica catálogos de la cuenta. Guía permite actualización/importación XMLTV; VOD ofrece ficha, temporadas/episodios, reanudación y pistas declaradas por el medio. Configuración incorpora copia protegida y diagnóstico sin datos de cuenta. El mismo motor mantiene un stream activo. [Manual](docs/MANUAL_USO.md), [cierre F5](docs/CIERRE_F5.md), [cierre F6](docs/CIERRE_F6.md), [privacidad](docs/PRIVACIDAD_F6.md) y protocolos manuales distinguen fixtures de pruebas reales. No se publica ni etiqueta una release 1.0.

La auditoría de F0–F2 y los pasos para revalidar el video tras la corrección del runtime están en [docs/AUDITORIA_F0_F2.md](docs/AUDITORIA_F0_F2.md). LibVLC se incluye junto al ejecutable; conserva la carpeta `libvlc` al ejecutar o copiar una salida de compilación. Las pruebas inicializan el motor nativo y verifican su cierre sin abrir canales del proveedor.
