# login

Aplicación web ASP.NET Core Razor Pages para registro e inicio de sesión y para gestionar proyectos de reforma de ReformaloTu. Cada usuario puede guardar proyectos con una descripción y recibir vídeos relacionados del catálogo.

## Funcionalidades

- Registro e inicio de sesión con ASP.NET Core Identity; inicio de sesión con Google cuando se configura OAuth.
- Creación, consulta, edición y eliminación de proyectos del usuario.
- Catálogo de vídeos indexados desde el corpus de `youtube-analyzer`, con filtros de texto y palabras clave.
- Recomendación de vídeos para un proyecto, con búsqueda semántica y evaluación de relevancia mediante Azure OpenAI cuando están configuradas.
- Persistencia local con SQLite y migraciones de Entity Framework Core.

## Requisitos

- .NET 10 SDK.
- Azure CLI (`az login`) para usar Azure OpenAI con las credenciales de desarrollo cuando la evaluación o la búsqueda semántica estén habilitadas.
- Opcional: credenciales OAuth de Google para habilitar ese proveedor de inicio de sesión.
- Opcional: la extensión nativa `sqlite-vector` (`vector.dll` en Windows o `vector.so` en Linux) para búsquedas vectoriales. Si no está disponible, la aplicación conserva la búsqueda semántica mediante el recorrido alternativo en memoria.

## Configuración local

La configuración general está en `appsettings.json`. La aplicación usa por defecto `DataSource=app.db;Cache=Shared`, así que la base de datos local se crea en la carpeta del proyecto.

No guardes claves ni secretos en archivos versionados. Para configurar Google OAuth, ejecuta desde la carpeta `login`:

```powershell
dotnet user-secrets set "Authentication:Google:ClientId" "tu-client-id"
dotnet user-secrets set "Authentication:Google:ClientSecret" "tu-client-secret"
```

En desarrollo, Azure Identity utiliza las credenciales de Azure CLI. Inicia sesión con `az login` y comprueba que la cuenta tenga acceso al recurso y a los despliegues configurados en `VideoAssessment` y `SemanticSearch`. Si se requiere una clave de Azure OpenAI, se puede proporcionar mediante User Secrets con `VideoAssessment:ApiKey` o `SemanticSearch:ApiKey`.

Algunos ajustes disponibles:

| Sección | Propósito |
| --- | --- |
| `ConnectionStrings:DefaultConnection` | Ruta y opciones de conexión SQLite. |
| `VideoCatalog:Categories` | Categorías y palabras clave del catálogo. |
| `VideoInventory:Materials` y `VideoInventory:Equipment` | Materiales y herramientas que se detectan en el contenido de los vídeos. |
| `VideoAssessment` | Activación, endpoint y límites para evaluar la relevancia de candidatos con Azure OpenAI. |
| `SemanticSearch` | Despliegue de embeddings, similitud mínima y máximo de resultados. |
| `SqliteVector:Dimensions` | Dimensión de los embeddings; debe coincidir con el modelo utilizado. |
| `BootstrapAdmin:Email` | Correo opcional al que se asigna el rol `Admin` cuando esa cuenta ya existe. |

Para usar `sqlite-vector`, coloca el binario nativo compatible con el sistema operativo en la carpeta del proyecto (`login`) antes de ejecutar o publicar la aplicación. El proyecto lo copia al directorio de salida y al de publicación cuando está presente. Se puede definir `SqliteVector:LibraryPath` para indicar una ruta alternativa.

## Ejecutar

Desde la raíz del repositorio:

```powershell
dotnet run --project .\login\login.csproj
```

En el arranque, la aplicación aplica las migraciones de EF Core e importa el catálogo configurado. En desarrollo, las cuentas no requieren confirmar el correo; fuera de desarrollo, Identity exige confirmación.

## Pruebas

Desde la raíz del repositorio:

```powershell
dotnet test .\login.Tests\login.Tests.csproj
```

## Fuentes del catálogo

El importador de vídeos lee los artefactos preparados por `youtube-analyzer`. Consulta `VideoCatalog` y `VideoInventory` en `appsettings.json` para configurar categorías, materiales y equipos usados durante la clasificación. El catálogo requiere transcripciones y, para búsqueda semántica, los índices generados en `semantic-index.json`.
