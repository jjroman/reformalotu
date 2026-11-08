using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Reformk.Data;

public static class VideoCatalogDatabaseMigration
{
    private const string LegacyMigration = "20261031221712_ImplementProjectVideoSelection";

    public static async Task MigrateAsync(
        ApplicationDbContext application,
        VideoCatalogDbContext catalog,
        CancellationToken cancellationToken = default)
    {
        var applicationConnection = (SqliteConnection)application.Database.GetDbConnection();
        var catalogConnection = (SqliteConnection)catalog.Database.GetDbConnection();
        var applicationPath = GetDatabasePath(applicationConnection.ConnectionString);
        var catalogPath = GetDatabasePath(catalogConnection.ConnectionString);
        if (string.Equals(applicationPath, catalogPath, OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The application and video catalog must use separate SQLite database files.");
        }

        var applied = (await application.Database.GetAppliedMigrationsAsync(cancellationToken)).ToHashSet();
        if (!applied.Contains(LegacyMigration))
        {
            await application.GetService<IMigrator>().MigrateAsync(LegacyMigration, cancellationToken);
        }

        await catalog.Database.MigrateAsync(cancellationToken);
        await CopyLegacyCatalogAsync(applicationPath, application, catalog, cancellationToken);
        await application.Database.MigrateAsync(cancellationToken);
    }

    private static string GetDatabasePath(string connectionString)
    {
        var settings = new SqliteConnectionStringBuilder(connectionString);
        if (settings.Mode == SqliteOpenMode.Memory ||
            string.IsNullOrWhiteSpace(settings.DataSource) ||
            settings.DataSource == ":memory:")
        {
            throw new InvalidOperationException("The application and video catalog require separate persistent SQLite database files.");
        }
        return Path.GetFullPath(settings.DataSource);
    }

    private static async Task CopyLegacyCatalogAsync(
        string applicationPath,
        ApplicationDbContext application,
        VideoCatalogDbContext catalog,
        CancellationToken cancellationToken)
    {
        await application.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var check = application.Database.GetDbConnection().CreateCommand();
            check.CommandText = "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = 'Videos'";
            if (await check.ExecuteScalarAsync(cancellationToken) is null)
            {
                return;
            }
        }
        finally
        {
            await application.Database.CloseConnectionAsync();
        }

        await catalog.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            var connection = (SqliteConnection)catalog.Database.GetDbConnection();
            await using var attach = connection.CreateCommand();
            attach.CommandText = "ATTACH DATABASE $path AS legacy";
            attach.Parameters.AddWithValue("$path", applicationPath);
            await attach.ExecuteNonQueryAsync(cancellationToken);
            try
            {
                await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
                await using var command = connection.CreateCommand();
                command.Transaction = (SqliteTransaction)transaction;
                command.CommandText = """
                    CREATE TABLE IF NOT EXISTS CatalogMigrationState (Id INTEGER PRIMARY KEY CHECK (Id = 1));
                    SELECT COUNT(*) FROM CatalogMigrationState WHERE Id = 1;
                    """;
                var alreadyCopied = Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken)) != 0;
                if (!alreadyCopied)
                {
                    command.CommandText = "SELECT COUNT(*) FROM Videos";
                    if (Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken)) != 0)
                    {
                        throw new InvalidOperationException("Cannot migrate the legacy catalog into a non-empty video database.");
                    }
                }

                foreach (var table in new[] { "Videos", "VideoInputs", "VideoTaskDefinitions", "VideoSemanticIndexes" })
                {
                    command.CommandText = $"PRAGMA main.table_info(\"{table}\")";
                    var columns = new List<string>();
                    await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
                    {
                        while (await reader.ReadAsync(cancellationToken))
                        {
                            columns.Add($"\"{reader.GetString(1).Replace("\"", "\"\"")}\"");
                        }
                    }
                    var columnList = string.Join(", ", columns);
                    if (!alreadyCopied)
                    {
                        command.CommandText = $"INSERT INTO main.\"{table}\" ({columnList}) SELECT {columnList} FROM legacy.\"{table}\"";
                        await command.ExecuteNonQueryAsync(cancellationToken);
                    }

                    foreach (var (source, destination) in new[] { ("main", "legacy"), ("legacy", "main") })
                    {
                        command.CommandText = $"""
                            SELECT COUNT(*) FROM (
                                SELECT {columnList} FROM {source}."{table}"
                                EXCEPT
                                SELECT {columnList} FROM {destination}."{table}"
                            )
                            """;
                        if (Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken)) != 0)
                        {
                            throw new InvalidOperationException($"The transferred {table} table does not match the legacy catalog.");
                        }
                    }
                }
                command.CommandText = "INSERT OR IGNORE INTO CatalogMigrationState (Id) VALUES (1)";
                await command.ExecuteNonQueryAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            finally
            {
                await using var detach = connection.CreateCommand();
                detach.CommandText = "DETACH DATABASE legacy";
                await detach.ExecuteNonQueryAsync(cancellationToken);
            }
        }
        finally
        {
            await catalog.Database.CloseConnectionAsync();
        }

        await application.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS CatalogTransferCompleted (Id INTEGER PRIMARY KEY CHECK (Id = 1));
            INSERT OR IGNORE INTO CatalogTransferCompleted (Id) VALUES (1);
            """,
            cancellationToken);
    }
}
