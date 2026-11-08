# Reformk - Estrategia MVP basada en Bricocrack

## Introducción

Durante el proceso de investigación se analizó el canal de YouTube Bricocrack, uno de los mayores canales en español dedicados al bricolaje, reformas y mejoras del hogar.

La conclusión principal es que el problema no está en la falta de información o tutoriales.

Existe una enorme cantidad de contenido de calidad disponible para los usuarios.

El problema es que las personas no saben transformar toda esa información en un plan de acción concreto.

---

# Hipótesis Principal

Las personas no necesitan más información.

Necesitan claridad para actuar.

Los usuarios:

- No saben por dónde empezar.
- No saben qué comprar.
- No saben cuánto comprar.
- No saben qué herramienta utilizar.
- No saben si están tomando las decisiones correctas.
- No saben cómo organizar un proyecto de principio a fin.

---

# Posicionamiento de Reformk

Reformk no es un buscador de vídeos.

Reformk es un copiloto digital para reformas y bricolaje.

Su objetivo es transformar una idea o necesidad en un plan ejecutable.

---

# Problema Actual

Actualmente el usuario sigue este proceso:

Idea
↓
Google
↓
YouTube
↓
Foros
↓
Tiendas
↓
Opiniones
↓
Confusión

Debe recopilar información de múltiples fuentes y tomar decisiones por sí mismo.

---

# Propuesta de Valor

Reformk transforma este proceso en:

Idea
↓
Reformk
↓
Plan de ejecución
↓
Materiales
↓
Herramientas
↓
Vídeos recomendados
↓
Ejecución

---

# MVP

## Objetivo

Validar que los usuarios encuentran valor en una herramienta que convierte una intención en un plan de acción.

---

## Entrada

El usuario describe lo que quiere hacer.

Ejemplos:

- Quiero pintar una habitación.
- Quiero instalar suelo laminado.
- Quiero reformar un baño.
- Quiero construir una pérgola.

---

## Salida

### Resumen del proyecto

Descripción general de la tarea.

### Grado de dificultad

- Baja
- Media
- Alta

### Materiales

Lista de materiales necesarios.

### Herramientas

Lista de herramientas necesarias.

### Pasos

Guía paso a paso.

### Riesgos frecuentes

Errores comunes y recomendaciones.

### Vídeos recomendados

Selección de vídeos relevantes para apoyar la ejecución.

---

# Estrategia de Conocimiento

La ventaja competitiva del MVP no será la IA.

Será la base de conocimiento.

---

## Fuente 1: Transcripciones de vídeos

Las transcripciones contienen:

- Materiales utilizados.
- Herramientas utilizadas.
- Procedimientos.
- Consejos prácticos.
- Advertencias.
- Errores a evitar.

### Ejemplo

Transcripción:

"Primero limpia la superficie y después aplica imprimación."

Extracción:

```json
{
  "steps": [
    "Limpiar superficie",
    "Aplicar imprimación"
  ]
}
```

---

## Fuente 2: Comentarios de usuarios

Los comentarios contienen preguntas reales de personas que intentan ejecutar proyectos similares.

### Ejemplos

- ¿Cuánto material necesito?
- ¿Qué herramienta usas?
- ¿Vale para exterior?
- ¿Cuánto cuesta hacerlo?
- ¿Puedo hacerlo sin experiencia?

Estas preguntas reflejan problemas reales que el vídeo no resuelve completamente.

---

# Taxonomía Inicial

Los comentarios pueden clasificarse en:

- Materiales
- Herramientas
- Cantidades
- Costes
- Tiempo
- Dificultad
- Seguridad
- Compatibilidad
- Procedimiento
- Errores frecuentes
- Mantenimiento

---

# Arquitectura del Conocimiento

Comentarios
+
Transcripciones
+
Clasificación IA
=
Base de conocimiento

La IA utilizará esta base de conocimiento para responder preguntas y generar planes.

---

# Flujo MVP

Usuario
↓
Describe proyecto
↓
Clasificación IA
↓
Consulta base de conocimiento
↓
Generación del plan
↓
Materiales
+
Herramientas
+
Pasos
+
Vídeos recomendados

---

# Tecnologías

## Frontend

- SvelteKit
- TypeScript
- Tailwind CSS

## Backend

- .NET 10
- ASP.NET Core
- Entity Framework Core

## Base de datos

- SQLite

## IA

- Azure OpenAI

## Integraciones

- YouTube Data API

---

# Futuras Evoluciones

## Fase 2

- Cálculo de cantidades.
- Estimaciones de costes.
- Listas de compra.

## Fase 3

- Subida de fotografías.
- Seguimiento de proyectos.
- Validación de avances.

## Fase 4

- Asistencia personalizada.
- Consultas premium.
- Seguimiento remoto de reformas.

## Fase 5

- Integración con distribuidores.
- Leroy Merlin.
- Obramat.
- Fabricantes de materiales.

---

# Visión

Reformk ayuda a los propietarios a entender, planificar, ejecutar y supervisar proyectos de mejora del hogar.

Nuestra hipótesis es:

> La gente ya tiene acceso al conocimiento.
>
> Lo que necesita es una forma sencilla de convertir ese conocimiento en decisiones y acciones concretas.