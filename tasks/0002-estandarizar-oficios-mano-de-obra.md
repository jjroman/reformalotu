# 0002 - Estandarizar los oficios de mano de obra

| | |
|---|---|
| **Proyectos a modificar** | `youtube-analyzer` |
| **Proyectos que no se modifican** | `login`, `login.Tests` |
| **Estado** | Pendiente |

## Contexto

El análisis semántico genera los oficios de `inputs.labor` a partir de una transcripción y del trabajo descrito. Aunque la estructura de mano de obra ya es estable, el nombre del oficio es texto libre y puede variar entre vídeos para una misma profesión (por ejemplo, por sinónimos, abreviaturas, género, plural, mayúsculas o acentos). Estas diferencias dificultan agrupar y comparar las estimaciones.

## Historia de usuario

**Como** analista de costos de ReformaloTu, **quiero** que los oficios generados en mano de obra utilicen nombres estandarizados, **para** agrupar, comparar y procesar las estimaciones de distintos vídeos de forma coherente.

## Alcance

**Entra**
- Definir un vocabulario inicial de oficios canónicos en español para los casos habituales de reformas y mantenimiento del hogar.
- Normalizar los nombres generados antes de guardar cada índice semántico.
- Reconocer variaciones frecuentes (sinónimos, variantes de género y número, acentos, mayúsculas y abreviaturas) y asignarles el mismo oficio canónico.
- Mantener una descripción funcional cuando no sea posible asignar con fiabilidad un oficio del vocabulario.
- Actualizar la versión del prompt/contrato para regenerar los índices existentes y documentar el vocabulario y las reglas en el README de `youtube-analyzer`.

**No entra**
- Cambiar los campos de `LaborInput`, sus IDs, niveles, cantidades, horas o referencias desde las tareas.
- Inferir trabajadores adicionales o modificar la asignación entre `lead` y `helper`.
- Importar los datos a la base de datos o cambiar cómo se presentan en la web.
- Forzar una clasificación canónica cuando el trabajo descrito no permita identificar un oficio con suficiente confianza.

## Reglas de normalización

1. Los nombres reconocidos se guardan usando una única etiqueta canónica en español, definida en un catálogo acotado y documentado.
2. Las variantes conocidas se resuelven mediante una tabla explícita de alias; la normalización básica de espacios, mayúsculas y acentos no debe depender únicamente del modelo de IA.
3. La clasificación debe respetar el trabajo descrito: no se asigna un oficio solo porque aparezca mencionado incidentalmente en la transcripción.
4. Cuando el rol sea genérico o no haya una coincidencia fiable, se conserva la función concreta inferida y se mantiene la indicación genérica existente entre paréntesis cuando corresponda; no se guarda únicamente «manitas».
5. Si no puede inferirse ninguna función concreta, se aplica una salida genérica documentada sin presentar una profesión específica como un hecho.
6. La normalización no altera el orden ni los IDs de `labor`, los niveles `lead`/`helper` ni los `laborIds` de las tareas.
7. El proceso es determinista: una misma etiqueta o alias produce el mismo resultado en cada ejecución.

## Criterios de aceptación

- Existe un catálogo inicial de oficios canónicos y alias en español, documentado y cubierto por pruebas.
- Variantes representativas del mismo oficio (incluyendo diferencias de acentos, mayúsculas, género, número y sinónimos definidos) se guardan con la misma etiqueta canónica.
- Los oficios distintos no se fusionan por coincidencias parciales o por una mención incidental en la transcripción.
- Los roles específicos no reconocidos se conservan de forma legible y no se reemplazan por una profesión inventada.
- Se conserva la estructura de mano de obra y todas las referencias de tareas; los índices generados siguen cumpliendo el contrato de validación.
- La generación de índices obsoletos se controla mediante la versión actualizada del prompt/contrato.
- El README de `youtube-analyzer` explica el catálogo, sus alias, el tratamiento de roles desconocidos y el efecto de la nueva versión sobre la regeneración.

## Validación

- Añadir pruebas unitarias para etiquetas canónicas, alias y variaciones ortográficas, así como para roles específicos desconocidos y funciones genéricas.
- Revisar manualmente una muestra de vídeos de oficios variados y comprobar que la normalización coincide con el trabajo descrito.
- Verificar que la normalización no cambia los IDs ni las referencias `laborIds` y que no introduce oficios sin respaldo suficiente.

## Definición de terminado

El vocabulario inicial y sus alias están implementados y documentados; las pruebas cubren resultados reconocidos y no reconocidos; una muestra representativa genera oficios consistentes; y el cambio de versión permite regenerar los índices existentes sin alterar el contrato de mano de obra.
