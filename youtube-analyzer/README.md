# YouTube Analyzer

## Usuario

Aplicación de consola que recopila comentarios y transcripciones de vídeos de YouTube. Opcionalmente crea un índice semántico con un resumen en español y un análisis estructurado de los insumos y tareas descritos o inferidos de la transcripción.

### Requisitos y configuración

- .NET 10 SDK.
- Una clave de YouTube Data API v3 para descargar comentarios, metadatos o localizar los vídeos de un canal.
- Para generar índices semánticos: un recurso Azure OpenAI con despliegues de chat y embeddings. También se puede usar autenticación de Azure con `az login`.

Edita `appsettings.json` para seleccionar un vídeo o un canal. `VideoId` tiene prioridad si ambos están configurados.

```json
{
  "VideoId": "",
  "Channel": "@identificador-del-canal",
  "TranscriptLanguage": "es",
  "OutputDirectory": "output",
  "OverwriteExisting": false,
  "SemanticIndexOnly": false,
  "DownloadConcurrency": 2,
  "SemanticIndex": {
    "Endpoint": "",
    "SummaryDeployment": "",
    "EmbeddingDeployment": "",
    "EmbeddingDimensions": 1536,
    "MaxTranscriptCharacters": 60000,
    "MaxConcurrency": 4,
    "RequestTimeoutSeconds": 300
  }
}
```

`RequestTimeoutSeconds` es el timeout de red por intento (por defecto 300). Si un vídeo agota los reintentos, se marca como fallido y el lote continúa; vuelve a ejecutar para reintentarlo.

Configura al menos `VideoId` o `Channel`. Para un canal se acepta el ID, el identificador o el handle de YouTube. El índice semántico se genera solo si `SemanticIndex` tiene un endpoint HTTPS y ambos despliegues configurados.

Guarda las credenciales fuera del repositorio. Desde la carpeta `youtube-analyzer`, puedes usar User Secrets:

```powershell
dotnet user-secrets set YOUTUBE_API_KEY "tu-clave-de-youtube"
dotnet user-secrets set AZURE_OPENAI_API_KEY "tu-clave-de-azure-openai"
```

La clave de Azure OpenAI es opcional; sin ella se usan las credenciales de Azure disponibles, por ejemplo las de `az login`.

### Uso

Desde la raíz del repositorio:

```powershell
dotnet run --project .\youtube-analyzer\youtube-analyzer.csproj
```

Para analizar transcripciones que ya están descargadas, sin consultar YouTube:

```powershell
dotnet run --project .\youtube-analyzer\youtube-analyzer.csproj -- --semantic-only
```

`--semantic-only` requiere el índice semántico configurado y procesa los directorios locales. Regenera índices con `promptVersion` anterior a 3 y omite los índices actuales. Antes de ejecutar el lote completo, configura `OutputDirectory` a una carpeta de muestra con unos diez vídeos; el modo no tiene un límite de muestra integrado.

La salida de consola se elige con `ConsoleMode` (`"Plain"`: una línea por vídeo; `"Progress"`: barra de progreso con contadores y ETA) o con `--plain` / `--progress`, que tienen prioridad. Si la salida está redirigida, `Progress` vuelve a `Plain`.

Otras opciones:

- `OverwriteExisting`: vuelve a descargar artefactos ya completos.
- `DownloadConcurrency`: limita el número de vídeos descargados en paralelo.
- `VerifyCommentCount` o `--verify-comments`: consulta el total remoto de comentarios y vuelve a descargar si difiere del guardado. Consume una unidad de cuota por vídeo y conserva el último total consultado para no repetir una diferencia permanente.
- `MetadataOnly`: genera metadatos faltantes sin descargar comentarios ni transcripciones; también se puede solicitar con `--metadata-only`.

### Resultados

Los archivos se guardan bajo `OutputDirectory`: directamente en esa carpeta para un vídeo único o en un subdirectorio por ID para un canal.

- `comments.json`: comentarios y respuestas con autor, texto, fecha y likes.
- `analysis.json`: número total de comentarios y likes, y promedio de likes por comentario.
- `transcript.json` y `transcript.txt`: segmentos de la transcripción, si está disponible.
- `semantic-index.json`: resumen, embedding y análisis estructurado cuando el índice semántico está habilitado.
- `metadata.json`: metadatos del vídeo.
- `download-state.json`: estado usado para reconocer resultados completos y recordar el último recuento remoto de comentarios.

En `semantic-index.json`, `summary` contiene un párrafo en español y es el único texto que se embebe. `inputs` contiene:

