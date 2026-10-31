# 0001 - Análisis estructurado de insumos y plan de tareas por video

| | |
|---|---|
| **Proyectos a modificar** | `youtube-analyzer` |
| **Proyectos que no se modifican** | `login`, `login.Tests` (la importación a base de datos queda fuera de esta tarea) |
| **Estado** | Finalizada |

## Contexto
Hoy el analyzer genera por video un resumen con embedding y, desde `promptVersion` 2, un bloque `inputs` con materiales, equipos y mano de obra. No hay secuencia de pasos para ejecutar la actividad, ni se sabe qué insumo se usa en qué paso. Sin eso no se puede calcular un análisis de insumos unitarios.

## Historia de usuario
**Como** analista de costos de ReformaloTu, **quiero** que cada video tenga un resumen fiel al transcript, sus insumos estructurados y una lista ordenada de tareas ligada a esos insumos, **para** estimar insumos unitarios por actividad y saber qué tan fiable es cada dato.

## Alcance
**Entra**
- Nuevo prompt y nueva estructura JSON en la misma llamada de `--semantic-only`.
- Ids por insumo y tareas que los referencian.
- Validación de integridad y regeneración controlada por `promptVersion` 3.
- Documentar el formato en el README de `youtube-analyzer`.

**No entra**
- Importar los datos a la base de datos de `login`.
- Mostrarlos en la web.
- Precios, rendimientos o cálculo de costos.

## Contrato de datos (JSON)

### Convenciones generales
- Claves en inglés y `camelCase`. Los valores de texto libre van en español.
- Los campos de abajo van siempre presentes. Si un dato no consta ni puede estimarse, su valor es `null`. Nunca se omite la clave.
- Las listas vacías se escriben `[]`.
- `confidence` es un entero de 1 a 10: 1 = completamente inferido, 5 = deducido con apoyo parcial del transcript, 10 = sacado textualmente. Si el modelo devuelve otro valor, el código lo limita a ese rango.
- Los ids son únicos dentro de un video y solo tienen sentido dentro de él. No identifican nada entre videos.
- Minutos, horas y cantidades son números decimales mayores que 0.
- No se inventan marcas. La marca solo se rellena si consta en el transcript.

### Raíz

| Campo | Tipo | Obligatorio | Descripción |
|---|---|---|---|
| `summary` | string | sí | Un párrafo en español, de 80 a 150 palabras, con solo lo que dice el transcript, sin inferencias. Es el único texto que se embebe. |
| `inputs` | objeto | sí | Insumos de la actividad. |
| `tasks` | array de Task | sí | Pasos en orden de ejecución. Puede ir vacío si no hay una actividad ejecutable. |

### `inputs`

| Campo | Tipo | Obligatorio | Descripción |
|---|---|---|---|
| `materials` | array de Material | sí | Puede ir vacío. |
| `equipment` | array de Equipment | sí | Herramientas, equipos y protección. Puede ir vacío. |
| `labor` | array de Labor | sí | Puede ir vacío solo si no se puede inferir ninguna mano de obra. |

### Material

| Campo | Tipo | Nulo | Valores y descripción |
|---|---|---|---|
| `id` | string | no | Formato `m1`, `m2`, `m3`…, único en el video. |
| `name` | string | no | Nombre genérico del material. |
| `brand` | string | sí | Marca citada en el transcript. |
| `specification` | string | sí | Tipo, medidas, color, grado, referencia u otro detalle que lo identifique. |
| `quantity` | número | sí | Cantidad total necesaria. |
| `unit` | string | sí | Texto libre y abreviado: `m2`, `m`, `ud`, `kg`, `l`, `saco`, etc. Obligatorio cuando `quantity` no es nulo. |
| `notes` | string | sí | Aclaraciones, por ejemplo de dónde sale la cantidad o si es una suposición. |
| `confidence` | entero 1–10 | no | Ver convenciones. |

### Equipment

| Campo | Tipo | Nulo | Valores y descripción |
|---|---|---|---|
| `id` | string | no | Formato `e1`, `e2`…, único en el video. |
| `name` | string | no | Nombre de la herramienta o equipo. |
| `category` | string | no | Uno de: `manual_tool`, `power_tool`, `machinery`, `safety`, `other`. |
| `brand` | string | sí | Marca citada en el transcript. |
| `specification` | string | sí | Modelo, potencia, tamaño u otro detalle. |
| `quantity` | número | sí | Unidades necesarias. |
| `notes` | string | sí | Aclaraciones. |
| `confidence` | entero 1–10 | no | Ver convenciones. |

### Labor

| Campo | Tipo | Nulo | Valores y descripción |
|---|---|---|---|
| `id` | string | no | Formato `l1`, `l2`…, único en el video. |
| `role` | string | no | Oficio o rol, por ejemplo `carpintero`, `albañil`, `electricista`, `ayudante`. |
| `level` | string | no | `lead` para el rol principal según el oficio, `helper` para ayudantes. |
| `headcount` | entero | sí | Personas de esa fila. Es mayor o igual a 1. |
| `estimatedHours` | número | sí | Horas totales de trabajo de esa fila para completar la actividad. |
| `notes` | string | sí | Aclaraciones. |
| `confidence` | entero 1–10 | no | Ver convenciones. |

