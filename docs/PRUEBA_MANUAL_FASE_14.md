# Prueba manual — Fase 14: experiencia simplificada

**Estado:** PENDIENTE_DE_VALIDACION. Esta guía no se considera ejecutada por las pruebas automatizadas.

## Preparación

1. Ejecutar StreamDesk en Windows con una cuenta IPTV autorizada agregada desde la interfaz. No copiar credenciales, URL autenticadas ni capturas que las muestren a informes o repositorios.
2. Para persistencia, favoritos y sincronización, usar contenido de prueba o elementos que se puedan identificar sin ambigüedad.
3. Repetir las comprobaciones de favoritos después de cerrar y volver a abrir la aplicación y con al menos dos cuentas guardadas.

## TV en vivo

- Conectar una cuenta con un catálogo amplio. Comprobar que el panel de canales permite leer nombres completos con tooltip y que desplazarse por miles de resultados no congela la ventana.
- Buscar por nombre, cambiar categoría y limpiar ambos filtros. Verificar que los resultados y el conteo coinciden.
- Estrechar y ampliar el panel con el separador. Confirmar que conserva límites razonables y que no aparece scroll horizontal.
- Elegir un canal y confirmar que empieza una sola reproducción, el canal queda resaltado y el favorito se puede activar/desactivar sin perder selección.
- Cambiar repetidamente de canal y comprobar que solo hay una sesión activa.
- Sin canal seleccionado, confirmar que se ve «Selecciona un canal para comenzar a ver televisión» y que no aparecen controles activos.
- Confirmar que el indicador «EN VIVO» solo aparece durante reproducción de un canal. Revisar programa actual, horario, progreso y programa siguiente únicamente si la cuenta entrega EPG; sin EPG debe aparecer el aviso discreto correspondiente.
- Probar pantalla completa, restaurar la ventana y PiP desde los controles del reproductor. Confirmar que PiP sigue disponible allí.
- Con una lista M3U guardada como cuenta, marcar un canal favorito, cerrar la aplicación, reabrir la lista y verificar que conserva el favorito. Una lista importada sin perfil debe conservar el comportamiento temporal de sesión.

## Favoritos

- Agregar al menos un canal, una película y una serie desde sus respectivas pantallas; confirmar el aviso temporal y que no queda una barra fija reservando espacio.
- Abrir Favoritos y revisar Todos, Canales, Películas y Series. Cada pestaña debe mostrar solo su tipo y debe usar la tarjeta visual compartida del catálogo para películas y series.
- Quitar cada tipo de favorito desde la biblioteca y comprobar que desaparece inmediatamente y también deja de estar marcado en su catálogo.
- Cerrar y abrir la aplicación. Confirmar persistencia de cada tipo.
- Cambiar entre dos cuentas guardadas y confirmar que ninguna lista mezcla favoritos de la otra cuenta.
- Actualizar el catálogo conservando el ID del contenido y confirmar que el favorito permanece. Comprobar el estado informado si un ID dejó de existir.
- Exportar e importar la sincronización cifrada con datos ficticios y verificar que se recuperan favoritos por perfil, sin mezclar cuentas.
- Con una cuenta sin favoritos, confirmar el texto «Todavía no tienes favoritos», la explicación y la acción para explorar contenido, sin reproductor vacío.

## Selector y administración de cuentas

- Abrir el selector visual de cuenta, identificar avatar/iniciales y cuenta activa, cambiar de cuenta y esperar a que se cargue el catálogo correcto.
- Repetir abrir, recorrer y elegir cuenta usando Tab, flechas, Enter y Escape. Confirmar foco visible y que Escape no cambia accidentalmente la cuenta.
- Abrir opciones de cuenta, ir a administrar cuentas y volver a Ajustes. Verificar que el selector utiliza terminología de cuenta IPTV y no crea perfiles de usuario independientes.
- Editar nombre o dirección de una cuenta y volver a abrir Ajustes. Confirmar que la credencial existente se conserva si el campo secreto queda vacío y que nunca se muestra en texto plano.
- Comprobar conexión y cancelar o completar la operación. Eliminar solo una cuenta de prueba tras leer la confirmación que explica qué datos locales se eliminarán.
- Confirmar que la cabecera no incluye botones redundantes de Canales, Películas/Series o Flotante.

## Ajustes

- Recorrer Mi cuenta, Reproducción, Apariencia, Control parental, Datos y respaldo y Opciones avanzadas con ratón y teclado.
- Cambiar tema, idioma/tamaño de interfaz, preferencias de audio/subtítulos si están disponibles, pantalla completa y reproducción del último canal. Reiniciar y comprobar persistencia.
- Cambiar la estabilidad mediante Automática (recomendada), Menor retraso y Mayor estabilidad. Confirmar que los valores son los mismos valores subyacentes admitidos y que la descripción explica el efecto sin alterar la semántica.
- Activar/desactivar el control parental, configurar PIN de prueba, bloquear y desbloquear contenido. Confirmar que nunca se revela el PIN.
- Actualizar el catálogo y revisar cantidad, estado y fecha de sincronización. Comprobar exportar e importar respaldo con datos de prueba.
- Abrir Opciones avanzadas y verificar que siguen accesibles la configuración numérica de estabilidad/búfer, HLS, modo de vídeo, amplificación, diagnóstico, conexión rápida, herramientas de soporte y opciones técnicas preexistentes.
- Confirmar que ningún campo de contraseña muestra el secreto guardado y que acciones destructivas mantienen confirmación.

## Presentación y regresiones

- Probar tamaño de ventana pequeño (sin bajar de los mínimos de la app), mediano y maximizado. Repetir con escalado de Windows disponible (por ejemplo, 100 %, 125 % y 150 %).
- Recorrer las siete secciones laterales con ratón y teclado; confirmar que Recientes conserva una ruta accesible.
- Comprobar que listas largas responden al desplazamiento, que los posters se cargan al aparecer y que no hay scroll horizontal accidental.
- Generar un aviso de éxito y uno de error. El éxito debe desaparecer solo, sin tapar controles del reproductor; el error importante debe seguir visible hasta que pueda leerse o resolverse.
- Confirmar que cambiar cuenta y filtrar/buscar durante una reproducción no abre streams duplicados ni detiene accidentalmente el reproductor.

## Registro de resultados

Registrar para cada bloque: `PASA`, `FALLA` o `NO APLICA`, versión de StreamDesk, tamaño de ventana, escalado de Windows y un resumen sin credenciales ni datos sensibles. Repetir cualquier caso fallido tras reiniciar. No marcar la fase como aprobada hasta cubrir los casos aplicables, incluida reproducción de una cuenta autorizada y las ventanas/escalas físicas disponibles.