- `materials`: elementos con `id` (`m1`, `m2`...), nombre, marca y especificación opcionales, cantidad/unidad, notas y confianza.
- `equipment`: herramientas y equipos con `id` (`e1`, `e2`...), nombre, categoría (`manual_tool`, `power_tool`, `machinery`, `safety` o `other`), detalles y confianza.
- `labor`: roles con `id` (`l1`, `l2`...), oficio, nivel (`lead` o `helper`), número de personas y horas estimadas.

`tasks` es una lista ordenada de pasos. Cada tarea contiene `order`, título, descripción, duración estimada, confianza y listas `materialIds`, `equipmentIds` y `laborIds` que apuntan a los insumos utilizados. Se extraen como tareas todas las acciones prácticas que el vídeo muestra, explica o recomienda (obra, pero también decorar, plantar, abonar, limpiar...); la lista queda vacía solo si el vídeo es puramente informativo o no tiene contenido hablado. Las listas de insumos y tareas pueden estar vacías; las marcas no se inventan y los datos que no constan ni pueden estimarse son `null`. Los materiales y equipos son elementos mencionados o detectados en el texto de la transcripción: no garantizan una lista de compra completa, cantidades exactas ni idoneidad profesional.

Los vídeos cuya transcripción solo contiene anotaciones (`[Music]`, `[Música]`) o palabras de relleno del reconocedor (`foreign`, `thank you`, `thanks`, `gracias`) no se consideran con transcripción útil: el proyecto `login` los omite al importar el catálogo (motivo `empty-transcript`). Su `semantic-index.json`, si existe, no se usa.

`confidence` es un entero de 1 a 10: 1 indica un dato completamente inferido, 5 una deducción con apoyo parcial y 10 un dato textual. Las inferencias deben leerse teniendo en cuenta su confianza; las estimaciones no son precios ni rendimientos.

## Desarrollador

### Módulos y modelos

- `YoutubeAnalyzer.Models`: modelos de comentarios, transcripciones, metadatos, resultados de análisis e índice semántico. `VideoInputs` representa materiales, equipos y mano de obra; `VideoTask` representa los pasos y sus referencias a esos insumos.
- `YoutubeAnalyzer.Services`: acceso a YouTube y transcripciones, análisis de comentarios, caché de artefactos, reintentos de red, generación de resumen/insumos/tareas y embeddings, y reportes Plain/Progress.
- `Program.cs` (nivel superior): carga configuración y secretos, orquesta los modos normal, solo metadatos y `--semantic-only`.

La versión de prompt y contrato está en `SemanticIndexService`. El modo semántico hace una llamada de chat por vídeo para obtener el JSON estricto con `summary`, `inputs` y `tasks`, y otra operación de embeddings sobre `summary`. Antes de escribir el índice, el programa normaliza IDs, orden, referencias, confianza y valores incompatibles con el contrato. Las referencias de tareas a IDs inexistentes se descartan. Si hay tareas ejecutables pero el análisis no devuelve mano de obra, se añade un lead descriptivo de baja confianza y se le asocia a esas tareas. Un oficio genérico como «manitas» debe ir entre paréntesis junto a la función concreta.

### Desarrollo y validación

- Compilar: `dotnet build .\youtube-analyzer\youtube-analyzer.csproj`.
- En modo `Progress`, los mensajes emitidos durante el procesamiento deben pasar por `IConsoleReporter.WriteDiagnostic`, no escribirse directamente en consola; el reporter sincroniza la salida y mantiene la barra como última línea.
- Validar índices v3 (ids canónicos, `order` consecutivo, referencias de tareas, un único lead, rangos de confianza): `& .\youtube-analyzer\scripts\Validate-SemanticIndex.ps1 -Path .\youtube-analyzer\output [-VideoId id1,id2]` (PowerShell 7; devuelve código de salida 1 si hay fallos). Llámalo con `&`, no con `pwsh -File`, para que `-VideoId` reciba la lista.
- `semantic-index.json` conserva claves PascalCase porque el proyecto `login` lo lee con `JsonDocument` sensible a mayúsculas.
- `login` decide qué vídeos importar en `CorpusInventoryReader`; el filtro de transcripciones sin contenido hablado debe mantenerse alineado con lo descrito en Resultados. Sus pruebas están en `login.Tests\Services\CorpusInventoryReaderTests.cs`.
- La solución usa .NET 10, nullable reference types y dependencias NuGet declaradas en `youtube-analyzer.csproj`.
- No se requiere base de datos. El índice se guarda como JSON portable, apto para almacenarse posteriormente como JSON o JSONB.
- La generación semántica requiere acceso al recurso Azure OpenAI y puede incurrir en coste. Valida primero una muestra pequeña; no lances el lote completo hasta revisar manualmente el resumen, las inferencias y la confianza.
- Para mantener la compatibilidad con cachés y el contrato, incrementa `PromptVersion` cuando cambien el prompt o el formato de `semantic-index.json`.
