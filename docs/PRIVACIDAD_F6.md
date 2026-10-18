# Revisión enfocada de privacidad F6

Alcance: fuente y regresiones concretas de esta entrega, con orientación `security-audit` en modo guía. No se ejecutó el workflow de auditoría exhaustiva ni un pentest. No se accedió a identidades reales, portapapeles, dumps, servidores del usuario ni infraestructura compartida. La validación de desarrollo usa fixtures ficticios conforme a AGENTS.md; no se afirma un sandbox aislado completo para pruebas de seguridad.

| Límite revisado | Evidencia |
|---|---|
| Credenciales UI → disco | DPAPI CurrentUser, buffers sensibles borrados, temporales atómicos; tests reales de cifrado y copia protegida con secretos ficticios. |
| Metadatos → SQLite | Modelos sin stream URL; sanitización de texto/posters, IDs válidos y progreso compuesto; rechazo de URL autenticada ficticia como ID. Catálogos y progreso separados por perfil/tipo. |
| XML → archivo/red externa | DTD prohibido, XmlResolver null, 16 MiB máximo y cancelación; fixture de entidad externa rechazado sin lectura del recurso. |
| HTTP/API → destino/log | HttpClientFactory sin loggers de URL, cookies ni redirección automática; pruebas previas de redirección y ausencia de URL autenticada en logs continúan pasando. No se promete que todos los CDN/redireccionamientos del motor nativo se hayan probado. |
| Errores/VLC → logs/diagnóstico | Mensajes/stack/URLs no se retienen; solo evento/tipo saneados. Cola y archivos acotados. Diagnóstico descarta líneas que no cumplen formato; test introduce secreto ficticio y comprueba su ausencia. |
| Datos → candidato | Publish recopila binarios/avisos, no carpetas personales; 903 entradas inventariadas, 0 .db/.dpapi/.env/.log. No se lee la carpeta real del usuario en verify-runtime. |
| Paquete → instalación | Validación de rutas, rechazo de traversal/enlaces y hashes antes de activar. Test de manifiesto intenta salir al archivo ficticio adyacente, se rechaza y el contenido se conserva. Hashes no autentican el editor. |
| Copia/rollback → datos | Snapshot consistente, secretos referenciados cifrados y copia incompleta abortada; datos personales separados del directorio de instalación. Uninstall conserva datos; restauración se realiza con app cerrada y copia previa. |

Se corrigieron defectos de validación/ciclo de vida encontrados durante el trabajo y se conserva evidencia antes/después de los fallos del instalador. No se asignan vulnerabilidades confirmadas ni severidades a hipótesis sin un límite de confianza y efecto demostrado. Quedan sin certificar políticas de Windows/TI, dispositivos físicos, comportamiento de terceros/proveedor y la revisión de cada plugin nativo. La consulta NuGet sin avisos conocidos no equivale a ausencia de vulnerabilidades.
