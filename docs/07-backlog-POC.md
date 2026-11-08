# Backlog-POC

Este backlog describe un experimento acotado, no el backlog definitivo del producto. El objetivo es reutilizar el proyecto `reformk/` para validar el flujo de acceso, creación de proyectos, persistencia en SQLite y asociación de proyectos con vídeos que ya existen en `youtube-analyzer/output`.

## Objetivo del POC

Comprobar que una persona puede iniciar sesión, describir una tarea de mejora del hogar y recibir una lista de vídeos del corpus existente que sean pertinentes para esa tarea; después puede volver a consultar el proyecto guardado.

## Decisiones de alcance

- Partir del proyecto existente `reformk/` (`Microsoft.NET.Sdk.Web`, .NET 10, Razor Pages, ASP.NET Core Identity y EF Core SQLite); no crear una SPA ni separar frontend y API.
- Reutilizar las páginas de autenticación de Identity y la configuración de inicio de sesión existente. Validar email/contraseña; probar Google OAuth si la configuración de desarrollo ya está disponible.
- Usar la base de datos SQLite mediante migraciones de EF Core. No depender de datos locales preexistentes de `app.db` para preparar el POC.
- Usar como fuente de vídeos el corpus local `youtube-analyzer/output`, organizado en carpetas por `videoId`.
- Mantener la selección de vídeos simple y explicable: comparar la descripción/categoría del proyecto con el texto de las transcripciones disponibles. No generar una guía completa con IA en esta prueba.
- Mostrar el vídeo mediante su miniatura de YouTube y un enlace directo construido con `videoId`. El título y el canal pueden enriquecerse con YouTube Data API si la configuración existente lo permite; no son requisito para completar el flujo básico.
- Limitar la demostración a un pequeño conjunto de tipos de proyecto que tengan transcripciones útiles en el corpus.

## Backlog priorizado

### P0 — Arrancar y reutilizar el proyecto

1. **Verificar el arranque del proyecto `reformk/`**
   - Confirmar que restaura y compila con .NET 10.
   - Confirmar que inicia en desarrollo y que las migraciones existentes crean el esquema de Identity en SQLite.
   - Criterio de aceptación: se puede iniciar la aplicación desde `reformk/` en una instalación limpia, sin depender de una base de datos personal ya creada.

2. **Dejar operativo el acceso existente**
   - Reutilizar registro, inicio y cierre de sesión de ASP.NET Core Identity.
   - Verificar el flujo de confirmación de cuenta en desarrollo para que el registro no quede bloqueado sin un proveedor de correo configurado; no relajar las políticas de producción.
   - Probar Google OAuth solo si sus credenciales están configuradas fuera del código.
   - Criterio de aceptación: un usuario puede registrarse, iniciar sesión y cerrar sesión; las páginas del POC requieren autenticación.

3. **Crear una navegación mínima del POC**
   - Sustituir la página inicial de plantilla por una entrada a “Crear proyecto” y “Mis proyectos”.
   - Añadir enlaces visibles al formulario, resultados e historial.
   - Criterio de aceptación: después de iniciar sesión se entiende cómo crear y consultar un proyecto.

### P1 — Preparar y relacionar el corpus de vídeos

4. **Inventariar vídeos utilizables**
   - Leer `transcript.json` o `transcript.txt` de cada carpeta de `youtube-analyzer/output`.
   - Identificar los tipos de proyecto con vídeos suficientes para una demostración pequeña y seleccionarlos como categorías iniciales.
   - Excluir vídeos sin transcripción disponible o con datos inválidos, y registrar en el log cuáles se omitieron y por qué.
   - Criterio de aceptación: el POC puede mostrar un resumen del corpus importado y de los vídeos omitidos, sin fallar silenciosamente.

5. **Cargar el catálogo local de vídeos**
   - Crear un proceso de importación repetible que extraiga, como mínimo, el `videoId` y el texto de la transcripción.
   - Mantener una asociación sencilla entre los vídeos y las categorías/palabras clave seleccionadas para la demo.
   - Hacer la importación idempotente: repetirla actualiza o conserva el catálogo sin duplicar vídeos.
   - Criterio de aceptación: los vídeos disponibles para recomendar proceden del corpus local y no de resultados arbitrarios de una búsqueda externa.

