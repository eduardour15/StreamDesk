# Validación manual — Fase 1

Ejecutar en Windows antes de aprobar F1. No incluir credenciales reales en capturas, logs ni el informe.

1. Ejecuta `dotnet run --project src/StreamDesk.Probe.Desktop/StreamDesk.Probe.Desktop.csproj` con la red desconectada. Confirma que abre en Inicio y cambia entre Inicio, TV en vivo, Favoritos, Recientes y Configuración sin bloquearse.
2. En Configuración, crea un perfil de prueba con una URL ficticia como `https://provider.invalid`, usuario `user-fixture` y contraseña `password-fixture`. Confirma el mensaje de guardado y que la contraseña queda vacía en pantalla.
3. Confirma que el perfil aparece en la lista. Cierra y vuelve a abrir StreamDesk para validar persistencia; luego elimínalo desde Configuración y confirma que desaparece.
4. Revisa `%LocalAppData%\StreamDesk\streamdesk.db`: contiene metadatos, pero no `user-fixture` ni `password-fixture`. El archivo protegido asociado está bajo `%LocalAppData%\StreamDesk\secrets\` y no debe contener las cadenas legibles. El log bajo `%LocalAppData%\StreamDesk\logs\` tampoco debe incluirlas.
5. Con una cuenta y un canal autorizados que ya uses, conecta desde TV en vivo, carga el catálogo, reproduce, cambia de canal, detén y cierra. Confirma imagen/sonido y liberación del stream para descartar regresión de F0.

Resultado a registrar en `docs/ESTADO_FASES.md`: sistema/versión de Windows, pasos completados, errores visibles y confirmación de que las credenciales no aparecen en SQLite ni logs. No registrar URL autenticada, usuario o contraseña.
