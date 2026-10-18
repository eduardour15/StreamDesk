# ADR-005: contenido condicional, EPG y progreso

- Fecha: 2026-10-18
- Estado: aceptada para implementación F5 y estabilización F6 solicitadas expresamente.

El usuario solicita trabajar F5 y F6 juntas. Se ejecutan en ese orden sin aprobar los gates físicos pendientes de F4 ni publicar 1.0. No se dispone de una verificación real de capacidades de su cuenta; los tests ficticios no sustituyen esa evidencia.

IContentProvider extiende capacidades opcionales del adaptador Xtream sin imponerlas a otros IIptvProvider. Consultar películas/series es una acción explícita; listas vacías no se convierten en contenido inventado. Los detalles y episodios se consultan al seleccionar. EPG usa get_short_epg por canal y admite importación XMLTV local autorizada. Se preservan IDs remotos y claves compuestas por serie/temporada/episodio. Las URLs de medios se construyen solo en memoria con rutas movie/series y extensiones declaradas permitidas; se ignoran direct_source y redirecciones de metadatos.

Las fechas Unix de EPG son autoritativas; fechas Xtream sin offset se omiten para no adivinar zona/DST. XMLTV acepta fecha completa con offset numérico o UTC si falta, según su DTD; DTD/entidades externas se bloquean. SQLite v4 guarda metadatos sanitizados, EPG UTC y progreso, nunca URLs autenticadas. Caché: 7 días, 12 catálogos por perfil, 64 MiB globales; EPG 5 minutos y 128 canales por perfil; progreso 200 elementos por perfil. Posters reutilizan LRU RAM de 128/512 KiB y no se guardan en disco.

Un mismo PlaybackService/LibVLC sirve TV/VOD. Opciones por sesión distinguen VOD, posición inicial y fin natural: finalizar un archivo no dispara reconexión. Lecturas, seek y selección de pistas se serializan con apertura/cierre; seek se ofrece solo si el motor lo declara. El progreso se captura periódicamente y antes de detener/cambiar/cerrar; fin natural marca completado. Siguiente episodio requiere una acción del usuario, sin conexiones especulativas ni autoplay nuevo.

Fuentes de formato, sin afirmar compatibilidad universal: [acciones Player API](https://github.com/worldofiptvcom/xtream-codes-api/blob/master/docs/player-api/overview.md), [DTD XMLTV y reglas de fecha](https://github.com/XMLTV/xmltv/blob/master/xmltv.dtd). La API de tracks/seek se coteja con documentación XML del paquete LibVLCSharp 3.10.2 instalado.
