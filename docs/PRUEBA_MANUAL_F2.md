# Verificación de F2

## Prueba automatizada reproducible en Windows

Desde la raíz, con .NET 10 SDK y Windows Desktop Runtime:

```powershell
dotnet build StreamDesk.sln
dotnet test StreamDesk.sln
dotnet test StreamDesk.sln --filter FullyQualifiedName~CatalogUiTests
```

La prueba de interfaz construye una ventana WPF fuera de pantalla con perfiles y 5.000 canales ficticios en una base temporal. No inicia los servicios de producción ni lee las credenciales del usuario. Comprueba filas materializadas, desplazamiento, acentos, categorías vacías, favoritos, aislamiento de perfiles, refresco fallido, caché de logos y cierre. Genera `artifacts/f2-catalog.png` y `artifacts/f2-ui-evidence.json`; ambos quedan fuera de Git.

## Proveedor real autorizado — opcional, PENDIENTE_DE_VALIDACION

1. Ejecuta `dotnet run --project src/StreamDesk.Probe.Desktop/StreamDesk.Probe.Desktop.csproj`.
2. En Configuración crea un perfil con la URL base y credenciales introducidas localmente. Si hay otro servidor, crea otro perfil; no supongas que es un respaldo equivalente. Si utiliza HTTP, consulta al proveedor si dispone de HTTPS.
3. En TV en vivo selecciona el perfil y pulsa Actualizar catálogo. Confirma categorías, canales y fecha de actualización. Busca por nombre, prueba una palabra con acentos y filtra por categoría. El filtro de favoritos solo incluye los favoritos ya guardados; añadir favoritos corresponde a F4.
4. Desconecta Internet e intenta actualizar. Debe aparecer un error con opción de reintentar y mantenerse el catálogo anterior. Cierra y abre la app sin red: el perfil debe mostrar los mismos metadatos y fecha, sin prometer video offline.
5. Cambia entre perfiles y confirma que cada servidor conserva su catálogo. La app no debe probar otro servidor automáticamente.
6. Con red disponible, conecta un perfil, selecciona un canal autorizado y reproduce/detén/cierra para revalidar el flujo de F0/F1. No se comparte el resultado con credenciales, URL autenticada ni capturas de campos sensibles.

Registrar solo fecha, sistema/escala de Windows, resultado y defectos en `docs/ESTADO_FASES.md`. No se han medido aquí GPU/CPU de video ni DPI/múltiples monitores; esas comprobaciones siguen en las tareas correspondientes del ROADMAP.
