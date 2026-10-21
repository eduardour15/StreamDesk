# StreamDesk — Indice de prompts para Codex

Este paquete permite ejecutar cada fase **mencionando solo su numero** en Codex. No necesitas copiar el prompt completo: `AGENTS.md` actua como enrutador, carga las instrucciones detalladas y el ROADMAP antes de trabajar. Guarda toda esta carpeta **dentro del repositorio abierto en Codex**.

## Comandos cortos que entendera Codex

```text
Ejecuta la fase 0
Ejecuta la fase 1
Ejecuta la fase 2
Ejecuta la fase 3
Ejecuta la fase 4
Ejecuta la fase 5
Ejecuta la fase 6
Ejecuta la fase 7
```

Tambien:

```text
Ejecuta F3-T06
Audita la fase 3
Continua la fase 2 desde la primera tarea pendiente
Muestra el estado de StreamDesk
```

Mejora posterior a F7:

```text
Ejecuta el prompt de reproductor inmersivo
Ejecuta PLAY-UX-T02
```

## Archivos por fase

| Fase | Documento | Tareas | Comando corto |
|---|---|---:|---|
| **Fase 0** | [Validacion tecnica](prompts/FASE_0_VALIDACION_TECNICA.md) | 11 | `Ejecuta la fase 0` |
| **Fase 1** | [Fundamentos, arquitectura y seguridad](prompts/FASE_1_FUNDAMENTOS.md) | 10 | `Ejecuta la fase 1` |
| **Fase 2** | [Integracion Xtream y catalogo](prompts/FASE_2_CATALOGO_XTREAM.md) | 10 | `Ejecuta la fase 2` |
| **Fase 3** | [Motor de reproduccion en vivo](prompts/FASE_3_REPRODUCTOR.md) | 11 | `Ejecuta la fase 3` |
| **Fase 4** | [UX UI y MVP](prompts/FASE_4_UX_MVP.md) | 11 | `Ejecuta la fase 4` |
| **Fase 5** | [EPG peliculas y series](prompts/FASE_5_CONTENIDO_EPG_VOD.md) | 9 | `Ejecuta la fase 5` |
| **Fase 6** | [Calidad seguridad e instalador](prompts/FASE_6_VERSION_1_0.md) | 12 | `Ejecuta la fase 6` |
| **Fase 7** | [Mejoras opcionales](prompts/FASE_7_MEJORAS_OPCIONALES.md) | 6 | `Ejecuta la fase 7` |

## Incrementos posteriores a las fases

| Documento | Alcance | Invocación |
|---|---|---|
| [Reproductor inmersivo y series aleatorias](prompts/REPRODUCTOR_INMERSIVO_Y_SERIES_ALEATORIAS.md) | Modo inmersivo, episodios aleatorios, audio amplificado seguro e indicador de carga | `Ejecuta el prompt de reproductor inmersivo` |
| [Optimización de fluidez y UX/UI](prompts/OPTIMIZACION_FLUIDEZ_UX_UI.md) | Análisis y optimización de búsquedas, reproducción, loaders, controles y recursos de las funciones existentes, sin añadir funciones | `Lee y ejecuta docs/prompts/OPTIMIZACION_FLUIDEZ_UX_UI.md` |

## Flujo recomendado

1. Copia `AGENTS.md`, `docs/`, `README.md` y el resto del paquete a la **raiz** del proyecto StreamDesk.
2. Abre la carpeta del proyecto en Codex (no una carpeta superior distinta).
3. Escribe `Ejecuta la fase 0`.
4. Tras recibir el informe, valida personalmente pruebas que Codex no pueda ejecutar en Windows o sobre tu cuenta; si corresponde, di `Audita la fase 0`.
5. Continua con `Ejecuta la fase 1` **solo si** la anterior paso su gate; asi hasta F6. F7 es opcional.
6. Para problemas, usa `Resuelve los bloqueos de la fase N` antes de intentar la fase siguiente.

**Regla de oro:** el mensaje corto no sustituye el plan. Codex DEBE leer el archivo de fase correcto. Si el archivo no existe, debe informar el error y no improvisar la tarea.

**Seguridad:** introduce las credenciales en el formulario de la aplicacion cuando ya este implementado. **No** pegues contrasenas en este paquete, en prompts ni en un repositorio, especialmente si el endpoint usa HTTP.
