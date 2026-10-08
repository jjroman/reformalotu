# Arquitectura

## Frontend

- SvelteKit
- TypeScript
- Tailwind CSS

## Backend

- .NET 10
- ASP.NET Core
- Entity Framework Core

## Base de Datos

- SQLite

## AI

- Azure OpenAI

## APIs externas

- YouTube Data API

## Corpus de vídeos para el POC

- Importación del corpus local existente de `youtube-analyzer/output`, organizado por `videoId`.
- `transcript.json` y `transcript.txt` aportan el contenido para relacionar vídeos con proyectos; `comments.json` aporta preguntas y contexto de usuarios.
- `analysis.json` solo contiene métricas agregadas de comentarios y no se considera una clasificación temática.
- Título, canal y miniatura se obtienen mediante YouTube Data API; el `videoId` permite construir el enlace directo al vídeo.
- Las recomendaciones se limitan al corpus importado. No se incorpora en el POC un proceso de descarga continua.

## Arquitectura General

Usuario
↓
SvelteKit
↓
ASP.NET Core
├─ SQLite
├─ Azure OpenAI
├─ Corpus local de vídeos analizados
└─ YouTube Data API
