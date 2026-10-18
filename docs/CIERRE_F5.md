# F5 — EPG y contenido condicional

Fecha: 2026-10-18. Solicitud: trabajar F5 y F6. F4 sigue pendiente de pruebas físicas; se avanza por esa instrucción expresa, sin falsificar aprobaciones. No se usaron cuentas reales ni se comprobó la disponibilidad de EPG/VOD en el proveedor del usuario.

Implementación: `ContentModels.cs`, `ContentContracts.cs`, `XtreamApiClient.Content.cs`, `XmlTvReader.cs`, `SqliteContentStore.cs` y migración v4; `PlaybackService.Media.cs`/backend con posición, seek y pistas; `MainViewModel.Content.cs`/`.Epg.cs` y ventana WPF reutilizan un único VideoView y motor. La consulta de contenido es explícita, las vistas se habilitan con datos recibidos y los enlaces se construyen solo en memoria. No se usan direct_source arbitrarios ni se inventan temporadas/enlaces.

EPG usa Unix UTC; fechas Xtream sin offset verificable se omiten. XMLTV local asocia EpgChannelId, bloquea DTD/entidades y convierte offsets incluidos cambios DST. UI muestra ahora/siguiente en zona Windows, actualización y ausencia de datos. Caché EPG 5 min/128 canales; metadatos 7 días/12 catálogos por perfil/64 MiB globales; posters reutilizan LRU RAM de 128 y 512 KiB por imagen, sin archivos de carátulas. Progreso acotado a 200 elementos por perfil, claves por tipo y serie/temporada/episodio; solo se guarda tras Playing. Detener/cambiar/cerrar captura posición; fin natural marca completado y no reconecta. El siguiente episodio requiere acción del usuario. Seek y pistas se validan/serializan con el medio activo.

## Checklist y evidencia

| ID | Estado | Resultado |
|---|---|---|
| F5-T01 | PARCIAL | Formatos y UTC/DST investigados y probados; calidad/disponibilidad de EPG real NO VERIFICADA. |
| F5-T02 | COMPLETADA | EPG actual/siguiente, actualización/caché, XMLTV y estados sin datos; fixtures UTC/DST/entidades inválidas. |
| F5-T03 | COMPLETADA | Categorías, búsqueda normalizada, posters diferidos y 5.000 películas virtualizadas en WPF. |
| F5-T04 | COMPLETADA | Ficha opcional, reproducción única, seek permitido y progreso/reanudación por perfil; contratos y flujo WPF probados. |
| F5-T05 | COMPLETADA | Series/temporadas/episodios, duplicados compuestos, temporada vacía y anterior/siguiente manual. |
| F5-T06 | COMPLETADA | Tracks declaradas por el motor, validación de IDs y mensajes sin pistas; integración nativa compilada, flujo UI probado con tracks ficticias. |
| F5-T07 | COMPLETADA | Continuar viendo, fechas/posición/tipo/ID sin enlaces autenticados, fin natural y aislamiento de proveedor. |
| F5-T08 | COMPLETADA | Límites/TTL, limpieza sin modificar favoritos de TV; tests de expiración, filas acotadas e independencia. |
| F5-T09 | COMPLETADA | 10 tests nuevos de datos/progreso/motor; UI ampliada con 5.000 películas, series vacías, reinicio/cambio de perfil y regresión completa de TV. |

Verificación F5: `dotnet build StreamDesk.sln --no-restore --verbosity minimal --maxcpucount:1 -p:UseSharedCompilation=false` y `dotnet test StreamDesk.sln --no-build --no-restore --verbosity minimal --maxcpucount:1`: compilación sin errores/advertencias, 123 tests correctos. Se amplió la prueba WPF existente; captura `artifacts/f5-content.png` inspeccionada con datos ficticios. La primera ejecución detectó cuatro regresiones al llamar playback desde Inicio: se corrigió el enrutamiento TV/VOD. Otra ejecución detectó selección antigua al cambiar película→serie; se limpian selección/filtros y el test espera el tipo de catálogo correcto. La suite completa pasó después de corregirlos.

Aceptación técnica de catálogo/EPG/progreso/ausencia de capacidades: PASA con fixtures y WPF real. EPG, audio/subtítulos y VOD del proveedor real: NO VERIFICADO. Rendimiento multimedia/DPI físico: NO VERIFICADO. Gate F5: **PENDIENTE_DE_VALIDACION**, 8 completas/1 parcial. Se trabaja F6 por la solicitud conjunta, sin declarar una release 1.0 ni aprobar retroactivamente F4/F5.

Fuentes primarias consultadas: [acciones API de referencia](https://github.com/worldofiptvcom/xtream-codes-api/blob/master/docs/player-api/overview.md), [fechas XMLTV](https://github.com/XMLTV/xmltv/blob/master/xmltv.dtd). Son referencias de formato; la implementación de cada proveedor puede variar.
