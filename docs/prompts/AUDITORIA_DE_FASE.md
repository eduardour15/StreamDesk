# PROMPT — Auditoria de la fase N

Cuando el usuario diga `Audita la fase N`, interpreta N segun `AGENTS.md`:

1. Lee `docs/ROADMAP.md`, `docs/prompts/FASE_N_*.md`, `docs/ESTADO_FASES.md`, codigo, pruebas y ADRs. No asumas que reportes anteriores estan correctos.
2. Para cada F[N]-T[XX] comprueba artefacto concreto (archivo/clase/UI/servicio), caso feliz, fallos y pruebas. Clasifica `COMPLETADA`, `PARCIAL`, `BLOQUEADA` o `PENDIENTE`, citando evidencia del repo.
3. Reproduce las pruebas posibles (build/test/fixture/manual Windows) y verifica que los reportes anteriores corresponden con codigo actual. Compara con la aceptacion del prompt y puerta de la fase anterior.
4. Revisa privacidad de secretos, URLs Xtream, passwords en logs/SQLite/Git, limites de conexiones, no bloqueo UI y liberacion de recursos.
5. Prueba manual real necesaria: si no hay entorno, **NO** apruebes gate; proporciona checklist de pruebas al usuario.
6. Emite matriz de incidencias con severidad y paso de reproduccion. No programes la siguiente fase. Si el usuario pidio SOLO auditoria, no modifiques codigo; actualiza el estado de auditoria en `docs/ESTADO_FASES.md` con evidencia, sin borrar historial.
7. Entrega veredicto `APROBADA`, `PENDIENTE_DE_VALIDACION` o `BLOQUEADA`, sustentado en criterios de aceptacion.
