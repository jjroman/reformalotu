using System.Text.Json;
using System.Text.Encodings.Web;
using System.Globalization;
using System.Text;
using System.Text.Unicode;
using System.Net.Http;
using System.ClientModel;
using Azure;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using YoutubeAnalyzer.Models;
using YoutubeAnalyzer.Services;

var secretConfiguration = new ConfigurationBuilder()
    .AddUserSecrets<AppSettings>(optional: true)
    .Build();

var settingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
await using var settingsFile = File.OpenRead(settingsPath);
var settings = await JsonSerializer.DeserializeAsync<AppSettings>(
    settingsFile,
    new JsonSerializerOptions { ReadCommentHandling = JsonCommentHandling.Skip })
    ?? throw new InvalidDataException("Could not read appsettings.json");

var semanticOnly = settings.SemanticIndexOnly
    || args.Contains("--semantic-only", StringComparer.OrdinalIgnoreCase);

var jsonOptions = new JsonSerializerOptions
{
    WriteIndented = true,
    Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
};

SemanticIndexService? semanticIndexService = null;

if (settings.SemanticIndex.IsConfigured)
{
    semanticIndexService = new SemanticIndexService(
        settings.SemanticIndex,
        Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY")
            ?? secretConfiguration["AZURE_OPENAI_API_KEY"]);
}
else
{
    Console.WriteLine("SemanticIndex is not configured; summaries and embeddings will not be generated.");
}

if (semanticOnly)
{
    if (semanticIndexService is null)
    {
        Console.Error.WriteLine("--semantic-only requires a configured SemanticIndex section.");
        Environment.ExitCode = 1;
        return;
    }

    // Offline mode: only already-downloaded transcripts are used; YouTube is never contacted.
    var corpusDirectory = ResolveCorpusDirectory(settings.OutputDirectory);

    if (!Directory.Exists(corpusDirectory))
    {
        Console.Error.WriteLine($"Output directory not found: {corpusDirectory}");
        Environment.ExitCode = 1;
        return;
    }

    var directories = Directory.EnumerateDirectories(corpusDirectory).Order(StringComparer.Ordinal).ToArray();
    var parallelOptions = new ParallelOptions
    {
        MaxDegreeOfParallelism = Math.Max(1, settings.SemanticIndex.MaxConcurrency)
    };

    // The work is network-bound, so concurrent async calls are used instead of dedicated threads.
    await Parallel.ForEachAsync(
        directories,
        parallelOptions,
        async (directory, _) => await EnsureSemanticIndexAsync(Path.GetFileName(directory), directory));

    return;
}

var hasVideoId = !string.IsNullOrWhiteSpace(settings.VideoId);
var hasChannel = !string.IsNullOrWhiteSpace(settings.Channel);

if (!hasVideoId && !hasChannel)
{
    Console.Error.WriteLine("Set either VideoId or Channel in appsettings.json.");
    Environment.ExitCode = 1;
    return;
}

var apiKey = Environment.GetEnvironmentVariable("YOUTUBE_API_KEY")
    ?? secretConfiguration["YOUTUBE_API_KEY"];

if (string.IsNullOrWhiteSpace(apiKey))
{
    Console.Error.WriteLine("Missing YOUTUBE_API_KEY");
    Environment.ExitCode = 1;
    return;
}

using var youtubeService = new YoutubeService(apiKey);
using var transcriptService = new TranscriptService();

var outputDirectory = Path.GetFullPath(settings.OutputDirectory);
Directory.CreateDirectory(outputDirectory);

var isSingleVideo = hasVideoId;
List<string> videoIds;

try
{
    videoIds = isSingleVideo
        ? [settings.VideoId]
        : await youtubeService.GetVideoIdsForChannelAsync(settings.Channel);
}
catch (HttpRequestException exception)
{
    Console.Error.WriteLine(
        $"Could not retrieve videos for channel {settings.Channel} after retries: {exception.Message}");
    Environment.ExitCode = 1;
    return;
}

