# Defectos detectados durante F4

Fecha: 2026-10-18. Severidad: S1 impide uso/seguridad, S2 afecta una tarea principal, S3 presentación.

| ID | Severidad | Hallazgo observado | Resolución y evidencia |
|---|---|---|---|
| F4-D01 | S2 | Plantilla personalizada de ComboBox mostraba ToString de Profile/XtreamCategory, incluso campos técnicos, en vez del nombre. Detectado en captura ficticia. | RESUELTO: conservar ItemTemplateSelector en ContentPresenter, respetando DisplayMemberPath. Assert WPF verifica «Todas las categorías» y ausencia del nombre técnico; captura revisada. No contenía credenciales ni URL de stream. |
| F4-D02 | S3 | Estados heredados de controles mostraban fondo claro/texto casi blanco y selección azul ajena al tema. | RESUELTO: estilos compartidos de Button/ComboBox/ListBoxItem, foco visible y contraste de tokens ≥4,5:1. Capturas revisadas. |
| F4-D03 | S2 | Reintentar dependía de SelectedChannel; filtrar hasta cero resultados eliminaba la selección y dejaba un botón de recuperación sin canal. | RESUELTO: recuperación usa el canal de la solicitud fallida. Regresión WPF filtra a cero, verifica selección nula y comprueba una nueva apertura al reintentar. |
| F4-D04 | S2 | Cambiar de perfil mientras se cargaba la biblioteca inicial podía ejecutar autoplay del nuevo perfil. Reproducido con test fallido y una apertura no deseada del backend ficticio. | RESUELTO: reservar una sola evaluación de inicio y exigir que la sesión restaurada siga vigente; respetar selección actual tras lecturas tardías. El caso aislado y la suite final de 113 pasan sin apertura al cambiar perfil. |

No hay defectos reproducidos abiertos en la suite final. Esto no equivale a completar la prueba humana. Validaciones abiertas, sin clasificarlas como bugs confirmados: DPI físicos/dos monitores, teclado/mouse físico sobre video, primera instalación en otro usuario/equipo, IPTV/audio y revisión completa de avisos de cada plugin antes de redistribuir. Protocolo: `PRUEBA_MANUAL_F4.md`.
