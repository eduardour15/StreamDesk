# F6 — matriz física y gate 1.0

Estado: PENDIENTE. Usar el candidato autocontenido y su `MANUAL_USO.md`. Solo cuentas autorizadas, un stream por defecto y equipos de trabajo con autorización de TI. No compartir credenciales, servidores ni enlaces de reproducción.

| Prueba | PC casa | PC trabajo |
|---|---|---|
| Windows soportado/x64 y hardware registrado sin identificadores de cuenta | PENDIENTE | PENDIENTE |
| Instalar sin administrador, SDK ni Visual Studio | PENDIENTE | PENDIENTE |
| Abrir desde Inicio y registro en Aplicaciones | PENDIENTE | PENDIENTE |
| Cuenta, búsqueda, favoritos, recientes y reinicio | PENDIENTE | PENDIENTE |
| IPTV: imagen/audio, formatos/códecs disponibles y 20 cambios | PENDIENTE | PENDIENTE |
| EPG/VOD/series/pistas/seek cuando la cuenta los ofrece | PENDIENTE | PENDIENTE |
| Ventana, fullscreen, 100/125/150/200%, dos monitores y teclado | PENDIENTE | PENDIENTE |
| Wi-Fi interrumpido/cambio de red/timeout, sin reintentar rechazos definitivos | PENDIENTE | PENDIENTE |
| Suspensión física, reanudación sin autoplay, cierre durante buffering | PENDIENTE | PENDIENTE |
| Medir CPU/RAM/GPU, inicio y cambio de canal con escenario comparable | PENDIENTE | PENDIENTE |
| Copia protegida/diagnóstico, actualizar, rollback y desinstalar | PENDIENTE | PENDIENTE |

Registrar versión Windows, CPU/GPU, resolución/escala, codec y condiciones de red, sin datos de la cuenta. `Measure-Playback.ps1` recoge métricas del proceso: usar igual duración/canales de prueba y estado de aceleración para comparar. No comparar los 2,65 s del fixture BMP con latencia IPTV.

Para comprobar el instalador manualmente: instalar en carpeta predeterminada, abrir/cerrar, crear una copia protegida, instalar candidato posterior con versión distinta, comprobar preferencias, revertir binarios y desinstalar. Confirmar que se conservan datos/copia y que desaparecen registro/acceso directo. La automatización usa `-SkipRegistration` para no modificar el escritorio del usuario. Evitar rutas de instalación extraordinariamente largas en Windows PowerShell 5.

Antes de 1.0: completar el mapeo de licencias/avisos/fuentes correspondientes de cada biblioteca/plugin redistribuido; el inventario y los textos GPL/LGPL no sustituyen esa revisión. Revisar defectos y gates F4/F5. Etiquetar/publicar requiere petición expresa y todos los criterios críticos verificados; los scripts actuales generan solo prereleases locales.
