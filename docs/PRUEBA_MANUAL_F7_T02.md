# Prueba manual F7-T02 — importación M3U/M3U8

Estado: **PENDIENTE DE VALIDACIÓN** con una lista autorizada del proveedor. No uses listas de terceros sin autorización.

1. En TV en vivo, abre **Importar lista M3U/M3U8** y elige un archivo local `.m3u` o `.m3u8` con nombres acentuados y categorías `group-title`. Comprueba que aparece el número correcto de canales y que la búsqueda y los filtros funcionan.
2. Reproduce un canal autorizado, luego detén o cambia el canal. Comprueba que no haya dos reproducciones activas. Verifica que la UI no muestre la URL ni sus parámetros de consulta.
3. Importa una lista remota HTTPS. Para una dirección HTTP, verifica que se presenta el aviso antes de descargar. Si el proveedor ofrece HTTPS, úsalo.
4. Prueba una dirección que responde con redirección: la importación debe rechazarse sin seguirla. Usa la dirección final directamente si está autorizada.
5. Cierra y abre la aplicación. La lista y sus streams no deben persistir; se vuelven a cargar desde el archivo o URL. Los favoritos de M3U solo duran la sesión.
6. Revisa `%LocalAppData%\StreamDesk\logs` y la base SQLite local únicamente en un entorno de prueba. No deben aparecer URL de lista, query strings, credenciales ni canales M3U.

La suite automatizada cubre UTF-8, acentos, nombres/categorías, URI relativas remotas, esquema inseguro, direcciones privadas, redirecciones, límites de tamaño y reproducción por una URI validada. No verifica compatibilidad con un proveedor real ni sustituye esta prueba.
