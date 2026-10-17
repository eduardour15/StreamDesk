# ADR-001: capas de aplicación, persistencia local y secretos

- **Fecha:** 2026-10-17
- **Estado:** Aceptada para F1

## Contexto

El prototipo F0 necesita crecer a una aplicación WPF que arranque sin conexión, permita navegar y administre perfiles sin guardar contraseñas en SQLite o logs. Se debe conservar LibVLC y mantener un único medio activo.

## Decisión

- Separar modelos (`StreamDesk.Probe.Core`), contratos/coordinación (`StreamDesk.Application`), integraciones y almacenamiento (`StreamDesk.Probe.Infrastructure`), LibVLC (`StreamDesk.Probe.Playback`) y UI/composición WPF (`StreamDesk.Probe.Desktop`). La inyección de dependencias se realiza en el punto de composición de `App` mediante constructores, sin introducir un contenedor adicional.
- Usar `Microsoft.Data.Sqlite` 10.0.13 con `SchemaVersion` y migración explícita. SQLite contiene datos del perfil y catálogo, nunca usuario, contraseña o URL autenticada.
- Proteger usuario/contraseña como archivo DPAPI `CurrentUser` bajo `%LocalAppData%\StreamDesk\secrets`, usando una clave aleatoria de perfil como entropía. Al eliminar un perfil se elimina su fila y archivo asociado.
- Mantener un solo servicio LibVLC y un `MediaPlayer`; el cambio de canal detiene y sustituye el medio actual.
- Los logs contienen solo identificador técnico y tipo de excepción, sin mensaje ni stack trace.

## Consecuencias

Los secretos solo pueden descifrarse bajo el usuario de Windows que los creó; cambiar de cuenta de Windows requiere una futura función explícita de migración. La eliminación borra el archivo protegido mediante el sistema de archivos, sin prometer borrado físico seguro del medio de almacenamiento. Revisar licencias y avisos transitivos de VLC sigue siendo requisito antes de distribuir.

La integración fue comprobada con tests Windows de migración, ausencia de secretos en SQLite, ciclo DPAPI y CRUD de perfil; la interacción visual y la regresión de reproducción quedan para la prueba manual F1.
