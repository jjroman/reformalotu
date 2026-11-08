using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Reformk.Data;
using Xunit;

namespace Reformk.Tests.Services;

public sealed class VideoCatalogDatabaseMigrationTests
{
    [Fact]
    public async Task LegacyCatalogIsCopiedAndProjectsAndCountsSurviveCatalogRemoval()
    {
        var root = Path.Combine(Path.GetTempPath(), "reformk-split-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var appPath = Path.Combine(root, "app.db");
        var catalogPath = Path.Combine(root, "catalog.db");
        var appConnectionString = $"Data Source={appPath};Pooling=False";
        var catalogConnectionString = $"Data Source={catalogPath};Pooling=False";

        try
        {
            await using var application = new ApplicationDbContext(
                new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(appConnectionString).Options);
            await application.GetService<IMigrator>().MigrateAsync("20261031221712_ImplementProjectVideoSelection");
            await using (var legacyCatalog = new VideoCatalogDbContext(
                new DbContextOptionsBuilder<VideoCatalogDbContext>().UseSqlite(appConnectionString).Options))
            {
                await legacyCatalog.Database.ExecuteSqlRawAsync(
                    "INSERT INTO Videos (VideoId, Title, TranscriptText, Categories, Keywords, OpenCount) VALUES ('abcdefghijk', 'Pintar', 'Transcripción', '', '', 7)");
                legacyCatalog.VideoInputs.Add(new VideoInput { VideoId = "abcdefghijk", SourceId = "m1", Kind = "material", Name = "Pintura" });
                legacyCatalog.VideoTaskDefinitions.Add(new VideoTaskDefinition
                {
                    VideoId = "abcdefghijk", Order = 1, Title = "Pintar", Description = "Aplicar", MaterialIds = ["m1"]
                });
                var vector = new[] { 1f, 0f, 0f };
                legacyCatalog.VideoSemanticIndexes.Add(new VideoSemanticIndex
                {
                    VideoId = "abcdefghijk", Summary = "Resumen", Embedding = MemoryMarshal.AsBytes(vector.AsSpan()).ToArray(),
                    EmbeddingDimensions = 3, TranscriptHash = "hash", SummaryModel = "summary", EmbeddingModel = "embedding"
                });
                await legacyCatalog.SaveChangesAsync();
            }

            var projectId = Guid.NewGuid();
            application.Users.Add(new IdentityUser { Id = "owner", UserName = "owner" });
            application.Projects.Add(new ReformProject
            {
                Id = projectId, OwnerId = "owner", Name = "Proyecto", Description = "Descripción",
                SelectedVideoId = "abcdefghijk", WorkDescription = "Trabajo guardado",
                Inputs = [new ProjectInput { Id = Guid.NewGuid(), Kind = "material", Name = "Pintura editada" }],
                Tasks = [new ProjectTask { Id = Guid.NewGuid(), Title = "Tarea propia", Description = "Guardada" }]
            });
            await application.SaveChangesAsync();
            application.ChangeTracker.Clear();

            await using var catalog = new VideoCatalogDbContext(
                new DbContextOptionsBuilder<VideoCatalogDbContext>().UseSqlite(catalogConnectionString).Options);
            await VideoCatalogDatabaseMigration.MigrateAsync(application, catalog);
            await VideoCatalogDatabaseMigration.MigrateAsync(application, catalog);

            Assert.Equal(7, (await application.VideoOpenCounts.SingleAsync()).Count);
            Assert.Equal("Pintura", (await catalog.VideoInputs.SingleAsync()).Name);
            Assert.Equal("m1", Assert.Single((await catalog.VideoTaskDefinitions.SingleAsync()).MaterialIds));
            Assert.Equal(12, (await catalog.VideoSemanticIndexes.SingleAsync()).Embedding.Length);
            Assert.Equal("Pintar", (await catalog.Videos.SingleAsync()).Title);

            await catalog.Videos.ExecuteDeleteAsync();
            application.ChangeTracker.Clear();
            var project = await application.Projects.Include(p => p.Inputs).Include(p => p.Tasks).SingleAsync();
            Assert.Equal(projectId, project.Id);
            Assert.Equal("abcdefghijk", project.SelectedVideoId);
            Assert.Equal("Trabajo guardado", project.WorkDescription);
            Assert.Equal("Pintura editada", Assert.Single(project.Inputs).Name);
            Assert.Equal("Tarea propia", Assert.Single(project.Tasks).Title);
            Assert.Equal(7, (await application.VideoOpenCounts.SingleAsync()).Count);

            await application.Database.OpenConnectionAsync();
            await using var command = application.Database.GetDbConnection().CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name IN ('Videos','VideoInputs','VideoTaskDefinitions','VideoSemanticIndexes')";
            Assert.Equal(0L, await command.ExecuteScalarAsync());
            command.CommandText = "PRAGMA foreign_key_check";
            await using (var reader = await command.ExecuteReaderAsync())
            {
                Assert.False(await reader.ReadAsync());
            }
            await application.Database.CloseConnectionAsync();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task MigrationRejectsTheSameDatabaseForBothContexts()
    {
        var connectionString = $"Data Source={Path.Combine(Path.GetTempPath(), "same.db")}";
        await using var application = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connectionString).Options);
        await using var catalog = new VideoCatalogDbContext(
            new DbContextOptionsBuilder<VideoCatalogDbContext>().UseSqlite(connectionString).Options);

        await Assert.ThrowsAsync<InvalidOperationException>(() => VideoCatalogDatabaseMigration.MigrateAsync(application, catalog));
    }

    [Fact]
    public async Task ExistingCatalogIsNotOverwrittenAndLegacyDatabaseIsNotDropped()
    {
        var root = Path.Combine(Path.GetTempPath(), "reformk-conflict-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await using var application = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite($"Data Source={Path.Combine(root, "app.db")};Pooling=False").Options);
            await using var catalog = new VideoCatalogDbContext(new DbContextOptionsBuilder<VideoCatalogDbContext>()
                .UseSqlite($"Data Source={Path.Combine(root, "catalog.db")};Pooling=False").Options);
            await application.GetService<IMigrator>().MigrateAsync("20261031221712_ImplementProjectVideoSelection");
            await application.Database.ExecuteSqlRawAsync(
                "INSERT INTO Videos (VideoId, Title, TranscriptText, Categories, Keywords, OpenCount) VALUES ('old-video', 'Old', '', '', '', 5)");
            await Assert.ThrowsAsync<SqliteException>(() => application.Database.MigrateAsync());
            await catalog.Database.MigrateAsync();
            catalog.Videos.Add(new VideoCatalogItem { VideoId = "new-video", Title = "New" });
            await catalog.SaveChangesAsync();

            await Assert.ThrowsAsync<InvalidOperationException>(() => VideoCatalogDatabaseMigration.MigrateAsync(application, catalog));
            Assert.Equal("new-video", (await catalog.Videos.SingleAsync()).VideoId);
            await application.Database.OpenConnectionAsync();
            await using var check = application.Database.GetDbConnection().CreateCommand();
            check.CommandText = "SELECT COUNT(*) FROM Videos WHERE VideoId = 'old-video' AND OpenCount = 5";
            Assert.Equal(1L, await check.ExecuteScalarAsync());
            await application.Database.CloseConnectionAsync();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task FreshDatabaseCanStartAndMigrateAgain()
    {
        var root = Path.Combine(Path.GetTempPath(), "reformk-fresh-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await using var application = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite($"Data Source={Path.Combine(root, "app.db")};Pooling=False").Options);
            await using var catalog = new VideoCatalogDbContext(new DbContextOptionsBuilder<VideoCatalogDbContext>()
                .UseSqlite($"Data Source={Path.Combine(root, "catalog.db")};Pooling=False").Options);
            await VideoCatalogDatabaseMigration.MigrateAsync(application, catalog);
            await VideoCatalogDatabaseMigration.MigrateAsync(application, catalog);
            Assert.Empty(await application.Projects.ToListAsync());
            Assert.Empty(await catalog.Videos.ToListAsync());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