if (hasVideoId && hasChannel)
{
    Console.WriteLine("VideoId is set; Channel will be ignored.");
}

if (videoIds.Count == 0)
{
    Console.WriteLine($"No videos were found for channel {settings.Channel}.");
    return;
}

// Downloads are limited to a small degree of parallelism to stay within YouTube API quotas.
await Parallel.ForEachAsync(
    videoIds,
    new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, settings.DownloadConcurrency) },
    async (videoId, cancellationToken) =>
{
    var channel = isSingleVideo ? "" : settings.Channel;
    var videoOutputDirectory = isSingleVideo
        ? outputDirectory
        : Path.Combine(outputDirectory, videoId);
    Directory.CreateDirectory(videoOutputDirectory);

    if (!settings.OverwriteExisting
        && await VideoOutputCache.IsCompleteAsync(
            videoId,
            videoOutputDirectory,
            jsonOptions))
    {
        Console.WriteLine($"Valid output already exists for {videoId}; skipping download.");
        await EnsureSemanticIndexAsync(videoId, videoOutputDirectory);
        return;
    }

    VideoOutputCache.ClearCompletionState(videoOutputDirectory);
    Console.WriteLine($"Processing video {videoId}...");

    List<YoutubeComment> comments;

    try
    {
        comments = await youtubeService.GetCommentsAsync(videoId);
    }
    catch (HttpRequestException exception)
    {
        Console.Error.WriteLine(
            $"Skipping video {videoId} after network retries failed: {exception.Message}");
        return;
    }

    var analysis = CommentAnalysisService.Analyze(comments);

    await File.WriteAllTextAsync(
        Path.Combine(videoOutputDirectory, "comments.json"),
        JsonUnicode.UnescapeSurrogatePairs(JsonSerializer.Serialize(comments, jsonOptions)));

    await File.WriteAllTextAsync(
        Path.Combine(videoOutputDirectory, "analysis.json"),
        JsonSerializer.Serialize(analysis, jsonOptions));

    VideoTranscript? transcript;

    try
    {
        transcript = await transcriptService.GetTranscriptAsync(
            videoId,
            settings.TranscriptLanguage);
    }
    catch (HttpRequestException exception)
    {
        Console.Error.WriteLine(
            $"Comments were saved, but transcript download failed for {videoId}: {exception.Message}");
        return;
    }

    if (transcript is null)
    {
        Console.Error.WriteLine($"No transcript is available for video {videoId}.");
        File.Delete(Path.Combine(videoOutputDirectory, "transcript.json"));
        File.Delete(Path.Combine(videoOutputDirectory, "transcript.txt"));
        await VideoOutputCache.MarkCompleteAsync(
            videoId,
            transcriptAvailable: false,
            videoOutputDirectory,
            jsonOptions,
            channel);
        return;
    }

    await File.WriteAllTextAsync(
        Path.Combine(videoOutputDirectory, "transcript.json"),
        JsonUnicode.UnescapeSurrogatePairs(JsonSerializer.Serialize(transcript, jsonOptions)));

    var transcriptText = string.Join(
        Environment.NewLine,
        transcript.Segments.Select(segment => segment.Text));

    await File.WriteAllTextAsync(
        Path.Combine(videoOutputDirectory, "transcript.txt"),
        transcriptText);

    await VideoOutputCache.MarkCompleteAsync(
        videoId,
        transcriptAvailable: true,
        videoOutputDirectory,
        jsonOptions,
        channel);

    Console.WriteLine(
        $"Downloaded {analysis.TotalComments} comments and transcript "
        + $"({transcript.LanguageName}, auto-generated: {transcript.IsAutoGenerated}) "
        + $"to {videoOutputDirectory}");

    await EnsureSemanticIndexAsync(videoId, videoOutputDirectory);
});

