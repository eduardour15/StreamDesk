# StreamDesk — prototipo técnico

StreamDesk es un prototipo experimental para validar una aplicación IPTV de escritorio en Windows con .NET 10, WPF, WPF UI y LibVLCSharp. El gate de Fase 0 sigue pendiente hasta probar un servicio autorizado y la interfaz de video en un equipo real.

## Ejecutar

Requisitos: Windows x64 y .NET 10 SDK/Desktop Runtime.

```powershell
dotnet restore StreamDesk.sln
dotnet build StreamDesk.sln
dotnet test StreamDesk.sln
dotnet run --project src/StreamDesk.Probe.Desktop/StreamDesk.Probe.Desktop.csproj
```

La ventana permite introducir una URL base, usuario y contraseña, consultar el estado de cuenta, cargar categorías y canales y solicitar reproducción. La contraseña no se guarda; permanece en memoria solo mientras la sesión está activa. La reproducción inicia únicamente cuando se elige un canal y se pulsa el botón.

Usa solo una cuenta y contenido cuya reproducción tengas autorizada. Si el servidor usa HTTP, la app advierte que las credenciales y datos pueden viajar sin cifrar; consulta al proveedor si ofrece HTTPS. El prototipo no cambia el endpoint ni intenta saltarse los límites de conexiones.

## Estructura

- `src/StreamDesk.Probe.Core`: cliente Xtream inicial, modelos y errores seguros.
- `src/StreamDesk.Probe.Desktop`: pantalla WPF, integración WPF UI y ciclo de vida de LibVLC.
- `tests/StreamDesk.Probe.Tests`: pruebas del cliente con respuestas ficticias.
- `docs/ROADMAP.md`: plan fuente.
- `docs/ESTADO_FASES.md`: evidencias, tareas y gate.
- `docs/PRUEBA_MANUAL_F0.md`: protocolo de validación con el servicio autorizado.
- `docs/DECISION_TECNICA_F0.md`: decisiones provisionales y licencias revisadas.

Las pruebas simuladas no verifican autenticación real, compatibilidad del proveedor, reproducción, overlay, rendimiento ni liberación de conexiones nativas. No compartas credenciales en el chat, repositorio, capturas o logs.
