# ADR-002: catálogo Xtream, caché y perfiles de servidor

- **Fecha:** 2026-10-17
- **Estado:** Aceptada para F2

## Decisiones

- Cada servidor se configura como un perfil independiente. No existe alternancia automática, equivalencia asumida ni reutilización implícita de credenciales entre servidores. No se comprobó equivalencia de servidores reales; mantenerlos separados es la decisión conservadora hasta contar con evidencia explícita.
- Los perfiles guardados recuperan credenciales desde DPAPI. Una conexión temporal usa memoria y no sustituye la caché de un perfil guardado. Seleccionar un perfil carga sus metadatos locales sin autenticar ni iniciar video.
- Se incorpora [Microsoft.Extensions.Http 10.0.13](https://www.nuget.org/packages/Microsoft.Extensions.Http/10.0.13) exclusivamente para clientes HTTP nombrados. La composición del resto de servicios sigue siendo por constructores. Los clientes Xtream y logos deshabilitan loggers predeterminados mediante [RemoveAllLoggers](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.dependencyinjection.httpclientbuilderextensions.removeallloggers), cookies y redirecciones automáticas.
- Las consultas GET de Xtream tienen un límite total de 15 segundos y una consulta en curso por sesión. Hasta dos reintentos para fallos de transporte o HTTP 408/429/500/502/503/504. Se respeta Retry-After hasta 5 segundos; una espera mayor se comunica al usuario sin reintentar antes de tiempo. Los rechazos HTTP 401/403, otros errores no transitorios y JSON inválido no se reintentan.
- Se preservan IDs remotos como texto, incluidos ceros iniciales; se recortan espacios de IDs/nombres. Entre duplicados con datos válidos se conserva la primera ocurrencia; nombres repetidos con IDs distintos permanecen. Una respuesta con campos obligatorios malformados rechaza el catálogo completo y conserva la caché anterior. Logos y EPG son opcionales; URLs de logos con query, userinfo o credenciales conocidas se omiten.
- SQLite pasa a esquema v2 con claves `(ProfileId, ContentType, ProviderId)` e índices de categoría/nombre normalizado. La migración conserva perfiles, canales, favoritos e historial. La API devuelve listas completas: se elige reemplazo atómico con upsert, eliminación de IDs obsoletos y fecha UTC al confirmar. Los favoritos de IDs que permanecen se conservan.
- La E/S de SQLite se ejecuta en tareas de fondo: [sus métodos ADO.NET async son síncronos internamente](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/async). No se carga masivamente la colección de canales mediante notificaciones por fila.
- Búsqueda local con índice de nombres sin distinguir acentos/mayúsculas, cancelación y debounce de 180 ms. Listas WPF con [virtualización y reciclaje](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/optimizing-performance-controls). Logos solo para filas materializadas, con cancelación al salir de pantalla, 4 descargas simultáneas, 512 KiB por imagen, decodificación a 64×64 y LRU de 128 resultados en memoria.
- El cargador de logos filtra las firmas raster que soportan los codecs WIC integrados de WPF antes de decodificar. SVG, WebP, AVIF y respuestas HTML se tratan como metadatos opcionales ausentes y no interrumpen la navegación ni provocan excepciones de primera oportunidad por cada fila.

## Validación y límites

58 tests pasan en Windows, incluida una ventana WPF con 5.000 canales inventados, búsqueda por acentos, categorías vacías, favoritos, fallo de refresco, cambios de perfil, reciclaje y cierre del hilo de prueba. La prueba visual carga los mismos recursos que producción sin construir `App` ni abrir datos del usuario. La captura no acredita video nativo ni un proveedor real.

No se ejecutó una nueva sesión IPTV real. Esa comprobación es opcional en el prompt F2 y queda pendiente de validación; tampoco se infiere compatibilidad de otros proveedores, formatos o servidores.
