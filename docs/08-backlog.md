# Backlog de producto

Este es el backlog vivo del producto, no el plan de ejecución del POC. Se ampliará y reordenará con los resultados de las pruebas, las necesidades de los usuarios y lo que se vaya validando. Las tareas técnicas inmediatas del experimento están en [backlog-POC.md](./backlog-POC.md).

## Estado inicial

- Todos los elementos comienzan como **Pendientes**.
- Una capacidad solo pasa a **Hecha** cuando está implementada; una hipótesis, cuando además está validada con usuarios.
- Al terminar el POC, trasladar aquí los aprendizajes y las capacidades que se decida conservar; no asumir que completar el POC equivale a completar el producto.

## Prioridad alta — Validar la propuesta de valor

- [ ] **Validar que el usuario puede pasar de una intención a una acción concreta.** Comprobar en pruebas que entiende por dónde empezar y qué decisiones debe tomar.
- [ ] **Ofrecer una guía de ejecución completa para proyectos seleccionados.** Incluir resumen, dificultad, materiales, herramientas, pasos ordenados, riesgos frecuentes y errores que conviene evitar.
- [ ] **Ajustar las recomendaciones al contexto del proyecto.** Recoger medidas, tipo de superficie, ubicación interior/exterior y experiencia del usuario cuando cambien los materiales, cantidades o pasos.
- [ ] **Indicar límites y cuándo recurrir a un profesional.** Evitar presentar como segura una recomendación cuando falten datos o el trabajo requiera evaluación especializada.
- [ ] **Validar utilidad y confianza de las respuestas.** Medir si la guía ayuda a decidir el siguiente paso y si los usuarios consideran pertinentes sus recomendaciones.

## Prioridad alta — Base de conocimiento y vídeos

- [ ] **Convertir el corpus de `youtube-analyzer/output` en una fuente consultable.** Normalizar transcripciones y, cuando aporte valor, comentarios y sus métricas.
- [ ] **Clasificar el conocimiento de los vídeos.** Organizar procedimientos, materiales, herramientas, cantidades, costes, tiempo, dificultad, seguridad, compatibilidad y errores frecuentes.
- [ ] **Fundamentar las recomendaciones en fuentes identificables.** Permitir que el usuario vea qué vídeo o fragmento respalda una sugerencia.
- [ ] **Recomendar vídeos pertinentes para cada proyecto.** Mejorar la selección más allá de la asociación inicial por descripción o categoría y mostrar resultados vacíos cuando no haya coincidencias fiables.
- [ ] **Medir la relevancia de los vídeos.** Usar impresiones, clics y valoración de pertinencia para mejorar el catálogo y las recomendaciones.
- [ ] **Definir cómo mantener el corpus actualizado.** Decidir qué contenido incorporar y con qué frecuencia antes de automatizar nuevas descargas.

## Prioridad media — Cantidades, costes e historial

- [ ] **Estimar cantidades de materiales para proyectos compatibles.** Solicitar las medidas necesarias y expresar claramente supuestos y márgenes de incertidumbre.
- [ ] **Ofrecer costes aproximados y listas de compra.** Separar estimaciones de precios confirmados y validar primero que las cantidades sean útiles.
- [ ] **Permitir consultar y continuar proyectos anteriores.** Conservar la guía, los datos aportados y los vídeos asociados.
- [ ] **Facilitar el seguimiento del proyecto.** Permitir que el usuario actualice su estado y retome los siguientes pasos.

## Prioridad futura — Asistencia y ecosistema

- [ ] **Explorar asistencia visual.** Permitir aportar fotografías para contextualizar un proyecto y validar avances, después de definir límites y tratamiento de imágenes.
- [ ] **Validar asistencia profesional bajo demanda.** Probar consultas o revisión de proyectos antes de construir un servicio completo.
- [ ] **Explorar integraciones con distribuidores y afiliados.** Vincular recomendaciones con productos disponibles sin convertir el producto en un buscador comercial.
- [ ] **Evaluar funciones de comunidad o colaboración.** Incluirlas solo si las pruebas muestran una necesidad clara.

## Decisiones a revisar tras el POC

- Qué tipos de proyecto y vídeos tuvieron correspondencias suficientemente pertinentes.
- Si los usuarios necesitan cantidades desde el primer plan o si basta con identificar los materiales.
- Si el historial aporta valor y cómo encaja con el modelo gratuito o Premium.
- Qué información del vídeo y qué fragmentos ayudan realmente a ejecutar la tarea.
- Qué partes del proyecto `reformapp/` y de la arquitectura del POC conviene mantener o sustituir para el producto.
