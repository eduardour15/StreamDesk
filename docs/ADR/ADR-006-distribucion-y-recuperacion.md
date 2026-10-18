# ADR-006: candidato autocontenido y recuperación local

Fecha: 2026-10-18. Estado: aceptada para F6; release estable no aprobada.

F6 se ejecuta por solicitud expresa junto con F5, conservando los gates reales pendientes. Se congela el contenido funcional de F5 y se corrigen fallos de ciclo de vida, límites y privacidad. La revisión de seguridad es enfocada, con orientación de security-audit y lectura de código; no se presenta como auditoría exhaustiva ni pentest. Las regresiones de desarrollo se ejecutan conforme a AGENTS.md, con fixtures ficticios. No se prueban identidades ni servicios reales del usuario.

Se genera un candidato 0.5.0-rc.1 win-x64 autocontenido, no single-file. Plugins/DLL de VLC permanecen como archivos separados. El verificador offline crea un BMP temporal, confirma píxeles decodificados y dispone recursos. El instalador PowerShell por usuario copia versiones a directorios nuevos, valida rutas y SHA-256, activa un marcador atómico y conserva la versión anterior para rollback. Uninstall exige marcador propio y conserva la carpeta de datos. Hashes detectan corrupción; no sustituyen firma/autenticación del editor. Registro/acceso directo son opcionales en pruebas para no alterar el escritorio del usuario.

Copias locales: SQLite BackupDatabase crea snapshot, se copian solo secretos DPAPI referenciados y se activa una carpeta terminada. Una copia con credenciales ausentes falla y limpia su carpeta temporal. Restauración de datos se documenta con aplicación cerrada, copia previa y compatibilidad de esquema; no hay degradación automática. Diagnóstico exporta solo entorno técnico y códigos permitidos; logging asíncrono con cola 256 y dos archivos de 1 MiB.

Las licencias NuGet no prueban el cumplimiento de cada plugin nativo. Se preservan avisos y textos GPL/LGPL, se inventarían DLL/hash/versión y se mantiene pendiente el mapeo completo de componentes/fuentes correspondientes antes de distribución externa. Ningún script de esta fase publica, sube cambios o etiqueta 1.0; el builder acepta solamente versiones prerelease.
