using login.Data;
using login.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace login.Tests.Services;

public sealed class VideoRecommendationServiceTests
{
    [Fact]
    public async Task PatioProjectPrioritizesPavingAndPlantsOverBathroomFixtures()
    {
        await using var connection = await CreateConnectionAsync();
        await using var dbContext = CreateDbContext(connection);
        dbContext.Videos.AddRange(
            CreateVideo(
                "patiopaving1",
                "Pavimento cerámico y vegetación para un patio",
                "pavimento|vegetación|patio",
                "Colocar pavimento de cerámica y plantas en el patio."),
            CreateVideo(
                "bathfixtures",
                "Instalación de sanitarios en un baño",
                "baño|sanitarios",
                "Cómo instalar un inodoro y un lavabo."));
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var recommendations = await service.RecommendAsync(
            "Quiero construir un patio con nuevo pavimento cerámico y vegetación. " +
            "Foto de inspiración: ![baño](https://example.com/banos#sanitarios)",
            null);

        Assert.Equal("patiopaving1", recommendations[0].Video.VideoId);
        Assert.DoesNotContain(recommendations, recommendation => recommendation.Video.VideoId == "bathfixtures");
        Assert.Contains("pavimento", recommendations[0].Explanation);
    }

    [Fact]
    public async Task KitchenProjectPrioritizesFurnitureAndStorageOverSalonPaintingAndLiveStreams()
    {
        await using var connection = await CreateConnectionAsync();
        await using var dbContext = CreateDbContext(connection);
        dbContext.Videos.AddRange(
            CreateVideo(
                "kitchenstore",
                "Renovación de muebles de cocina y organización del almacenamiento",
                "cocina|muebles|almacenamiento",
                "Ideas para organizar armarios y cajones de la cocina."),
            CreateVideo(
                "salonpaint1",
                "Cómo pintar las paredes de un salón",
                "pintura|salón",
                "Consejos para pintar un salón."),
            CreateVideo(
                "kitchenlive1",
                "En directo: cocina y muebles",
                "cocina|muebles",
                string.Join(' ', Enumerable.Repeat("cocina muebles quiero tener ver", 100))));
        await dbContext.SaveChangesAsync();

        var recommendations = await CreateService(dbContext).RecommendAsync(
            "Quiero renovar los muebles de mi cocina y mejorar la organización del almacenamiento.",
            "Cocinas");

        Assert.Equal("kitchenstore", recommendations[0].Video.VideoId);
        Assert.DoesNotContain(recommendations, recommendation =>
            recommendation.Video.VideoId is "salonpaint1" or "kitchenlive1");
    }

    [Fact]
    public async Task AddingAUrlDoesNotChangeTheRecommendationRanking()
    {
        await using var connection = await CreateConnectionAsync();
        await using var dbContext = CreateDbContext(connection);
        dbContext.Videos.AddRange(
            CreateVideo("patiopaving1", "Pavimento cerámico para patio", "patio|cerámica", ""),
            CreateVideo("bathfixtures", "Instalación de sanitarios en el baño", "baño|sanitarios", ""));
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var withoutUrl = await service.RecommendAsync("Construir patio con pavimento cerámico", null);
        var withUrl = await service.RecommendAsync(
            "Construir patio con pavimento cerámico https://example.com/baño-inodoro#instalación",
            null);

        Assert.Equal(
            withoutUrl.Select(recommendation => recommendation.Video.VideoId),
            withUrl.Select(recommendation => recommendation.Video.VideoId));
    }

    [Fact]
    public async Task ReturnsNoRecommendationsWhenCatalogHasNoRelevantVideo()
    {
        await using var connection = await CreateConnectionAsync();
        await using var dbContext = CreateDbContext(connection);
        dbContext.Videos.Add(CreateVideo(
            "salonpaint1",
            "Pintura de paredes en el salón",
            "salón|pintura",
            string.Join(' ', Enumerable.Repeat("cocina quiero tener ver foto", 100))));
        await dbContext.SaveChangesAsync();

        var recommendations = await CreateService(dbContext).RecommendAsync(
            "Quiero renovar el almacenamiento de mi cocina.",
            "Cocinas");

        Assert.Empty(recommendations);
    }

