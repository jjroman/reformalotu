# YouTube Analyzer

Aplicación de consola .NET 10 que recopila comentarios y transcripciones de vídeos de YouTube para apoyar la investigación de proyectos de reforma. Puede generar además un resumen en español y un embedding por transcripción para integrarlos en el catálogo semántico de `login`.

## Requisitos

- .NET 10 SDK.
- Una clave de YouTube Data API v3 con acceso a los vídeos que se van a analizar.
- Opcional: un recurso Azure OpenAI con despliegues de chat y embeddings para generar índices semánticos.
- `az login` si se usará la autenticación de Azure en lugar de una clave de Azure OpenAI.

## Configuración

Edita `appsettings.json` para elegir un vídeo o un canal. Si se proporciona `VideoId`, tiene prioridad y `Channel` se ignora.

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
    "MaxConcurrency": 4
  }
}
```

`VideoId` y `Channel` son mutuamente excluyentes en la práctica: configura al menos uno. Para un canal se acepta el ID de canal o su identificador/handle de YouTube.

Guarda las credenciales fuera del repositorio. Desde la carpeta `youtube-analyzer`, puedes usar User Secrets:

```powershell
dotnet user-secrets set YOUTUBE_API_KEY "tu-clave-de-youtube"
dotnet user-secrets set AZURE_OPENAI_API_KEY "tu-clave-de-azure-openai"
```

La clave de Azure OpenAI es opcional; al no configurarla, la aplicación intenta autenticarse mediante las credenciales de Azure disponibles, por ejemplo `az login`. El índice semántico solo se genera si `SemanticIndex` tiene un endpoint HTTPS y ambos despliegues configurados.

## Ejecutar

Desde la raíz del repositorio:

```powershell
dotnet run --project .\youtube-analyzer\youtube-analyzer.csproj
```

Para generar o actualizar los índices semánticos de transcripciones que ya están descargadas, sin consultar YouTube:

```powershell
dotnet run --project .\youtube-analyzer\youtube-analyzer.csproj -- --semantic-only
```

El modo `--semantic-only` requiere que el índice semántico esté configurado. La salida de consola se elige con `ConsoleMode` (`"Plain"`: una línea por vídeo; `"Progress"`: barra de progreso con contadores y ETA) o con los argumentos `--plain` / `--progress`, que tienen prioridad. Si la salida está redirigida a un archivo, `Progress` vuelve a `Plain` automáticamente. `DownloadConcurrency` limita el número de vídeos procesados a la vez; `OverwriteExisting` permite volver a descargar artefactos que ya están completos.

`VerifyCommentCount` (o el argumento `--verify-comments`) consulta a YouTube cuántos comentarios tiene cada vídeo (1 unidad de cuota por vídeo) y, si difiere del total ya descargado, vuelve a bajarlos. El recuento remoto se guarda en `download-state.json` para no repetir la descarga cuando la diferencia es permanente. Los vídeos con comentarios desactivados se procesan sin comentarios en lugar de abortar la ejecución.

## Resultados

Los archivos se guardan bajo `OutputDirectory`:

- Para un único vídeo: directamente en el directorio de salida.
- Para un canal: en un subdirectorio por ID de vídeo.
- `comments.json`: comentarios principales y respuestas, con autor, texto, fecha y likes.
- `analysis.json`: número total de comentarios y likes, y promedio de likes por comentario.
- `transcript.json` y `transcript.txt`: segmentos de la transcripción, si YouTube dispone de ella.
- `semantic-index.json`: resumen e embedding cuando el índice semántico está habilitado.
- `metadata.json`: estado que permite reconocer resultados completos y omitir trabajo ya realizado.

## Dependencias principales

- Google YouTube Data API para localizar vídeos y obtener comentarios.
- YoutubeExplode para descargar subtítulos disponibles.
- Azure OpenAI para resúmenes y embeddings opcionales.
