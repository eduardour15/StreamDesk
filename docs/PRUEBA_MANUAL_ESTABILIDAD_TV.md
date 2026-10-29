# Validación de estabilidad de TV — 2026-10-24

Estado: **PENDIENTE_DE_VALIDACION** con proveedor y hardware reales. Relacionado con F3-T05/T07/T10/T11 y ADR-013. No registrar cuentas, URLs, capturas con datos personales ni nombres del proveedor.

## Qué cambió

VLC puede descartar imágenes atrasadas y seleccionar automáticamente el decodificador compatible. El detector de estancamiento sigue leyendo estadísticas durante el búfer y no acepta avisos repetidos como evidencia de avance. El aumento de caché por reconexión se conserva durante la sesión, incluso después de restablecer el presupuesto de reintentos.

**Incremento ADR-014:** «Priorizar fluidez» está activado por defecto y usa al menos 5000 ms de caché en HTTP de TV. Al conectar prefiere HLS si la cuenta lo declara. Para el demux adaptativo se solicita margen de 20–30 s y reserva máxima de 30 s, limitada por lo que publique la señal. «Automática» adapta entre variantes HLS; «Ahorro» solicita la menor variante. En una señal única no se inventa una calidad inferior. No se cambia entre canales por parecido de nombre.

## Comparación reproducible

1. Detener y cerrar la instancia anterior antes de abrir el ejecutable actualizado. No abrir ambos reproductores simultáneamente. Este incremento se compila en `artifacts/fluidez/bin/Debug/net10.0-windows/StreamDesk.Probe.Desktop.exe`, junto a sus DLL y runtime nativo; no mover el EXE solo. Se eligió esa salida para conservar la instancia Debug que estaba abierta durante el desarrollo. No instala ni sustituye la versión de Program Files.
2. Conservar las preferencias y elegir desde la UI una cuenta y una señal autorizadas. La lectura inicial de Ajustes en esta sesión fue **3500 ms / automático**. Anotar hardware, resolución/códec si se conocen, conexión usada y valores elegidos, sin datos de cuenta.
3. En Ajustes, mantener «Priorizar fluidez» y calidad «Automática», pulsar «Aplicar al canal actual» y comparar 10 minutos en la señal problemática. Si hay variantes HLS, repetir con «Ahorro». Registrar bloqueos perceptibles, tiempo detenido y demora. El número de búfer guardado se conserva, pero con fluidez activa el efectivo mínimo es 5000 ms. Para comparar la base original de 3500 ms hay que desactivar fluidez y aplicar; después se puede comparar con 5000/8000 ms, siempre de forma secuencial. Aplicar reabre la misma entrada; no identifica ni sustituye señales equivalentes.
4. Mantener automático inicialmente. Si la imagen se bloquea mientras sigue el sonido o aparecen defectos gráficos, repetir el mismo contenido por software tras reabrirlo. Comparar CPU y fluidez; no concluir que software es mejor para todos los equipos.
5. Probar TS y HLS únicamente si el proveedor los declara. Elegir el más estable según la medición, sin asumir que alguno siempre es superior. La conmutación acotada existente no cambia servidores ni evita límites de cuenta.
6. Probar un corte breve de red, su recuperación y Detener durante la espera. El detector espera `max(4 s, 2 × búfer efectivo, margen HLS reconocido) + 5 s`, más muestreo, reapertura y backoff. Con fluidez desactivada y 3500 ms son aproximadamente 7 + 5 s. Con fluidez activa y 5000 ms efectivos son 10 + 5 s en TS, o 20 + 5 s en una fuente `.m3u8`. El margen puede crecer hasta 30 s en reintentos. El tiempo de apertura/búfer nativo en HLS permite al menos margen + 5 s. Es el coste explícito de permitir rellenar la reserva; no se promete recuperación inmediata.
7. Repetir cortes después de al menos 60 s de salida continua. Debe recuperarse el presupuesto de dos reintentos, manteniendo la caché aumentada por sesión. Una caída persistente debe agotar el presupuesto, ofrecer recuperación manual y permitir Detener. No provocar restricciones del proveedor para probar errores de cuenta.
8. Cambiar 20 veces de canal, pausar/reanudar y cerrar mientras carga. Verificar ausencia de audio residual y streams simultáneos. Probar también película, final natural de episodio, pantalla completa y PiP como regresión.
9. Verificar que Ajustes conserva fluidez/calidad al reiniciar, que «Aplicar» usa el canal actual y que una cuenta solo-TS no intenta HLS. Para fuentes sin varias variantes, Ahorro no debe prometer resolución inferior ni iniciar otro canal. La calidad Automática depende del manifiesto real; TS/HLS no equivalen a SD/HD.
10. Verificar el diagnóstico en Ajustes y en los controles visibles. Recepción es el delta del contador de lectura de VLC; fps se calcula con los contadores de salida/decodificación disponibles. Algunos módulos publican datos parciales o ninguno: valores ausentes no son cero ni prueban una avería. Las recargas no incluyen apertura inicial, pausa ni seek y se reinician al cambiar de sesión. Comparar con lo observado, sin atribuir automáticamente la causa al Wi-Fi o al proveedor.

Para recoger CPU/RAM/GPU de una sesión, desde la raíz del repositorio con una sola instancia abierta:

```powershell
.\scripts\Measure-Playback.ps1 -DurationSeconds 600 -IntervalSeconds 5
```

Los archivos quedan en `artifacts/metrics`. La herramienta no cuenta cortes ni identifica códec/primer fotograma; hay que anotarlos aparte. Los contadores GPU pueden no estar disponibles. No atribuir a estas mediciones una causa de red sin comprobarla.

## Registro sin secretos

| Caso | Búfer / modo | Duración | Bloqueos / segundos detenido | Apertura | CPU/RAM/GPU | Resultado |
|---|---|---|---|---|---|---|
| Señal A, configuración inicial | 3500 / automático | | | | | PENDIENTE |
| Señal A, más caché | 5000 / automático | | | | | PENDIENTE |
| Señal A, más caché si procede | 8000 / automático | | | | | PENDIENTE |
| Misma señal por software si procede | valor elegido / software | | | | | PENDIENTE |
| Cortes, recuperación y Detener | | | | | | PENDIENTE |
| 20 cambios y cierre | | | | | | PENDIENTE |

Aceptar el incremento cuando se demuestre mejora o ausencia de regresión en la señal problemática, recuperación acotada y un solo stream activo. Los tests controlados y AVI local no sustituyen esta prueba. No se inicia otra fase ni se aprueba una release mediante este protocolo.
