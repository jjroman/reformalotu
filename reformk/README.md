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
- **Acceso:** registro e inicio de sesión; el acceso con Google está disponible al configurar OAuth.

Los materiales, equipos, oficios y tareas se importan del análisis del vídeo y se copian al proyecto para que sus cambios no afecten al catálogo ni a otros proyectos. Las cantidades u horas no informadas se muestran como no indicadas; el contenido detectado no garantiza una lista completa ni asesoramiento profesional. La descripción de trabajo generada con IA es opcional: si falla, el vídeo y las listas se conservan y se puede reintentar.

## Desarrollador

### Módulos y modelos

- `Reformk.Pages`: páginas Razor para autenticación, proyectos y herramientas internas. Las operaciones de proyecto están autorizadas y restringidas al propietario.
- `Reformk.Data`: `ReformProject` y sus entidades editables (`ProjectInput`, `ProjectTask`); catálogo (`VideoCatalogItem`, `VideoInput`, `VideoTaskDefinition`) e índice semántico (`VideoSemanticIndex`). `ApplicationDbContext` configura persistencia relacional compatible con SQLite y PostgreSQL.
- `Reformk.Services`: lectura e importación reejecutable del análisis, búsqueda semántica paginada, asociación/copia de vídeo a proyectos y generación opcional de la descripción de trabajo.
- `Reformk.Tests`: pruebas de importación, paginación semántica y ciclo de vida de vídeos, insumos y tareas.
- `youtube-analyzer`: proyecto relacionado que produce el catálogo e índices semánticos; esta funcionalidad consume `Inputs` y `Tasks` del índice v3.

### Requisitos y configuración

- .NET 10 SDK.
- Azure OpenAI con un despliegue de embeddings para la búsqueda semántica. En desarrollo se pueden usar las credenciales de Azure CLI (`az login`); fuera de desarrollo se usa `DefaultAzureCredential`. Si la búsqueda no está disponible, se informa del error y se puede crear el proyecto sin vídeo.
- La generación de la descripción de trabajo usa `ProjectAi:Enabled`, `ProjectAi:Endpoint` y `ProjectAi:Deployment`. Se puede deshabilitar; al habilitarla en desarrollo se autentica con Azure CLI.
- Opcional: credenciales OAuth de Google. Google solo se habilita cuando están configurados tanto el Client ID como el Client Secret; sin ambos, el acceso local sigue disponible y Google no se registra.
- Opcional: extensión nativa `sqlite-vector` (`vector.dll` en Windows o `vector.so` en Linux). La búsqueda semántica cuenta con un recorrido alternativo en memoria si la extensión no está disponible.

La configuración general está en `appsettings.json`. La base predeterminada usa SQLite (`DataSource=app.db;Cache=Shared`). No guardes claves ni secretos en archivos versionados. Configura OAuth con User Secrets desde la carpeta `reformk`:

```powershell
dotnet user-secrets set "Authentication:Google:ClientId" "tu-client-id"
dotnet user-secrets set "Authentication:Google:ClientSecret" "tu-client-secret"
```

Opciones principales:

| Sección | Propósito |
| --- | --- |
| `ConnectionStrings:DefaultConnection` | Proveedor y conexión de la base de datos. |
| `Corpus:Path` | Ruta al corpus producido por `youtube-analyzer`; puede ser absoluta o relativa a la carpeta de la aplicación. |
| `VideoCatalog:Categories` | Categorías ofrecidas al crear un proyecto y sus términos asociados. |
| `SemanticSearch:Endpoint`, `EmbeddingDeployment`, `MinimumSimilarity`, `MaxResults` | Despliegue de embeddings y umbral de búsqueda; el endpoint puede reutilizar `ProjectAi:Endpoint`. |
| `ProjectAi:Enabled`, `Endpoint`, `Deployment` | Generación de la descripción de trabajo y endpoint compartido opcional. |
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
dotnet ef database update --project .\reformk\Reformk.csproj --startup-project .\reformk\Reformk.csproj
```

Al arrancar, la aplicación aplica las migraciones de EF Core e importa el catálogo solo cuando `Corpus:Path` está configurado y existe. El importador lee `semantic-index.json`; la versión del análisis y el hash de la transcripción determinan si los insumos y tareas deben sincronizarse. En desarrollo, las cuentas no requieren confirmar el correo; fuera de desarrollo, Identity exige confirmación.
