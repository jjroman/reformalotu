using login.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Xunit;

namespace login.Tests.Services;

public sealed class CorpusInventoryReaderTests
{
    private const string VideoId = "abcdefghijk";

    [Theory]
    [InlineData("foreign [Music] [Music] thank you [Music]")]
    [InlineData("[Música] [Música]")]
    [InlineData("   ")]
    public void ReadOmitsVideosWithoutSpokenContent(string transcriptText)
    {
        var inventory = ReadCorpusWith(transcriptText);

        Assert.Empty(inventory.Videos);
        Assert.Equal(1, inventory.OmittedByReason["empty-transcript"]);
    }

    [Fact]
    public void ReadKeepsVideosWithSpokenContent()
    {
        var inventory = ReadCorpusWith("[Music] hoy vamos a instalar una persiana [Music]");

        Assert.Equal(VideoId, Assert.Single(inventory.Videos).VideoId);
    }

    private static CorpusInventory ReadCorpusWith(string transcriptText)
    {
        var root = Path.Combine(Path.GetTempPath(), "corpus-reader-" + Guid.NewGuid().ToString("N"));
        var folder = Path.Combine(root, VideoId);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "transcript.txt"), transcriptText);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Corpus:Path"] = root })
            .Build();
        var reader = new CorpusInventoryReader(
            new TestWebHostEnvironment(),
            configuration,
            Options.Create(new VideoCatalogOptions()),
            Options.Create(new VideoInventoryOptions()));

        return reader.Read();
    }

    private sealed class TestWebHostEnvironment : Microsoft.AspNetCore.Hosting.IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = nameof(CorpusInventoryReaderTests);
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
