# 0003 - Elegir video al crear el proyecto y mostrar su análisis de insumos

| | |
|---|---|
| **Proyectos a modificar** | `ReformApp`, `ReformApp.Tests` |
| **Proyectos que no se modifican** | `youtube-analyzer` (ya genera `Inputs` y `Tasks` con `promptVersion` 3; ver tareas 0001 y 0002) |
| **Estado** | Completada |

## Contexto

- `Pages/Projects/Create` guarda el proyecto y evalúa candidatos con IA (`VideoRecommendationService`), guardando el resultado en `ProjectRecommendation`. El usuario no elige nada.
- La búsqueda semántica (`SemanticVideoSearchService`) solo se usa en `Pages/Interno/SearchTest`.
- `semantic-index.json` ya trae `Inputs` (materiales con cantidad y unidad, equipos y mano de obra con horas) y `Tasks` ordenadas. `VideoCatalogImporter` y `CorpusInventoryReader` no los leen: `VideoCatalogItem` solo tiene `Materials` y `Equipment` como `List<string>` obtenidos por búsqueda de términos, sin cantidad, unidad ni mano de obra.

## Historia de usuario

**Como** usuario que crea un proyecto, **quiero** ver videos relevantes, elegir el que más me convence y ver en mi proyecto qué materiales, herramientas y mano de obra necesito y qué debo hacer, **para** saber cómo ejecutar lo que describí.

## Alcance

**Entra**
1. **Importación del análisis.** Importar `Inputs` (materiales, equipos, mano de obra) y `Tasks` desde `semantic-index.json` a la base de datos, de forma reimportable (se actualiza si cambia `TranscriptHash` o `PromptVersion`). Sustituye las listas de texto actuales. Modelo relacional o JSON compatible con SQLite y PostgreSQL.
2. **Crear proyecto.** El usuario completa nombre, descripción y categoría y pulsa **Buscar videos**. El proyecto no se guarda todavía.
   - Se muestran 3 videos por búsqueda semántica (la de `SearchTest`): título, resumen y conteo de materiales, equipos y mano de obra.
   - **Buscar más** añade los 3 siguientes, sin repetir y conservando el formulario. Se oculta cuando no hay más.
   - **Elegir** crea el proyecto con ese video (`SelectedVideoId`) y redirige al detalle.
   - **Crear sin video** crea el proyecto sin video, sin insumos ni tareas. Es una opción válida en todo momento.
   - Sin resultados: se muestra un aviso y se puede ajustar la descripción o crear sin video.
3. **Insumos y tareas propios del proyecto.** Al adherir un video se copian sus materiales, equipos, mano de obra y tareas al proyecto. Las ediciones del usuario no modifican el catálogo del video.
4. **Detalle del proyecto.** Muestra el video adherido y cuatro listas tipo «to-do»: materiales (nombre, cantidad, unidad), equipos, mano de obra (oficio, nivel, personas, horas) y tareas (orden, título, descripción).
   - Edición simple en cada lista: **añadir** un elemento con un formulario corto y **eliminar** cualquiera. Las tareas se pueden marcar como hechas.
   - Los insumos añadidos manualmente se distinguen de los que vienen del video.
   - Al eliminar un insumo, sus referencias en las tareas se limpian.
5. **Cambiar o quitar el video.** El usuario puede cambiar el video desde el detalle (con la misma búsqueda y selección). Se pide confirmación porque **se eliminan todos los insumos y tareas del proyecto**, incluidos los editados o añadidos a mano, y se copian los del nuevo video. Quitar el video también elimina los insumos y tareas.
6. **Descripción del trabajo.** Texto generado por IA a partir de la descripción del proyecto y del análisis del video. Se guarda en `WorkDescription`, se regenera al cambiar el video y, si falla, el video queda adherido y se puede reintentar. Sin video no se muestra.
7. **Retirar `ProjectRecommendation`.** Eliminar la entidad, su tabla (con migración), `VideoRecommendationService`, el flujo de evaluación automática en Crear y el código y pruebas asociados (`VideoAssessment*`) si solo servían a esas recomendaciones. Antes de borrar, confirmar con una búsqueda de referencias qué se usa en otras partes (por ejemplo `OpenVideo` o `Edit`).

**No entra**
- Cambios en `youtube-analyzer`.
- Precios, rendimientos o costos.
- Combinar varios videos en un proyecto.
- Reordenar tareas por arrastre o editar un elemento existente (solo añadir y eliminar).

## Reglas

- La búsqueda semántica queda tras una abstracción que no obligue a `sqlite-vector` en el modelo de datos del proyecto.
- Solo el dueño del proyecto puede elegir o cambiar el video. El servidor valida que el video exista. Los POST llevan antiforgery.
- Los datos mostrados salen de la base de datos, nunca del cliente.
- Interfaz y descripción generada en español, con el estilo de la skill `reformalotu-site-style`.
- Datos no informados (cantidad, unidad, horas) se muestran como «no indicado», sin inventarlos.

## Criterios de aceptación

- Tras importar, un video tiene sus materiales con cantidad y unidad, equipos, mano de obra y tareas consultables. Reimportar sin cambios no duplica datos.
- Al buscar se ven 3 videos; «Buscar más» añade 3 sin repetir y se oculta al agotarse.
- Al elegir un video se crea el proyecto con ese video y el detalle muestra sus insumos, tareas y la descripción del trabajo.
- Se puede crear un proyecto sin video; el detalle ofrece elegir uno.
- En cada lista se puede añadir y eliminar elementos; los cambios persisten y no afectan al catálogo ni a otros proyectos.
- Al cambiar o quitar el video, tras confirmar, los insumos y tareas anteriores desaparecen y, si hay video nuevo, aparecen los suyos.
- Un usuario ajeno no puede cambiar el video ni editar las listas de un proyecto.
- Falla la IA: el proyecto y el video se conservan y se ofrece reintentar.
- No quedan referencias a `ProjectRecommendation` y la aplicación compila y migra sin errores.

## Validación

- Pruebas unitarias de importación (inserción, actualización, idempotencia), paginación sin repetidos, asociación del video y validación de pertenencia.
- Pruebas de copia de insumos al elegir video, añadir y eliminar elementos, limpieza de referencias, y borrado al cambiar o quitar el video.
- Prueba manual del flujo completo: buscar, buscar más, elegir, editar listas, cambiar de video y crear sin video.

## Decisiones tomadas

- El proyecto puede crearse sin video.
- El video se puede cambiar; al hacerlo se eliminan los insumos y tareas del proyecto.
- `ProjectRecommendation` se elimina.

## Preguntas abiertas

- Ninguna.

## Definición de terminado

El flujo completo funciona con datos importados, las pruebas pasan, las preguntas abiertas están resueltas y el `README.md` de `ReformApp` documenta las páginas y el modelo nuevos.
