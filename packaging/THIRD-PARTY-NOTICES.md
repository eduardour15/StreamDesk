# StreamDesk 0.1 — candidato local de evaluación

No es una release pública. Este candidato requiere Windows x64 y .NET 10 Windows Desktop Runtime; no requiere SDK para ejecutarlo. Conserva la carpeta `libvlc` y las bibliotecas administradas junto al ejecutable.

Dependencias directas fijadas: WPF-UI 4.3.0 (MIT), LibVLCSharp y LibVLCSharp.WPF 3.10.2 (LGPL-2.1-or-later), VideoLAN.LibVLC.Windows 3.0.24 (LGPL-2.1-or-later), Microsoft.Data.Sqlite / Microsoft.Extensions.Http / System.Security.Cryptography.ProtectedData 10.0.13 (MIT). `licenses/packages.json` inventaría todos los paquetes resueltos con su declaración de licencia; `licenses/` conserva nuspec y archivos de licencia/avisos presentes en los paquetes originales.

Los binarios de VLC se cargan como DLL separadas. Fuentes y condiciones: [LibVLCSharp](https://code.videolan.org/videolan/LibVLCSharp), [paquete LibVLC](https://code.videolan.org/videolan/libvlc-nuget), [VLC](https://code.videolan.org/videolan/vlc), [contribuciones nativas](https://code.videolan.org/videolan/vlc/-/tree/3.0.x/contrib/src). Cada biblioteca/plugin puede tener condiciones distintas de la declaración global del paquete. No se afirma que el inventario NuGet cubra los avisos y fuentes correspondientes de todos los plugins; su revisión completa sigue pendiente antes de redistribuir externamente.

No contiene cuentas ni datos locales del desarrollador. El instalador copia solo `app` y avisos a una carpeta nueva del usuario, sin privilegios de administrador. Para quitarlo, cierra StreamDesk y elimina esa carpeta y su acceso directo. Los datos de `%LocalAppData%\StreamDesk` se conservan; elimina perfiles desde Configuración si deseas borrar también sus credenciales protegidas.
