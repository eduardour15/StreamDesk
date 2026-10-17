# PROMPT — Resolver bloqueos de una fase

Para `Resuelve los bloqueos de la fase N`:

1. Inspecciona codigo y `docs/ESTADO_FASES.md`; identifica los defectos no resueltos de Fase N y su relacion con el gate.
2. Ordena por: seguridad/secretos > fallo de compilacion > playback/autenticacion > corrupcion de datos > UI severa > secundarios.
3. Corrige **solo** bloqueos de la fase N con cambios minimos, pruebas de regresion y evidencia. No amplíes alcance ni avances de fase.
4. Si depende de test manual/credenciales/PC no disponibles, crea protocolo detallado para que usuario lo ejecute; no inventes aprobacion.
5. Actualiza estado + resultados y vuelve a evaluar gate. Reporta lo que falta y detente.
