# ADR-004: biblioteca local y controles MVP

- Fecha: 2026-10-18
- Estado: aceptada para implementación F4.

El usuario solicita cerrar F3 y avanzar a F4. Se registra esa decisión sin atribuir resultados a las mediciones IPTV ni a las pruebas físicas pendientes. F4 puede evaluarse con datos ficticios; el gate técnico conserva esas limitaciones.

SQLite v3 mantiene favoritos e historial por perfil/tipo/ID remoto. El historial registra únicamente el primer estado Playing de una solicitud, conserva los últimos 50 canales distintos por tipo y elimina referencias a canales retirados. La migración consolida duplicados y conserva la fecha más reciente. No se guarda ninguna URL de reproducción. La preferencia de autoplay es por perfil, apagada por defecto; solo se evalúa una vez al arrancar, nunca al cambiar perfil o reanudar Windows. Se recuerda el último perfil seleccionado. La reproducción manual autentica con DPAPI cuando sea necesario.

Una misma vista de TV sirve para catálogo, favoritos y recientes, sin mover ni duplicar VideoView. Los controles se alojan en una franja WPF adyacente al HWND de video: no dependen de un overlay para ser operables. Autohide colapsa esa franja solo durante reproducción, sin foco ni mouse sobre controles; movimiento o teclado la recupera. Los controles no se ocultan durante carga/error. Pantalla completa oculta navegación y filtros y conserva el mismo reproductor. Se descarta depender del overlay no verificado en monitores físicos.

No se añaden paquetes. El candidato de prueba es una carpeta win-x64 reproducible con scripts de instalación por usuario y avisos conservados; no se publica ni etiqueta una release. La redistribución externa y la aceptación en otro equipo siguen requiriendo revisión de avisos nativos y pruebas físicas.

Referencias primarias: [VideoHwndHost de LibVLCSharp](https://github.com/videolan/libvlcsharp/blob/3.x/src/LibVLCSharp.WPF/VideoHwndHost.cs), [VideoView y su asociación al HWND](https://github.com/videolan/libvlcsharp/blob/3.x/src/LibVLCSharp.WPF/VideoView.cs), [plantilla ComboBox de WPF](https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/Themes/XAML/ComboBox.xaml). El selector de plantilla de ítem se conserva en la presentación de selección para respetar DisplayMemberPath; la revisión visual detectó y corrigió la presentación inicial del ToString de los modelos.