    [Fact]
    public async Task PrepareCandidatesReturnsTopRankedCandidatesWithinConfiguredLimit()
    {
        await using var connection = await CreateConnectionAsync();
        await using var dbContext = CreateDbContext(connection);
        dbContext.Videos.AddRange(
            CreateVideo(
                "candidate-c",
                "Muebles de cocina y almacenamiento",
                "cocina|muebles|almacenamiento",
                ""),
            CreateVideo(
                "candidate-a",
                "Muebles de cocina y almacenamiento",
                "cocina|muebles|almacenamiento",
                ""),
            CreateVideo(
                "candidate-b",
                "Muebles de cocina y almacenamiento",
                "cocina|muebles|almacenamiento",
                ""),
            CreateVideo(
                "candidate-low",
                "Iluminación para cocina",
                "cocina|iluminación",
                ""));
        await dbContext.SaveChangesAsync();

        var candidates = await CreateService(
            dbContext,
            new VideoAssessmentOptions { MaxCandidates = 2 })
            .PrepareCandidatesAsync(
                "Quiero renovar los muebles de mi cocina y mejorar el almacenamiento.",
                "Cocinas");

        Assert.Equal(
            ["candidate-a", "candidate-b"],
            candidates.Select(candidate => candidate.VideoId));
    }

    [Fact]
    public async Task PrepareCandidatesTruncatesTranscriptToConfiguredLimit()
    {
        await using var connection = await CreateConnectionAsync();
        await using var dbContext = CreateDbContext(connection);
        dbContext.Videos.Add(CreateVideo(
            "kitchenstore",
            "Muebles de cocina y almacenamiento",
            "cocina|muebles|almacenamiento",
            "transcript excerpt that is too long"));
        await dbContext.SaveChangesAsync();

        var candidate = Assert.Single(await CreateService(
                dbContext,
                new VideoAssessmentOptions { MaxTranscriptCharacters = 10 })
            .PrepareCandidatesAsync(
                "Quiero renovar los muebles de mi cocina y mejorar el almacenamiento.",
                "Cocinas"));

        Assert.Equal("transcript", candidate.TranscriptExcerpt);
    }

    [Fact]
    public async Task PrepareCandidatesReturnsEmptyForUnrecognizedIntentOrEmptyCatalog()
    {
        await using var connection = await CreateConnectionAsync();
        await using var dbContext = CreateDbContext(connection);
        var service = CreateService(dbContext);

        var unrecognizedIntent = await service.PrepareCandidatesAsync(
            "Necesito ayuda con algo",
            null);
        var emptyCatalog = await service.PrepareCandidatesAsync(
            "Quiero renovar los muebles de mi cocina.",
            "Cocinas");

        Assert.Empty(unrecognizedIntent);
        Assert.Empty(emptyCatalog);
    }

    private static async Task<SqliteConnection> CreateConnectionAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        return connection;
    }

    private static ApplicationDbContext CreateDbContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;
        var dbContext = new ApplicationDbContext(options);
        dbContext.Database.EnsureCreated();
        return dbContext;
    }

    private static VideoRecommendationService CreateService(
        ApplicationDbContext dbContext,
        VideoAssessmentOptions? assessmentOptions = null)
    {
        var catalogOptions = Options.Create(new VideoCatalogOptions
        {
            Categories = [new VideoCategoryOptions { Name = "Cocinas", Keywords = ["cocina"] }]
        });
        return new VideoRecommendationService(
            dbContext,
            catalogOptions,
            Options.Create(assessmentOptions ?? new VideoAssessmentOptions()),
            NullLogger<VideoRecommendationService>.Instance);
    }

    private static VideoCatalogItem CreateVideo(
        string id,
        string title,
        string keywords,
        string transcript) =>
        new()
        {
            VideoId = id,
            Title = title,
            Categories = keywords,
            Keywords = keywords,
            TranscriptText = transcript
        };
}
