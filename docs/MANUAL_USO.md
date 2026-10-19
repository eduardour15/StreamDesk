# StreamDesk — candidato 0.5 para Windows x64

## Instalar y actualizar

El candidato F5/F6 todavía no es 1.0. Conserva juntos `app`, `licenses`, `files.json`, `candidate.json` y los scripts. El paquete autocontenido incluye .NET Desktop y LibVLC x64; no necesitas SDK ni Visual Studio para ejecutarlo. La verificación en Windows limpio y ambos equipos sigue pendiente.

En la carpeta del candidato, ejecutar:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Manage-StreamDesk.ps1 -Action Install
```

Instala en `%LocalAppData%\Programs\StreamDesk`, registra desinstalación por usuario y crea acceso en Inicio. No requiere administrador. El script verifica hashes de archivos antes de activar la versión; detecta corrupción, no certifica la identidad del editor. No ejecutes paquetes de origen desconocido. La política de ejecución se aplica solo a ese proceso.

Para actualizar, cierra StreamDesk y ejecuta el mismo comando desde el candidato nuevo, con una versión distinta. Se conserva la versión anterior y se cambia el marcador activo una vez copiada y verificada la nueva. No se sobrescriben binarios en uso. Si Windows o TI impide scripts en un equipo corporativo, solicita un mecanismo aprobado de instalación; no cambies políticas corporativas.

## Configurar y usar

1. Abrir desde Inicio. En Configuración, guardar nombre, servidor base, usuario y contraseña. Las credenciales se protegen para el usuario actual de Windows. Si el servicio usa HTTP, consulta si admite HTTPS: las credenciales pueden viajar sin cifrar. Cada servidor es un perfil independiente.
2. TV en vivo: elegir perfil y Actualizar. Buscar sin distinguir acentos, filtrar categoría, seleccionar y reproducir. Las estrellas guardan favoritos; Recientes e Inicio permiten volver al último canal.
3. «Consultar películas y series» comprueba catálogos de la cuenta. Solo se habilitan vistas con datos. Seleccionar título, leer ficha, elegir temporada/episodio y Ver/reanudar. Continuar conserva posición por perfil. «Desde inicio» ignora progreso. No se descargan videos.
4. Guía: ahora/siguiente en hora local Windows; Actualizar guía o importar XMLTV autorizado con ID EPG coincidente. Datos ausentes/horarios ambiguos no se inventan. XML con DTD/entidades se rechaza.
5. Posición y pistas: seek solo cuando lo permite el motor. Elegir pista declarada y Aplicar. No todos los streams ofrecen subtítulos/audio alternativo. Anterior/siguiente avanza manualmente episodios en Series; en TV sigue el orden visible.
6. Espacio reproduce/detiene, F pantalla completa, Esc restaura, M mute, ←/→ anterior/siguiente y ↑/↓ volumen fuera de listas/controles de edición. Se respetan inputs y navegación de listas. Mouse/teclado recupera controles ocultos. La reproducción al iniciar está apagada de fábrica; requiere activar explícitamente la preferencia del perfil. Suspender o cambiar perfil no inicia video automáticamente.

## Ventana flotante (PiP)

En una vista de TV, película o serie, pulsa «Ventana flotante» o P fuera de campos de edición. La misma ventana se vuelve compacta y conserva el medio activo. Arrástrala desde su título y cambia su tamaño desde los bordes. «Fijar» activa/desactiva que permanezca sobre otras ventanas.

«Volver» o Esc restaura tamaño, posición y estado previos sin reiniciar la reproducción. F pasa a pantalla completa; P desde pantalla completa entra en PiP. Esc siempre vuelve al modo normal. PiP conserva detener/reproducir, silenciar, reintentar y los atajos de reproducción; los controles permanecen visibles. Para elegir otro título o ajustar pistas, vuelve a la vista normal. Cerrar con la X termina StreamDesk y libera el reproductor; no deja reproducción en segundo plano. Minimizar conserva el comportamiento habitual de Windows.

## Copias, diagnóstico y recuperación

En Configuración, «Crear copia protegida» genera una instantánea SQLite consistente y copia solo los secretos DPAPI referenciados en `%LocalAppData%\StreamDesk\backups\backup-*`. Si falta un secreto o falla la copia, no se presenta una copia parcial como válida. Solo funciona bajo el mismo usuario de Windows; no es una exportación portable de credenciales. Las copias contienen datos personales del catálogo/perfiles: mantenlas locales y elimina las antiguas cuando ya no las necesites.

«Diagnóstico sin datos de cuenta» genera un JSON local con SO/runtime/arquitectura y códigos técnicos permitidos. No contiene cuentas, URLs de solicitud, stacks ni dumps. Revisa el archivo antes de compartirlo. Los logs tienen cola acotada y dos archivos de hasta 1 MiB cada uno.

Para revertir binarios, cerrar la aplicación y ejecutar el gestor de la versión instalada con `-Action Rollback`. La versión previa se reactiva; no se degradan bases de datos automáticamente. Si una versión antigua no entiende el esquema, conserva tus datos y usa una copia compatible.

Restauración manual de configuración, solo con StreamDesk cerrado:

1. Guardar una copia de seguridad de la carpeta actual `%LocalAppData%\StreamDesk` en otra carpeta local del mismo usuario.
2. Elegir una copia terminada con `backup.json`; comprobar su versión de esquema y usar una aplicación compatible.
3. Sustituir `streamdesk.db` y la carpeta `secrets` por los de esa copia. Eliminar únicamente los archivos auxiliares `streamdesk.db-wal` y `streamdesk.db-shm` de la carpeta de datos cerrada antes de abrir la base restaurada. No mezclar una base con secretos de otra copia.
4. Abrir y confirmar perfiles/favoritos/progreso. Si algo falla, cerrar y recuperar la copia anterior. No restaurar en otra cuenta de Windows esperando descifrar DPAPI.

## Desinstalar y problemas conocidos

Cerrar StreamDesk y usar Aplicaciones de Windows o ejecutar el gestor con `-Action Uninstall`. Se eliminan binarios/registro/acceso directo de esa instalación; **se conservan** `%LocalAppData%\StreamDesk`, perfiles y copias. Para borrar credenciales, eliminar perfiles desde Configuración antes de desinstalar. Para borrar todos los datos después, eliminar expresamente esa carpeta local y sus copias cuando ya no se necesiten.

Pendientes: pruebas reales en dos equipos, DPI/monitores físicos, codecs/formatos/capacidades reales, Wi-Fi y suspensión física, revisión completa de licencias/fuentes de plugins. No hay compatibilidad universal ni release 1.0 aprobada. Los scripts de instalación se prueban sin registrar en Windows durante los tests; acceso directo y registro deben verificarse manualmente. Véase `PRUEBA_MANUAL_F6.md` del repositorio.
