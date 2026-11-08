# ==========================================
# Reformk - Repository Bootstrap Script
# ==========================================

$Root = "."

Write-Host "Creating Reformk repository structure..." -ForegroundColor Green

# Directories
$directories = @(
    "$Root\docs",
    "$Root\frontend",
    "$Root\backend",
    "$Root\data",
    "$Root\infra"
)

foreach ($dir in $directories) {
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
}

# README.md
@'
# Reformk

## Reforma tu hogar con confianza

Reformk es una plataforma que ayuda a propietarios de viviendas a realizar proyectos de mejora del hogar mediante planes paso a paso generados con inteligencia artificial.

Los usuarios describen lo que quieren hacer y reciben:

- Materiales recomendados
- Herramientas necesarias
- Pasos de ejecución
- Recursos de aprendizaje
- Consejos y advertencias

## Objetivo

Reducir el tiempo, la incertidumbre y los errores asociados a las reformas DIY (Do It Yourself).

## MVP

El MVP permitirá:

1. Describir un proyecto.
2. Analizar la intención.
3. Recomendar materiales.
4. Recomendar herramientas.
5. Generar pasos.
6. Mostrar vídeos relevantes.

## Stack Tecnológico

### Frontend

- SvelteKit
- TypeScript
- Tailwind CSS

### Backend

- .NET 10
- ASP.NET Core
- Entity Framework Core

### Base de Datos

- SQLite

### IA

- Azure OpenAI

### Integraciones

- YouTube Data API

## Documentación

Ver carpeta docs.

## Estado

🚧 En fase de validación y MVP.
'@ | Set-Content "$Root\README.md"

# 00-vision.md
@'
# Visión

Convertirnos en el asistente digital de referencia para cualquier persona que quiera mejorar, reparar o reformar su hogar por sí misma.

## Misión

Ayudar a las personas a ejecutar proyectos de mejora del hogar proporcionando orientación práctica, personalizada y accesible.

Reformk transforma una intención en un plan de acción.

## Eslogan

Reforma tu hogar con confianza.
'@ | Set-Content "$Root\docs\00-vision.md"

# 01-problem-statement.md
@'
# Problem Statement

Actualmente los usuarios deben:

- Buscar en Google
- Ver decenas de vídeos
- Leer foros
- Comparar opiniones

La información existe, pero está dispersa.

## Problema principal

Las personas no necesitan más información.

Necesitan un plan.

## Hipótesis principal

Los usuarios prefieren recibir una guía práctica completa antes que invertir varias horas investigando por Internet.
'@ | Set-Content "$Root\docs\01-problem-statement.md"

# 02-user-personas.md
@'
# User Personas

## Carlos

Edad: 38

Propietario de vivienda.

Quiere ahorrar dinero realizando reparaciones sencillas.

### Problemas

- Poco tiempo
- Poco conocimiento técnico
- Miedo a cometer errores

---

## Laura

Edad: 45

Acaba de comprar una vivienda para reformar.

### Problemas

- Demasiadas opiniones contradictorias
- Dificultad para calcular materiales
'@ | Set-Content "$Root\docs\02-user-personas.md"

# 03-mvp-scope.md
@'
# MVP Scope

## Entra en el MVP

- Introducción de proyecto mediante texto
- Clasificación automática
- Recomendación de materiales
- Recomendación de herramientas
- Generación de guía paso a paso
- Recomendación de vídeos relevantes
- Persistencia básica en SQLite

## No entra en el MVP

- Login
- Suscripciones
- Pagos
- Marketplace
- Aplicación móvil
- Comunidad
- Chat persistente
- Análisis de imágenes
- Asesoría profesional

## Métrica de éxito

El usuario afirma:

"Ahora sé exactamente qué tengo que hacer."
'@ | Set-Content "$Root\docs\03-mvp-scope.md"

# 04-architecture.md
@'
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

## Arquitectura General

Usuario
↓
SvelteKit
↓
ASP.NET Core
├─ SQLite
├─ Azure OpenAI
└─ YouTube Data API
'@ | Set-Content "$Root\docs\04-architecture.md"

# 05-roadmap.md
@'
# Roadmap

## Fase 1

MVP

- Materiales
- Herramientas
- Pasos
- Vídeos

## Fase 2

Estimaciones

- Cantidades
- Costes aproximados

## Fase 3

Asistencia visual

- Fotografías
- Validación de ejecución

## Fase 4

Asesoría

- Consultas
- Revisión profesional

## Fase 5

Marketplace

- Materiales
- Herramientas
- Afiliados
'@ | Set-Content "$Root\docs\05-roadmap.md"

# 06-monetization.md
@'
# Monetización

## Fase Inicial

Gratuito.

Objetivo:

Validar adopción.

## Fase Intermedia

Afiliados

- Leroy Merlin
- Obramat
- ManoMano
- Amazon

## Fase Avanzada

Suscripción Premium

- Proyectos ilimitados
- Historial de proyectos
- Asistencia avanzada
- Soporte personalizado

## Futuro

Asesoría profesional bajo demanda.
'@ | Set-Content "$Root\docs\06-monetization.md"

# 07-backlog.md
@'
# Backlog

## Alta prioridad

- Crear landing principal
- Crear API de proyectos
- Integrar Azure OpenAI
- Integrar YouTube Data API
- Clasificación de proyectos
- Generación de materiales
- Generación de herramientas
- Generación de pasos

## Prioridad media

- Sistema de historial
- Sistema de favoritos
- Mejora de recomendaciones

## Futuro

- Análisis de imágenes
- Asesoría humana
- Marketplace
- Comunidad
'@ | Set-Content "$Root\docs\07-backlog.md"

# deployment-notes.md
@'
# Deployment Notes

Pendiente de definición.

Objetivo inicial:

- Frontend: SvelteKit
- Backend: .NET 10
- Base de datos: SQLite
- IA: Azure OpenAI
'@ | Set-Content "$Root\infra\deployment-notes.md"

Write-Host ""
Write-Host "✅ Reformk structure created successfully!" -ForegroundColor Green
Write-Host ""
Write-Host $Root