# F6 — estabilización y candidato autocontenido

Fecha: 2026-10-18. Se trabajó F6 después de F5 por instrucción conjunta del usuario. No se aprobaron retrospectivamente F4/F5, no se accedió a cuentas reales y no se publicó/etiquetó 1.0. Base de implementación F5: `cd8aeed`.

## Código y entrega

- `SafeFileLogger`: cola acotada a 256 entradas sin retener excepciones, escritura de fondo, cierre/flush y rotación en dos archivos de hasta 1 MiB. Elimina E/S de logging del camino UI y crecimiento automático ilimitado.
- `LocalRecoveryService`, contrato y Configuración: snapshot SQLite consistente, solo secretos DPAPI referenciados, marcador de copia terminada y limpieza al fallar; diagnóstico local con SO/runtime/arquitectura y eventos filtrados. Pruebas usan datos temporales inventados. No se exportan contraseñas ni se descifran credenciales para el diagnóstico.
- Estabilización de contenido: validación de IDs que impide persistir URLs como identificadores, limpieza de progreso obsoleto por tipo/serie, pistas vacías al sustituir/detener el medio, errores parciales de catálogo visibles y preservación de la operación vigente ante respuestas antiguas. La validación de episodios se realiza después de incorporar serie/temporada.
- `RuntimeVerifier`: genera BMP temporal, verifica píxeles del decoder nativo y libera motor/buffer/archivo. Se activa solo con `--verify-runtime`, antes de construir servicios de datos; sin perfiles ni red.
- `Build-Candidate.ps1`: Release win-x64 autocontenido, DLL/plugins separados, textos/avisos y manifiesto SHA-256 de 903 archivos. `Manage-StreamDesk.ps1` instala/actualiza/desinstala por usuario y permite rollback; rutas/links/hash/propiedad de carpeta se validan. Activación mediante reemplazo de marcador después de copiar. Los scripts antiguos son entradas de compatibilidad, sin duplicar la implementación.

**Candidato verificado:** `artifacts/candidates/StreamDesk-0.5.0-rc.1-20261018-171817`. Abrir `app/StreamDesk.Probe.Desktop.exe` o instalar con su gestor. No requiere SDK ni runtime .NET externo. `candidate.json` mantiene `ReleaseApproved=false` y revisión de licencias pendiente.

## Evidencia real

- `dotnet build StreamDesk.sln -c Release --no-restore --verbosity minimal --maxcpucount:1 -p:UseSharedCompilation=false` y `dotnet test StreamDesk.sln -c Release --no-build --no-restore --verbosity minimal --maxcpucount:1`: **128 correctos, 0 fallidos/omitidos**. Incluyen 113 regresiones previas, 10 casos F5 y 5 de estabilización/recuperación; prueba WPF ampliada de TV/VOD/EPG con datos ficticios. Compilación final sin errores/advertencias.
- Copia SQLite/DPAPI real en fixture: reinicio, descifrado bajo el mismo usuario de prueba, secreto ausente aborta sin snapshot parcial. Logger concurrente acotado y diagnóstico que descarta líneas inesperadas con texto sensible ficticio.
- `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Build-Candidate.ps1`: generación y decodificación nativa con exit 0. Archivos locales .db/.dpapi/.env/.log en paquete: 0.
- `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Test-Candidate.ps1 -Candidate artifacts/candidates/StreamDesk-0.5.0-rc.1-20261018-171817`: instalación real del candidato, actualización/rollback/desinstalación con archivos ficticios y rechazo de traversal de manifiesto. Datos separados conservados. Registro HKCU/acceso directo se omiten expresamente en automatización; quedan pendientes físicos.
- Ejecutable instalado con entorno explícito, PATH limitado a System32, DOTNET_ROOT inexistente y lookup múltiple desactivado: decodificó BMP correctamente. Esto comprueba el payload autocontenido, no sustituye una PC Windows limpia. Evidencia en `artifacts/i-3b750b31/evidence.json`.
- Medición **de ese único fixture**, incluyendo arranque/decodificación/cierre: 2.650 ms; pico RAM muestreado 94,63 MiB; CPU muestreada 656,25 ms; 77 muestras. GPU NO MEDIDA. No son latencias de canal, benchmark IPTV, consumo estable ni comparación válida con F0/F3; no había baseline real comparable. `Measure-Playback.ps1` permanece disponible para la sesión autorizada real.
- `dotnet list StreamDesk.sln package --vulnerable --include-transitive --no-restore --format json`: sin avisos conocidos reportados en los seis proyectos al consultar NuGet. JSON en `artifacts/f6-package-vulnerabilities.json`. No certifica todos los binarios/plugins.
- Inventario de **323 DLL nativas** con hash/versión. Se preservaron nuspec/avisos disponibles, licencias de packs .NET y textos GPLv2/LGPLv2.1 oficiales de VLC. Falta mapear cada componente a sus avisos/fuentes correspondientes; no se declara cumplimiento completo.

