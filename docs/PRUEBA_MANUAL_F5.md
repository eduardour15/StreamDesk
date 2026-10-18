# Validación real F5 — pendiente

Con una cuenta autorizada, sin compartir credenciales ni URLs:

1. TV: actualizar catálogo, reproducir y abrir «Guía · ahora y después». Comparar programa/hora con el servicio. Registrar disponibilidad y zona horaria. Datos Xtream sin timestamp verificable se omiten. Si usas XMLTV autorizado, importar archivo con ID EPG coincidente; DTD/entidades externas no están permitidos.
2. Pulsar «Consultar películas y series». Anotar si la cuenta tiene cada capacidad. Vistas sin datos permanecen deshabilitadas; un fallo conserva caché conocida y permite reintentar. No interpretar una consulta fallida como inexistencia definitiva del servicio.
3. Películas: buscar con acentos, filtrar categoría, abrir ficha sin poster/sinopsis, reproducir, cambiar posición si el medio lo permite, detener y reanudar. Cerrar/reabrir y volver desde Continuar. «Desde inicio» debe ignorar la posición previa.
4. Series: elegir temporada/episodio, probar una temporada vacía si existe y siguiente/anterior. No debe iniciar otro episodio sin acción. Al terminar un archivo no debe reconectarse al principio.
5. Abrir «Posición y pistas». Verificar audio/subtítulos solo si el medio declara alternativas. Confirmar idioma real; la presencia de un selector no prueba que el stream incluya pistas.
6. Cambiar perfil, volver a TV, fullscreen, red lenta/interrupción y cierre durante carga. Verificar un único stream, preferencias separadas y ausencia de URLs/contraseñas en errores y logs. Repetir la matriz física F4.

Resultados: EPG real PENDIENTE; películas PENDIENTE; series PENDIENTE; seek/reinicio PENDIENTE; pistas PENDIENTE; no regresión TV/fullscreen PENDIENTE. Registrar solo resultados, Windows/hardware/formato y tiempos, sin metadatos de cuenta. F6 conserva estos pendientes en su release gate.