6. **Definir la presentación básica de cada vídeo**
   - Generar la miniatura y el enlace directo a partir del `videoId`.
   - Si se dispone de YouTube Data API, enriquecer la tarjeta con título y canal; si no, mostrar el identificador y el enlace sin inventar metadatos.
   - Criterio de aceptación: cada tarjeta abre el vídeo correcto en YouTube y funciona sin que título/canal sean obligatorios.

### P2 — Crear, persistir y consultar proyectos

7. **Añadir el modelo de proyecto a SQLite**
   - Crear una entidad mínima con identificador, usuario propietario, descripción, categoría opcional y fecha de creación.
   - Guardar los `videoId` recomendados junto al proyecto para que sus resultados sean reproducibles al volver al historial.
   - Añadir una migración de EF Core; mantener intactas las tablas de Identity.
   - Criterio de aceptación: los datos sobreviven al reinicio de la aplicación y cada proyecto queda asociado a su usuario.

8. **Implementar “Crear proyecto”**
   - Añadir un formulario Razor Pages para introducir la descripción y, opcionalmente, elegir una categoría inicial.
   - Validar que la descripción no esté vacía y mostrar errores de validación comprensibles.
   - Criterio de aceptación: al enviar una descripción válida se crea un proyecto persistido en SQLite.

9. **Relacionar y mostrar vídeos**
   - Buscar transcripciones relacionadas con la descripción/categoría usando la estrategia simple del POC.
   - Mostrar hasta tres vídeos ordenados por relevancia, guardando los identificadores asociados al proyecto.
   - Si no hay coincidencias, mostrar un estado vacío explícito y permitir modificar la descripción; no mostrar vídeos no relacionados como alternativa.
   - Criterio de aceptación: para las categorías preparadas hay resultados procedentes del corpus; para una categoría sin datos se informa que no se encontraron vídeos.

10. **Implementar “Mis proyectos”**
    - Mostrar los proyectos guardados por el usuario autenticado.
    - Permitir abrir un proyecto y volver a ver los vídeos asociados.
    - Restringir las consultas por el identificador del usuario autenticado.
    - Criterio de aceptación: un usuario no puede consultar proyectos de otra cuenta cambiando la URL o el identificador.

### P3 — Validar el experimento

11. **Probar el flujo de extremo a extremo**
    - Verificar registro/inicio de sesión, creación, persistencia, resultados de vídeo e historial.
    - Verificar una carpeta sin transcripción, un vídeo con datos inválidos y una búsqueda sin coincidencias.
    - Criterio de aceptación: todos los casos tienen un resultado explícito y los fallos de importación no interrumpen el uso del resto del corpus.

12. **Recoger señales básicas de uso**
    - Registrar la creación de proyectos y los clics en vídeos, sin guardar información personal innecesaria.
    - Anotar manualmente durante las pruebas si los vídeos sugeridos fueron pertinentes y si ayudaron a decidir el siguiente paso.
    - Criterio de aceptación: al final de una sesión de prueba se puede revisar cuántos proyectos se crearon y cuántos vídeos se abrieron.

## Fuera de este POC

- Sustituir la arquitectura de producto prevista o construir un frontend SvelteKit separado.
- Generar con Azure OpenAI un plan completo de reforma, estimaciones o cantidades.
- Procesar comentarios para responder preguntas, clasificar automáticamente todo el corpus o entrenar una base de conocimiento.
- Descargar vídeos, comentarios o transcripciones nuevos de forma automática.
- Marketplace, pagos, suscripciones, favoritos, imágenes, asesoría profesional y funciones sociales.
- Preparar el sistema para producción, multi-tenant avanzado o alto volumen.

## Riesgos y notas de implementación

- El corpus contiene `videoId`, transcripciones, comentarios y métricas agregadas; no incluye necesariamente título, canal ni miniatura. La tarjeta básica no debe depender de metadatos que no están disponibles.
- `analysis.json` contiene métricas agregadas de comentarios, no etiquetas temáticas; no se debe usar como clasificación de relevancia.
- La base local de desarrollo debe generarse desde migraciones y no incluir datos personales o credenciales.
