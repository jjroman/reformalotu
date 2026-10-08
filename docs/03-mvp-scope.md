# 03-mvp-scope.md

# MVP Scope

## Objetivo

Validar que los usuarios encuentran valor en transformar una idea de reforma en una guía práctica con materiales, herramientas, pasos y recursos de aprendizaje.

---

## Entra en el MVP

### Identidad

- Registro mediante email y contraseña.
- Inicio de sesión mediante email y contraseña.
- Inicio de sesión mediante Google OAuth.
- Perfil básico de usuario.

### Proyectos

- Introducción de proyecto mediante texto.
- Clasificación automática del proyecto.
- Generación de resumen ejecutivo.
- Estimación de dificultad.
- Recomendación de materiales.
- Recomendación de herramientas.
- Generación de guía paso a paso.
- Recomendación de hasta tres vídeos relacionados con el proyecto, seleccionados del corpus existente en `youtube-analyzer/output`.

### Vídeos relacionados (POC)

- Importación del corpus existente, organizado por identificador de vídeo.
- Uso de `transcript.json` o `transcript.txt` para relacionar el contenido del vídeo con el tipo y la descripción del proyecto.
- Uso de `comments.json` como contexto complementario para identificar dudas frecuentes de otros usuarios.
- Obtención de título, canal y miniatura mediante YouTube Data API; estos metadatos no forman parte del corpus actual.
- Presentación de cada vídeo como una tarjeta con título, canal, miniatura y enlace directo a YouTube.
- Registro de impresiones y clics para validar la relevancia de las recomendaciones.
- Si no hay vídeos con una relación suficiente o no hay transcripción disponible, no se muestran recomendaciones no relacionadas.

`analysis.json` contiene métricas agregadas de comentarios (total, likes y promedio), no una clasificación temática del vídeo. No se utilizará como catálogo de relevancia.

El POC utiliza los datos ya descargados; la descarga continua de nuevos vídeos y el análisis de todo el catálogo no forman parte de este alcance.

### Persistencia

- Historial de proyectos.
- Asociación de proyectos a usuarios.
- Persistencia en SQLite.

### Observabilidad

- Registro de consultas.
- Registro básico de actividad.
- Métricas de uso.

---

## No entra en el MVP

- Marketplace.
- Sistema de pagos.
- Suscripciones.
- Aplicación móvil.
- Comunidad.
- Mensajería entre usuarios.
- Análisis de imágenes.
- Asesoría profesional.
- Compartición de proyectos.
- Roles avanzados.
- Notificaciones.

---

## Casos de Uso Principales

### CU-01 Crear cuenta

Como usuario

Quiero registrarme

Para guardar mis proyectos.

### CU-02 Iniciar sesión con Google

Como usuario

Quiero iniciar sesión con mi cuenta de Google

Para acceder rápidamente.

### CU-03 Crear proyecto

Como usuario

Quiero describir una reforma

Para obtener una guía práctica.

### CU-04 Consultar proyecto

Como usuario

Quiero acceder a proyectos anteriores

Para reutilizar información.

### CU-05 Consultar vídeos relacionados

Como usuario

Quiero ver vídeos relacionados con mi proyecto

Para complementar la guía con recursos de aprendizaje relevantes.

---

## Métrica Principal de Éxito

Al menos un porcentaje significativo de usuarios registrados vuelve a utilizar la plataforma para un segundo proyecto.

---

## Definición de MVP Exitoso

El MVP será considerado exitoso cuando los usuarios indiquen:

"Ahora sé exactamente qué tengo que hacer."

y puedan:

- Comprender el proceso.
- Identificar los materiales.
- Identificar las herramientas.
- Encontrar recursos de aprendizaje relevantes.
- Ver vídeos relacionados con el proyecto y acceder a ellos en YouTube.
'@ | Set-Content "$Root\docs\03-mvp-scope.md"