Reglas de mano de obra:
- Debe haber exactamente un `lead` si hay al menos una fila.
- Los ayudantes van en filas separadas con `level: "helper"`, cada una con su `headcount`.
- El rol del `lead` sigue el oficio principal del trabajo (por ejemplo, trabajo en madera = carpintero).

### Task

| Campo | Tipo | Nulo | Valores y descripción |
|---|---|---|---|
| `order` | entero | no | Consecutivo desde 1, sin saltos ni repetidos. |
| `title` | string | no | Título corto, en infinitivo. |
| `description` | string | no | Cómo se ejecuta el paso, en 1 a 3 frases. |
| `estimatedMinutes` | número | sí | Duración del paso. |
| `materialIds` | array de string | no | Ids de `materials` usados. Puede ir vacío. |
| `equipmentIds` | array de string | no | Ids de `equipment` usados. Puede ir vacío. |
| `laborIds` | array de string | no | Ids de `labor` que intervienen. Puede ir vacío. |
| `confidence` | entero 1–10 | no | Ver convenciones. |

### Reglas de integridad
1. Todos los ids de `materialIds`, `equipmentIds` y `laborIds` deben existir en `inputs`. Si no existe, el código descarta esa referencia y no falla el video.
2. Los ids dentro de cada lista son únicos.
3. `order` es consecutivo desde 1. Si no lo es, el código lo reasigna según la posición.
4. `confidence` fuera de 1 a 10 se limita a ese rango.
5. `quantity` con valor exige `unit` con valor.

### Ejemplo completo (video `--iD5mqyYFo`)

```json
{
  "summary": "El vídeo presenta unas pinzas de bloqueo para persianas que impiden que se levanten desde el exterior mientras la vivienda está vacía. Son económicas, se colocan con un gesto sencillo, no requieren taladrar y no dejan marcas al retirarlas, por lo que sirven también a quienes viven de alquiler.",
  "inputs": {
    "materials": [
      {
        "id": "m1",
        "name": "Pinza de bloqueo para persiana",
        "brand": null,
        "specification": "Sin taladrar, desmontable sin dejar marcas",
        "quantity": 1,
        "unit": "ud",
        "notes": "Una por persiana; el transcript no indica la cantidad",
        "confidence": 6
      }
    ],
    "equipment": [],
    "labor": [
      {
        "id": "l1",
        "role": "manitas",
        "level": "lead",
        "headcount": 1,
        "estimatedHours": 0.1,
        "notes": "Estimación por persiana",
        "confidence": 4
      }
    ]
  },
  "tasks": [
    {
      "order": 1,
      "title": "Revisar la persiana",
      "description": "Comprobar que la pinza encaja en la guía de la persiana.",
      "estimatedMinutes": 2,
      "materialIds": [],
      "equipmentIds": [],
      "laborIds": ["l1"],
      "confidence": 3
    },
    {
      "order": 2,
      "title": "Colocar la pinza",
      "description": "Bajar la persiana y encajar la pinza con el gesto de ajuste.",
      "estimatedMinutes": 2,
      "materialIds": ["m1"],
      "equipmentIds": [],
      "laborIds": ["l1"],
      "confidence": 8
    }
  ]
}
```

## Requisitos de comportamiento
- Un solo prompt y una sola llamada por video, en `--semantic-only`.
- El prompt pide inferir lo que no sea explícito pero razonable, y marcar cada dato con `confidence`.
- La caché se invalida por `promptVersion` 3: los videos con versión menor se regeneran y los de versión 3 se saltan.
- La salida de consola, `Plain` y `Progress`, no cambia.

## Criterios de aceptación
- La salida cumple el contrato anterior, validada con un esquema JSON estricto.
- Un video sin herramientas ni marcas devuelve `equipment: []` y `brand: null`, sin inventar datos.
- Ninguna referencia en `tasks` apunta a un id inexistente.
- Todos los videos de una muestra de unos 10, con y sin mucho detalle, tienen un `lead` en `labor` y `order` consecutivo.
- Revisión manual de esa muestra para validar calidad del resumen y significado de `confidence`.

## Riesgos
- **Coste y tiempo:** se regeneran los 1720 videos y cada respuesta es más larga. Antes del lote completo, probar con la muestra y estimar tokens.
- **Fiabilidad:** pasos y tiempos serán en su mayoría inferidos, con `confidence` bajo. Quien los consuma debe filtrar por ese valor.
- **Resumen:** hacer más trabajo en una sola llamada podría empeorar el resumen en transcripts largos.
- **Portabilidad:** el JSON debe poder guardarse como `jsonb` en PostgreSQL o como texto en SQLite.

## Preguntas abiertas
1. ¿Se añade la actividad principal con nombre y unidad de medida (m², ud, ml)? Sin eso, el insumo unitario queda ambiguo.
2. ¿Qué umbral de `confidence` se considera utilizable en un análisis de costos?
3. ¿La importación a la base de datos es la siguiente tarea o va en esta entrega?

## Definición de terminado
El código compila, la muestra se genera con la estructura nueva, un script valida que no hay ids rotos ni `order` incorrecto, los resultados se revisan manualmente, el formato queda documentado en el README del analyzer y después se lanza el lote completo.