Incidencias de verificación: el instalador inicial trataba el array JSON como un solo objeto en Windows PowerShell 5; se corrigió. El reemplazo con backup nulo fallaba por conversión de PowerShell y se cambió por archivo anterior nombrado. El sandbox denegó File.Replace; con autorización fuera de ese límite, las pruebas de marcador/rollback pasaron. Una ruta de prueba excesivamente larga superó el límite heredado de PowerShell 5; se acortó el directorio de fixture. El directorio predeterminado por usuario es corto; rutas extraordinariamente largas siguen siendo una limitación documentada. No se modificó el registro ni se instaló sobre datos reales durante estas pruebas.

## Checklist

| ID | Estado | Evidencia / pendiente |
|---|---|---|
| F6-T01 | PARCIAL | Logger/cachés/virtualización acotados y medición local anterior; faltan CPU/RAM/GPU y perfiles IPTV comparables. |
| F6-T02 | PARCIAL | Regresiones de apertura/cierre/reintentos/cancelación/suspensión simulada pasan; Wi-Fi, cambio de red y suspensión física pendientes. |
| F6-T03 | COMPLETADA | Build/test Release, 128 tests y UI WPF TV/VOD/guía con fixtures. |
| F6-T04 | COMPLETADA | Comprobaciones específicas DPAPI/BD/logs/diagnóstico/XML/URI/redirecciones de metadatos pasan; lectura de fuente enfocada, sin afirmar pentest exhaustivo. |
| F6-T05 | PARCIAL | NuGet sin avisos reportados, inventario nativo y textos/avisos conservados; mapeo completo de fuentes/licencias nativas pendiente. |
| F6-T06 | COMPLETADA | Publish local win-x64 autocontenido, 323 DLL nativas y decodificación BMP del paquete instalado. |
| F6-T07 | COMPLETADA | Gestor per-user con hashes, instalación real y update/rollback/uninstall de fixtures; registro/acceso directo físicos pendientes en T08. |
| F6-T08 | PENDIENTE | Solo este entorno Windows disponible; protocolo para trabajo/casa con autorización de TI. |
| F6-T09 | PARCIAL | Autocontenido probado sin SDK en PATH/DOTNET_ROOT; falta equipo limpio sin herramientas instaladas. |
| F6-T10 | COMPLETADA | Actualización manual, snapshot protegido, rollback de binarios y estrategia de restauración de datos/diagnóstico documentada. |
| F6-T11 | COMPLETADA | `MANUAL_USO.md` incluido, notas y problemas conocidos, prueba manual F6. |
| F6-T12 | BLOQUEADA | No se marca ni etiqueta 1.0 mientras falten criterios críticos reales/licencias. |

## Aceptación

| Criterio | Resultado |
|---|---|
| Suite Release y recorridos UI | PASA con fixtures; no acredita proveedor real. |
| Instalación/copia/activación y runtime nativo local | PASA; update/rollback/uninstall probados con archivos ficticios. |
| Privacidad específica revisada/probada | PASA en alcance indicado; no certificación global de seguridad. |
| Dos equipos, Windows limpio, IPTV y DPI/red físicos | NO VERIFICADO. |
| Licencias/fuentes correspondientes completas | NO VERIFICADO. |
| Liberar 1.0 | NO PASA hasta cerrar esos criterios. |

**Gate F6: PENDIENTE_DE_VALIDACION.** 6 COMPLETADA, 4 PARCIAL, 1 PENDIENTE y 1 BLOQUEADA. No se inicia F7. Siguiente paso: ejecutar los protocolos F4/F5/F6, recopilar resultados no sensibles y completar la revisión nativa. Fuentes de licencias preservadas: [COPYING VLC](https://github.com/videolan/vlc/blob/3.0.x/COPYING) y [COPYING.LIB VLC](https://github.com/videolan/vlc/blob/3.0.x/COPYING.LIB).
