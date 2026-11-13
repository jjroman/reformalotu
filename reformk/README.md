# Reformk

## Usuario

Reformk permite organizar proyectos de reforma, buscar vídeos relacionados mediante búsqueda semántica y llevar listas propias de materiales, equipos, mano de obra y tareas.

### Páginas

- **Crear proyecto (`/Projects/Create`):** indica nombre, descripción y categoría, busca vídeos relevantes en tandas de tres y elige una referencia o crea el proyecto sin vídeo. El proyecto solo se guarda al elegir un vídeo o continuar sin él.
- **Mis proyectos (`/Projects/Index`):** consulta los proyectos de la cuenta autenticada.
- **Detalle (`/Projects/Details/{id}`):** consulta la descripción, el vídeo seleccionado y su propuesta de trabajo; añade o elimina elementos de las listas, marca tareas como hechas, y cambia o quita el vídeo. Al reemplazarlo se pide confirmación y las listas actuales se sustituyen.
- **Editar proyecto (`/Projects/Edit/{id}`):** actualiza el nombre, la descripción y la categoría.
- **Abrir vídeo (`/Projects/OpenVideo`):** abre en YouTube el vídeo asociado al proyecto.
- **Prueba de búsqueda (`/Interno/SearchTest`):** herramienta interna para comprobar la búsqueda semántica.
- **Acceso:** registro e inicio de sesión; fuera de desarrollo, las cuentas nuevas deben confirmar su correo. El acceso con Google está disponible al configurar OAuth.

Los materiales, equipos, oficios y tareas se importan del análisis del vídeo y se copian al proyecto para que sus cambios no afecten al catálogo ni a otros proyectos. Las cantidades u horas no informadas se muestran como no indicadas; el contenido detectado no garantiza una lista completa ni asesoramiento profesional. La descripción de trabajo generada con IA es opcional: si falla, el vídeo y las listas se conservan y se puede reintentar.

Si un vídeo elegido deja de estar en el catálogo, el proyecto conserva su referencia, descripción de trabajo, insumos y tareas. El detalle informa de que el vídeo no está disponible y permite cambiarlo o quitarlo.

## Desarrollador

### Módulos y modelos

- `Reformk.Pages`: páginas Razor para autenticación, proyectos y herramientas internas. Las operaciones de proyecto están autorizadas y restringidas al propietario.
- `Reformk.Data`: `ApplicationDbContext` guarda usuarios, proyectos, listas y contadores de aperturas. `VideoCatalogDbContext` guarda el catálogo (`VideoCatalogItem`, `VideoInput`, `VideoTaskDefinition`) y su índice semántico (`VideoSemanticIndex`) en otra base de datos. El proyecto conserva el identificador del vídeo sin una clave foránea entre bases.
- `Reformk.Services`: lectura e importación reejecutable del análisis, búsqueda semántica paginada, asociación/copia de vídeo a proyectos, generación opcional de la descripción de trabajo y envío de correo de Identity mediante MailerSend SMTP.
- `Reformk.Tests`: pruebas de importación, paginación semántica y ciclo de vida de vídeos, insumos y tareas.
- `youtube-analyzer`: proyecto relacionado que produce el catálogo e índices semánticos; esta funcionalidad consume `Inputs` y `Tasks` del índice v3.

### Requisitos y configuración

- .NET 10 SDK.
- Azure OpenAI con un despliegue de embeddings para la búsqueda semántica. En desarrollo se pueden usar las credenciales de Azure CLI (`az login`); fuera de desarrollo se usa `DefaultAzureCredential`. Si la búsqueda no está disponible, se informa del error y se puede crear el proyecto sin vídeo.
- La generación de la descripción de trabajo usa `ProjectAi:Enabled`, `ProjectAi:Endpoint` y `ProjectAi:Deployment`. Se puede deshabilitar; al habilitarla en desarrollo se autentica con Azure CLI.
- Opcional: credenciales OAuth de Google. Google solo se habilita cuando están configurados tanto el Client ID como el Client Secret; sin ambos, el acceso local sigue disponible y Google no se registra.
- MailerSend SMTP se usa para la confirmación de cuenta y recuperación de contraseña. El host predeterminado es `smtp.mailersend.net`, puerto `587` con STARTTLS; configura las credenciales y una dirección de remitente verificada antes de enviar correos.
- Opcional: extensión nativa `sqlite-vector` (`vector.dll` en Windows o `vector.so` en Linux). La búsqueda semántica cuenta con un recorrido alternativo en memoria si la extensión no está disponible.

La configuración general está en `appsettings.json`. La base predeterminada usa SQLite (`DataSource=app.db;Cache=Shared`); en producción, `appsettings.Production.json` la cambia a `../db/app.db`. Por defecto, el catálogo se guarda en `video.db` junto a la base de aplicación; se puede elegir otra ubicación con `ConnectionStrings:VideoCatalog`. Deben ser archivos distintos. Las rutas relativas de SQLite se resuelven desde el directorio de trabajo de la aplicación y las carpetas de destino deben existir. No guardes claves ni secretos en archivos versionados. Configura OAuth con User Secrets desde la carpeta `reformk`:

