using Reformk.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Reformk.Tests.Services;

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

    [Fact]
    public void ReadSkipsWhenCorpusPathIsNotConfigured()
    {
        var reader = CreateReader(new ConfigurationBuilder().Build());

        Assert.Null(reader.Read());
    }

    [Fact]
    public void ReadSkipsWhenConfiguredCorpusPathDoesNotExist()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Corpus:Path"] = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))
            })
            .Build();
        var reader = CreateReader(configuration);

        Assert.Null(reader.Read());
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
        var reader = CreateReader(configuration);

        return reader.Read() ?? throw new InvalidOperationException("The configured corpus was not read.");
    }

    private static CorpusInventoryReader CreateReader(IConfiguration configuration) =>
        new(
            new TestWebHostEnvironment(),
            configuration,
            Options.Create(new VideoCatalogOptions()),
            NullLogger<CorpusInventoryReader>.Instance);

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
