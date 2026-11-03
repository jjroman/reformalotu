using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ReformApp.Data;
using ReformApp.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace ReformApp.Tests.Services;

public sealed class VideoCatalogImporterTests
{
    [Fact]
    public async Task ImportStoresAnalysisAndReimportReplacesItWhenPromptVersionChanges()
    {
        var root = Path.Combine(Path.GetTempPath(), "catalog-import-" + Guid.NewGuid().ToString("N"));
        var videoId = "abcdefghijk";
        var videoFolder = Path.Combine(root, videoId);
        Directory.CreateDirectory(videoFolder);
        const string transcriptText = "Hoy vamos a renovar una habitación.";
        var transcriptHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(transcriptText)));
        File.WriteAllText(
            Path.Combine(videoFolder, "transcript.json"),
            JsonSerializer.Serialize(new { Segments = new[] { new { Text = transcriptText } } }));

        WriteIndex(version: 3, materialName: "Pintura", transcriptHash);
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = CreateDbContext(connection);
        var importer = CreateImporter(root, dbContext);

        await importer.ImportAsync();
        var firstInput = await dbContext.VideoInputs.SingleAsync();
        Assert.Equal("Pintura", firstInput.Name);
        Assert.Equal("kg", firstInput.Unit);
        Assert.Equal(1.5, firstInput.Quantity);
        Assert.Equal("Pintura las paredes", (await dbContext.VideoTaskDefinitions.SingleAsync()).Title);

        await importer.ImportAsync();
        Assert.Equal(1, await dbContext.VideoInputs.CountAsync());
        Assert.Equal(1, await dbContext.VideoTaskDefinitions.CountAsync());

        WriteIndex(version: 4, materialName: "Pintura lavable", transcriptHash);
        await importer.ImportAsync();

        var updatedInput = await dbContext.VideoInputs.SingleAsync();
        Assert.Equal("Pintura lavable", updatedInput.Name);
        Assert.Equal(4, (await dbContext.Videos.SingleAsync()).AnalysisPromptVersion);
        Assert.Equal(4, (await dbContext.VideoSemanticIndexes.SingleAsync()).PromptVersion);
        Assert.Equal(1, await dbContext.VideoTaskDefinitions.CountAsync());

        void WriteIndex(int version, string materialName, string hash)
        {
            var artifact = new
            {
                VideoId = videoId,
                Summary = "Renovación de habitación.",
                Embedding = new[] { 1f, 0f },
                TranscriptHash = hash,
                SummaryModel = "summary-model",
                PromptVersion = version,
                Inputs = new
                {
                    Materials = new[]
                    {
                        new
                        {
                            Id = "m1",
                            Name = materialName,
                            Brand = (string?)null,
                            Specification = "Lavable",
                            Quantity = 1.5,
                            Unit = "kg",
                            Notes = (string?)null,
                            Confidence = 8
                        }
                    },
                    Equipment = Array.Empty<object>(),
                    Labor = Array.Empty<object>()
                },
                Tasks = new[]
                {
                    new
                    {
                        Order = 1,
                        Title = "Pintura las paredes",
                        Description = "Aplicar la pintura.",
                        EstimatedMinutes = (double?)null,
                        MaterialIds = new[] { "m1" },
                        EquipmentIds = Array.Empty<string>(),
                        LaborIds = Array.Empty<string>(),
                        Confidence = 8
                    }
                },
                EmbeddingModel = "embedding-model",
                EmbeddingDimensions = 2,
                GeneratedAt = DateTimeOffset.Parse("2026-10-31T12:00:00Z")
            };
            File.WriteAllText(Path.Combine(videoFolder, "semantic-index.json"), JsonSerializer.Serialize(artifact));
        }
    }

    private static VideoCatalogImporter CreateImporter(string corpusPath, ApplicationDbContext dbContext)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Corpus:Path"] = corpusPath })
            .Build();
        var environment = new TestWebHostEnvironment();
        var reader = new CorpusInventoryReader(
            environment,
            configuration,
            Options.Create(new VideoCatalogOptions()));
        return new VideoCatalogImporter(
            reader,
            dbContext,
            environment,
            Options.Create(new SqliteVectorOptions { Dimensions = 2 }),
            NullLogger<VideoCatalogImporter>.Instance);
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

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = nameof(VideoCatalogImporterTests);
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public string WebRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
