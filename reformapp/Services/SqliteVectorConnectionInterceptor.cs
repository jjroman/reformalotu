using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;

namespace ReformApp.Services;

public sealed class SqliteVectorOptions
{
    /// <summary>Path to the native extension; defaults to vector.dll/vector.so next to the app.</summary>
    public string? LibraryPath { get; init; }

    /// <summary>Dimension of the embedding model; must match the stored vectors.</summary>
    public int Dimensions { get; init; } = 1536;
}

/// <summary>
/// Loads sqlite-vector on every opened connection and registers the embedding column,
/// as the extension state is per connection.
/// </summary>
public sealed class SqliteVectorConnectionInterceptor(
    IOptions<SqliteVectorOptions> options,
    ILogger<SqliteVectorConnectionInterceptor> logger) : DbConnectionInterceptor
{
    private bool _missingLibraryReported;

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        if (connection is not SqliteConnection sqlite)
        {
            return;
        }

        var libraryPath = ResolveLibraryPath();
        if (!File.Exists(libraryPath))
        {
            if (!_missingLibraryReported)
            {
                _missingLibraryReported = true;
                logger.LogWarning(
                    "sqlite-vector library was not found at {LibraryPath}; vector search is unavailable.",
                    libraryPath);
            }

            return;
        }

        try
        {
            sqlite.EnableExtensions(true);
            sqlite.LoadExtension(libraryPath);
        }
        catch (SqliteException exception)
        {
            // The app must keep working without vector search; semantic search falls back to an in-memory scan.
            logger.LogError(exception, "sqlite-vector could not be loaded from {LibraryPath}.", libraryPath);
            return;
        }

        using var exists = sqlite.CreateCommand();
        exists.CommandText = "select 1 from sqlite_master where type = 'table' and name = 'VideoSemanticIndexes'";
        if (exists.ExecuteScalar() is null)
        {
            return;
        }

        using var init = sqlite.CreateCommand();
        init.CommandText =
            $"select vector_init('VideoSemanticIndexes','Embedding','type=FLOAT32,dimension={options.Value.Dimensions},distance=COSINE')";
        init.ExecuteNonQuery();
    }

    public override Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        ConnectionOpened(connection, eventData);
        return Task.CompletedTask;
    }

    private string ResolveLibraryPath()
    {
        if (!string.IsNullOrWhiteSpace(options.Value.LibraryPath))
        {
            return Path.GetFullPath(options.Value.LibraryPath, AppContext.BaseDirectory);
        }

        var fileName = OperatingSystem.IsWindows() ? "vector.dll" : "vector.so";
        return Path.Combine(AppContext.BaseDirectory, fileName);
    }
}