// A relative OutputDirectory depends on the working directory, so fall back to the project's own folder.
static string ResolveCorpusDirectory(string outputDirectory)
{
    var fromWorkingDirectory = Path.GetFullPath(outputDirectory);

    if (Path.IsPathRooted(outputDirectory) || Directory.Exists(fromWorkingDirectory))
    {
        return fromWorkingDirectory;
    }

    for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
        directory is not null;
        directory = directory.Parent)
    {
        if (File.Exists(Path.Combine(directory.FullName, "youtube-analyzer.csproj")))
        {
            return Path.GetFullPath(Path.Combine(directory.FullName, outputDirectory));
        }
    }

    return fromWorkingDirectory;
}

// A failure here leaves the artifact missing, so the next run retries only this step.
async Task EnsureSemanticIndexAsync(string videoId, string videoOutputDirectory)
{
    var transcriptPath = Path.Combine(videoOutputDirectory, "transcript.json");

    if (semanticIndexService is null || !File.Exists(transcriptPath))
    {
        return;
    }

    try
    {
        var cachedTranscript = JsonSerializer.Deserialize<VideoTranscript>(
            await File.ReadAllTextAsync(transcriptPath),
            jsonOptions);

        if (cachedTranscript is null)
        {
            return;
        }

        var generated = await semanticIndexService.EnsureAsync(
            videoId,
            videoOutputDirectory,
            cachedTranscript,
            jsonOptions);

        Console.WriteLine(generated
            ? $"Generated semantic index for {videoId}."
            : $"Semantic index for {videoId} is up to date.");
    }
    catch (Exception exception) when (exception is ClientResultException
        or RequestFailedException
        or AuthenticationFailedException
        or HttpRequestException
        or JsonException
        or InvalidOperationException)
    {
        Console.Error.WriteLine($"Semantic index failed for {videoId}: {exception.Message}");
    }
}

internal sealed class AppSettings
{
    public string VideoId { get; init; } = "";
    public string Channel { get; init; } = "";
    public string TranscriptLanguage { get; init; } = "es";
    public string OutputDirectory { get; init; } = "output";
    public bool OverwriteExisting { get; init; }
    public bool SemanticIndexOnly { get; init; }
    public int DownloadConcurrency { get; init; } = 2;
    public SemanticIndexSettings SemanticIndex { get; init; } = new();
}

internal static class JsonUnicode
{
    public static string UnescapeSurrogatePairs(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        var result = new StringBuilder(json.Length);
        var consecutiveBackslashes = 0;

        for (var index = 0; index < json.Length;)
        {
            if (json[index] == '\\'
                && consecutiveBackslashes % 2 == 0
                && TryReadSurrogatePair(json, index, out var codePoint))
            {
                result.Append(char.ConvertFromUtf32(codePoint));
                index += 12;
                consecutiveBackslashes = 0;
                continue;
            }

            var current = json[index++];
            result.Append(current);
            consecutiveBackslashes = current == '\\'
                ? consecutiveBackslashes + 1
                : 0;
        }

        return result.ToString();
    }

    private static bool TryReadSurrogatePair(string json, int index, out int codePoint)
    {
        codePoint = 0;

        if (index + 12 > json.Length
            || json[index + 1] != 'u'
            || json[index + 6] != '\\'
            || json[index + 7] != 'u'
            || !int.TryParse(
                json.AsSpan(index + 2, 4),
                NumberStyles.HexNumber,
                CultureInfo.InvariantCulture,
                out var high)
            || !int.TryParse(
                json.AsSpan(index + 8, 4),
                NumberStyles.HexNumber,
                CultureInfo.InvariantCulture,
                out var low)
            || !char.IsHighSurrogate((char)high)
            || !char.IsLowSurrogate((char)low))
        {
            return false;
        }

        codePoint = char.ConvertToUtf32((char)high, (char)low);
        return true;
    }
}
