# ADR-008: importación M3U/M3U8 en memoria

- **Fecha:** 2026-10-19
- **Estado:** aceptada para F7-T02; la validación manual con una lista autorizada queda pendiente.

## Contexto

F7-T02 incorpora M3U/M3U8 sin sustituir el adaptador Xtream ni alterar el esquema SQLite. Las listas pueden incluir tokens en sus URL de origen y de stream, así que el catálogo persistido no debe recibir esas direcciones.

## Decisiones

- `IPlaylistImporter` vive en Application y `M3uPlaylistImporter` en Infrastructure. La UI puede abrir archivos `.m3u`/`.m3u8` o descargar una lista HTTP(S).
- Se admiten cabecera `#EXTM3U`, `#EXTINF`, nombres Unicode y `group-title`. Los logos `tvg-logo` se ignoran para no abrir una segunda superficie de solicitudes de red sin validar. Las listas locales solo aceptan URL absolutas; las listas remotas también pueden usar direcciones relativas a la ubicación de la lista. Se descartan esquemas distintos de HTTP(S), `userinfo` y fragmentos.
- Los canales y sus URI se mantienen en memoria. SQLite recibe cero registros de la lista; favoritos de una lista importada son temporales a la sesión; al reiniciar se debe volver a importar. Nunca se incluye el query string en IDs, estados o mensajes de error.
- Las descargas remotas no envían cookies ni usan proxy, deshabilitan la redirección automática y aplican una política de direcciones públicas tanto al resolver como al abrir el socket mediante `SocketsHttpHandler.ConnectCallback`. Se rechazan direcciones privadas, locales, especiales y de documentación. La validación DNS del canal se repite antes de pasarlo a LibVLC.
- El cuerpo se limita a 16 MiB, el tiempo de descarga a 20 segundos, el número de canales a 50.000 y cada línea a 16 KiB. La lectura usa `ResponseHeadersRead`, por lo que el límite de contenido y el timeout se hacen cumplir también durante el streaming de bytes, según la [documentación de `HttpCompletionOption`](https://learn.microsoft.com/en-us/dotnet/api/system.net.http.httpcompletionoption?view=net-10.0). [`AllowAutoRedirect=false`](https://learn.microsoft.com/en-us/dotnet/api/system.net.http.socketshttphandler.allowautoredirect?view=net-10.0) hace visibles las respuestas 3xx para rechazarlas; el socket de descarga se conecta a la IP pública ya comprobada con [ConnectCallback](https://learn.microsoft.com/en-us/dotnet/api/system.net.http.socketshttphandler.connectcallback?view=net-10.0).
- La UI avisa antes de descargar sobre HTTP sin cifrar y recomienda HTTPS cuando esté disponible. Las direcciones que incluyen `usuario:contraseña@host` se rechazan.

## Consecuencias y límites

La lista se puede reproducir y filtrar durante la sesión, pero todavía no sincroniza metadatos, logos ni EPG. Los favoritos de una lista importada temporalmente no sobreviven al reinicio; las cuentas M3U guardadas conservan solo los IDs favoritos como se describe en el addendum. La validación DNS ocurre antes de llamar al motor LibVLC, que abre su propia conexión; un cambio DNS posterior no puede fijarse desde la API actual de Playback. El riesgo residual queda documentado y no se presenta la comprobación previa como garantía de pinning del stream. La conexión de la lista y la reproducción con un proveedor real requieren la prueba manual de `PRUEBA_MANUAL_F7_T02.md`.

## Addendum 2026-11-06 — favoritos de cuentas M3U guardadas

La decisión de favoritos solo temporales se conserva para una lista importada sin perfil guardado. Para una cuenta M3U guardada, la migración SQLite v7 persiste exclusivamente los IDs estables de canal en `M3uChannelFavorites`, asociados al `ProfileId`. No persiste la lista, URL base, URL de canal, query strings ni tokens. La colección participa en la importación/exportación cifrada de biblioteca, permanece separada por cuenta y no sustituye los datos Xtream ni los favoritos multimedia. Metadatos, logos y EPG de la lista siguen sin persistirse.
