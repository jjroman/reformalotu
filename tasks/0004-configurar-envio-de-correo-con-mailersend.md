# 0004 - Configurar el envío de correo con MailerSend

| | |
|---|---|
| **Proyectos a modificar** | `Reformk`, `Reformk.Tests` |
| **Proyectos que no se modifican** | `youtube-analyzer` |
| **Estado** | Finalizada |

## Contexto

`Reformk` usa ASP.NET Core Identity y exige confirmar la cuenta fuera de desarrollo. El proyecto tenía el paquete de Azure Communication Email, pero no un remitente configurado. Existe una plantilla de confirmación en `emails/emailconfirmation.txt`.

## Historia de usuario

**Como** usuario de Reformk, **quiero** recibir los correos de confirmación y recuperación de cuenta mediante MailerSend SMTP, **para** completar de forma segura las operaciones de Identity.

## Alcance

### Entra

- Sustituir el paquete de Azure Communication Email por MailKit.
- Configurar SMTP de MailerSend con STARTTLS y credenciales externas a los archivos versionados.
- Integrar el envío con las interfaces de correo de ASP.NET Core Identity.
- Usar la plantilla de confirmación existente y publicarla junto con la aplicación.
- Documentar la configuración local y de producción y probar el renderizado de la plantilla.

### No entra

- Exponer un endpoint público genérico para enviar correos.
- Enviar campañas, notificaciones de producto u otros correos no relacionados con Identity.
- Configurar credenciales reales ni verificar la entrega de mensajes en el proveedor.

## Reglas

- No guardar credenciales SMTP en archivos versionados ni en registros.
- Usar STARTTLS y propagar los errores de envío; no simular éxito.
- Reutilizar los enlaces y tokens generados por Identity sin modificarlos.
- Mantener las cuentas sin confirmación requerida en desarrollo y exigir confirmación en otros entornos.

## Criterios de aceptación

- Identity puede enviar correo de confirmación y recuperación a través del SMTP configurado.
- La plantilla de confirmación sustituye nombre, aplicación, enlace y expiración sin dejar marcadores pendientes.
- La aplicación inicia en producción solo si la configuración SMTP obligatoria es válida.
- La contraseña SMTP se configura con User Secrets o variables de entorno, no en `appsettings`.
- El paquete de Azure Communication Email ya no está en el proyecto.
- La aplicación y las pruebas compilan y pasan.

## Validación

- Pruebas unitarias de sustitución de marcadores y rechazo de marcadores sin resolver.
- Ejecutar `dotnet test .\Reformk.Tests\Reformk.Tests.csproj`.
- Revisar manualmente las opciones de configuración y los artefactos de publicación; la entrega real requiere credenciales SMTP configuradas.

## Preguntas abiertas

Ninguna.

## Definición de terminado

Identity está conectado a MailerSend por SMTP seguro, la configuración y publicación están documentadas, las pruebas automáticas pasan y no hay credenciales en el repositorio.
