# Instrucciones del repositorio

## sqlite-vector (búsqueda vectorial en SQLite)

Validado en Windows x64 con `Microsoft.EntityFrameworkCore.Sqlite` 10.0.12 (net10.0, SQLite 3.53.3) y `sqlite-vector` 1.1.2 (https://github.com/sqliteai/sqlite-vector).

- Binario nativo: release `vector-windows-x86_64-<versión>.zip` (`vector.dll`). Para Linux usar la variante correspondiente (`linux-x86_64`, `linux-musl-x86_64`, etc.).
- Cargar la extensión en cada conexión abierta (por ejemplo con un `DbConnectionInterceptor` en `ConnectionOpened`): `connection.EnableExtensions(true)` y `connection.LoadExtension(rutaVectorDll)`.
- Llamar a `vector_init` en cada conexión nueva, no solo al crear la tabla:
  `select vector_init('tabla','columna','type=FLOAT32,dimension=N,distance=COSINE')`.
- Mapear la columna vectorial como `byte[]` (BLOB). Insertar con `vector_as_f32('[...]')`.
- Consultar con SQL crudo (EF Core no traduce estas funciones):
  `select e.rowid, v.distance from tabla e join vector_full_scan('tabla','columna',vector_as_f32('[...]'),k) v on e.rowid = v.rowid`.
- Publicación: incluir `vector.dll` como contenido copiado al output y al publish. Aún no está probado el publish.
- Probar cambios con esta extensión en un proyecto temporal fuera del repo antes de tocar el código real.
