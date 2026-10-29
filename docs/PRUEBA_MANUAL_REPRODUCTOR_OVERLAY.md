# Validación del reproductor superpuesto — 2026-10-24

Usar contenido autorizado configurado localmente; no adjuntar credenciales, URLs autenticadas ni capturas con datos de cuenta. Esta guía no acredita por sí sola una prueba.

1. Abrir TV en vivo y reproducir un canal. Entrar en pantalla completa con F. Comprobar que la superficie llega a los bordes del monitor, con bandas negras solo por la relación de aspecto. No debe existir una franja de aplicación bajo el vídeo.
2. Mover el ratón sobre el centro del vídeo, lejos de la barra. Dejarlo quieto 2,5 segundos: controles, estado y cursor deben desaparecer, sin cambio de tamaño del vídeo. Moverlo de nuevo: deben reaparecer inmediatamente. Comprobar también un clic para pausa y doble clic para fullscreen sin pausa accidental.
3. Mantener el ratón sobre un botón y un menú de audio/subtítulos/formato abierto; arrastrar volumen/progreso; navegar con Tab. Los controles y el cursor deben permanecer accesibles mientras se interactúa. Volver al área del vídeo y repetir el autoocultamiento.
4. Probar Espacio/K, M, ↑/↓ y F/Esc. Volumen por teclado: pasos de 5 y límite 0–100. En TV sin timeline, ←/→ y J/L no deben cambiar de canal, abrir otro stream ni reconectar. Shift+P/N cambia anterior/siguiente.
5. Reproducir una película o episodio con `CanSeek`: ←/→ salta 5 s; J/L salta 10 s; los saltos se limitan al inicio/final. Comprobar que los sliders y campos de búsqueda mantienen su teclado y que los atajos no actúan sobre diálogos.
6. Repetir fullscreen/ventana al menos tres veces. Entrar/salir de PiP con P/Esc y sus botones; probar F desde PiP. Verificar posición, tamaño, estado maximizado y fijación previa, continuidad del medio y un solo stream.
7. Comprobar pausa, carga/búfer, error y detener: controles y mensajes accesibles. Con el cursor oculto, salir del vídeo, alternar de aplicación, minimizar, detener y cerrar; el cursor debe quedar visible fuera del reproductor.
8. Repetir a 100/125/150/200 % y, si está disponible, en otro monitor con DPI distinto. Registrar cada combinación realmente comprobada. Los escenarios no realizados quedan `PENDIENTE_DE_VALIDACION`.
9. Al abrir la aplicación en Inicio no debe aparecer el icono ni la barra del reproductor. Durante una reproducción, navegar a Inicio, Ajustes y los catálogos de Películas/Series; los controles superpuestos deben desaparecer completamente. Volver a TV: deben reaparecer dentro del reproductor, con el mismo medio y sin una nueva apertura. Repetir después de minimizar/restaurar.

Pruebas automatizadas desde la raíz:

```powershell
dotnet build StreamDesk.sln
dotnet test StreamDesk.sln
```

Opcional, en un escritorio interactivo de Windows y con el usuario sin usar el ratón durante la prueba:

```powershell
$env:STREAMDESK_NATIVE_INPUT = '1'
dotnet test StreamDesk.sln --no-build --no-restore --filter FullyQualifiedName~WpfCatalogVirtualizes --maxcpucount:1
Remove-Item Env:STREAMDESK_NATIVE_INPUT
```

Esta prueba utiliza un vídeo AVI local generado (16:9, 2 fps, sin audio), restaura el puntero y escribe `artifacts/player-overlay-desktop-evidence.json` y capturas `player-overlay-visible.png`/`player-overlay-hidden.png`. La suite normal escribe `player-overlay-evidence.json` por separado, sin sobrescribir la evidencia física. No utiliza datos de proveedor ni verifica vídeo IPTV real.