```powershell
dotnet user-secrets set "Authentication:Google:ClientId" "tu-client-id"
dotnet user-secrets set "Authentication:Google:ClientSecret" "tu-client-secret"
dotnet user-secrets set "MailSender:SmtpUsername" "tu-usuario-smtp"
dotnet user-secrets set "MailSender:SmtpPassword" "tu-contraseña-smtp"
dotnet user-secrets set "MailSender:FromAddress" "cuenta-verificada@tu-dominio"
```

Opciones principales:

| Sección | Propósito |
| --- | --- |
| `ConnectionStrings:DefaultConnection` | Conexión SQLite de usuarios, proyectos y contadores. |
| `ConnectionStrings:VideoCatalog` | Conexión SQLite opcional del catálogo; por defecto, `video.db` junto a la base de aplicación. |
| `Corpus:Path` | Ruta al corpus producido por `youtube-analyzer`; puede ser absoluta o relativa a la carpeta de la aplicación. |
| `VideoCatalog:Categories` | Categorías ofrecidas al crear un proyecto y sus términos asociados. |
| `SemanticSearch:Endpoint`, `EmbeddingDeployment`, `MinimumSimilarity`, `MaxResults` | Despliegue de embeddings y umbral de búsqueda; el endpoint puede reutilizar `ProjectAi:Endpoint`. |
| `ProjectAi:Enabled`, `Endpoint`, `Deployment` | Generación de la descripción de trabajo y endpoint compartido opcional. |
| `MailSender:SmtpHost`, `SmtpPort` | Host SMTP y puerto de MailerSend; el puerto predeterminado es `587` y se usa STARTTLS. |
| `MailSender:SmtpUsername`, `SmtpPassword` | Credenciales SMTP; configúralas mediante User Secrets o variables de entorno (`MailSender__SmtpUsername`, `MailSender__SmtpPassword`). |
| `MailSender:FromAddress`, `FromName`, `AppName` | Remitente verificado y nombres mostrados en los correos. |
| `SqliteVector:Dimensions` | Dimensión de los embeddings; debe coincidir con el modelo utilizado. |
| `BootstrapAdmin:Email` | Correo al que asignar el rol `Admin` cuando esa cuenta ya existe. |

Para usar `sqlite-vector`, coloca el binario nativo compatible en `reformk` o configura `SqliteVector:LibraryPath`. El proyecto copia el binario al directorio de salida y publicación cuando está presente.

### Publicación en un alojamiento FTP

Reformk es una aplicación ASP.NET Core, no un sitio estático. Publica el proyecto y sube el contenido de `publish` a un alojamiento compatible con .NET 10 e IIS:

```powershell
dotnet publish .\reformk\Reformk.csproj -c Release -o .\publish
```

El catálogo de vídeos depende además del corpus generado por `youtube-analyzer`; no forma parte de la publicación de Reformk. Sube ese corpus a una ubicación persistente del servidor y configura `Corpus:Path` (por ejemplo, con la variable de entorno `Corpus__Path`) para apuntar a su carpeta `output`. Si la ruta no está configurada o no existe, la importación se omite y se registra un mensaje informativo; la aplicación conserva el catálogo que ya esté en la base de datos, pero no puede detectar novedades del corpus.

`sqlite-vector` es opcional. Si el servidor no puede cargar su DLL nativa por arquitectura incompatible o dependencias ausentes, la aplicación registra una advertencia y usa el recorrido de búsqueda en memoria. No es necesario subir `vector.dll` para que la aplicación funcione.

### Ejecutar, probar y migrar

Desde la raíz del repositorio:

```powershell
dotnet run --project .\reformk\Reformk.csproj
dotnet test .\Reformk.Tests\Reformk.Tests.csproj
```

Al arrancar, la aplicación aplica las migraciones de ambas bases de datos e importa el catálogo solo cuando `Corpus:Path` está configurado y existe. El importador lee `semantic-index.json`; la versión del análisis y el hash de la transcripción determinan si los insumos y tareas deben sincronizarse. En desarrollo, las cuentas no requieren confirmar el correo; fuera de desarrollo, Identity exige confirmación.

### Migración y actualización del catálogo

- Antes de actualizar una instalación existente, detén la aplicación y realiza una copia de seguridad coherente de SQLite, incluida la base de aplicación y, si ya existe, la del catálogo. Usa la API de copia de seguridad de SQLite o asegúrate de incluir correctamente el WAL; copiar solo el archivo principal mientras hay escrituras puede perder datos.
- El primer arranque copia y verifica el catálogo antiguo en la nueva base antes de retirarlo de la base de aplicación. Los identificadores seleccionados, las listas y los contadores de aperturas se conservan. Si el destino ya contiene otro catálogo, la migración se detiene sin sobrescribirlo. No ejecutes `dotnet ef database update` directamente para esta separación: el arranque coordina las dos bases y la copia.
- Para sustituir el catálogo, detén la aplicación, mantén una copia de seguridad de ambos archivos y reemplaza la base de catálogo por otra compatible con las migraciones de `VideoCatalogDbContext`. No reemplaces un archivo SQLite abierto ni omitas su WAL pendiente. Después inicia la aplicación y verifica la búsqueda y los proyectos existentes. Si `Corpus:Path` está configurado, la importación de arranque también puede modificar el catálogo.
- La reversión automática a una sola base no está soportada; para volver atrás, restaura copias coordinadas de las dos bases y la versión de aplicación correspondiente.
