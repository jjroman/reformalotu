using System.Runtime.InteropServices;
using login.Data;
using login.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace login.Tests.Services;

public sealed class SemanticVideoSearchServiceTests
{
    private sealed class FakeEmbeddingClient(float[] vector) : IQueryEmbeddingClient
    {
        public Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default) =>
            Task.FromResult(vector);
    }

    [Fact]
    public async Task WhenSimilarityIsAboveThresholdThenClosestVideoIsReturned()
    {
        await using var connection = await CreateConnectionAsync();
        await using var dbContext = CreateDbContext(connection);
        await SeedAsync(dbContext);
        var service = CreateService(dbContext, [1f, 0f, 0f]);

        var result = await service.SearchAsync("suelo");

        Assert.Equal("near", Assert.Single(result.Matches).VideoId);
    }

    [Fact]
    public async Task WhenNothingReachesThresholdThenNoMatchesAreReturnedWithBestSimilarity()
    {
        await using var connection = await CreateConnectionAsync();
        await using var dbContext = CreateDbContext(connection);
        await SeedAsync(dbContext);
        var service = CreateService(dbContext, [0f, 0f, 1f]);

        var result = await service.SearchAsync("nada relacionado");

        Assert.Empty(result.Matches);
        Assert.Equal(0d, result.BestSimilarity!.Value, 3);
    }

    [Fact]
    public async Task SearchPageReturnsStableResultsWithoutRepeatingVideos()
    {
        await using var connection = await CreateConnectionAsync();
        await using var dbContext = CreateDbContext(connection);
        foreach (var id in new[] { "near-a", "near-b", "near-c", "near-d" })
        {
            var vector = new[] { 1f, 0f, 0f };
            dbContext.Videos.Add(new VideoCatalogItem { VideoId = id, Title = id });
            dbContext.VideoSemanticIndexes.Add(new VideoSemanticIndex
            {
                VideoId = id,
                Summary = $"Resumen {id}",
                Embedding = MemoryMarshal.AsBytes(vector.AsSpan()).ToArray()
            });
        }
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext, [1f, 0f, 0f]);
        var firstPage = await service.SearchPageAsync("cocina", 0, 3);
        var secondPage = await service.SearchPageAsync("cocina", 3, 3);

        Assert.Equal(["near-a", "near-b", "near-c"], firstPage.Matches.Select(match => match.VideoId));
        Assert.True(firstPage.HasMore);
        Assert.Equal(["near-d"], secondPage.Matches.Select(match => match.VideoId));
        Assert.False(secondPage.HasMore);
    }

    private static SemanticVideoSearchService CreateService(ApplicationDbContext dbContext, float[] queryVector) =>
        new(
            dbContext,
            new FakeEmbeddingClient(queryVector),
            Options.Create(new SemanticSearchOptions { MinimumSimilarity = 0.5, MaxResults = 3 }),
            NullLogger<SemanticVideoSearchService>.Instance);

    private static async Task SeedAsync(ApplicationDbContext dbContext)
    {
        foreach (var (id, vector) in new[] { ("near", new[] { 1f, 0f, 0f }), ("far", new[] { 0f, 1f, 0f }) })
        {
            dbContext.Videos.Add(new VideoCatalogItem { VideoId = id, Title = id });
            dbContext.VideoSemanticIndexes.Add(new VideoSemanticIndex
            {
                VideoId = id,
                Summary = $"Resumen {id}",
                Embedding = MemoryMarshal.AsBytes(vector.AsSpan()).ToArray()
            });
        }

        await dbContext.SaveChangesAsync();
    }

    private static async Task<SqliteConnection> CreateConnectionAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        return connection;
    }

    private static ApplicationDbContext CreateDbContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        var dbContext = new ApplicationDbContext(options);
        dbContext.Database.EnsureCreated();
        return dbContext;
    }
}
