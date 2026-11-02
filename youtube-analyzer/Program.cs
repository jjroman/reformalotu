using System.Text.Json;
using System.Text.Encodings.Web;
using System.Globalization;
using System.Text;
using System.Text.Unicode;
using System.Net.Http;
using System.ClientModel;
using Azure;
using Azure.Identity;
using Google;
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
IConsoleReporter semanticReporter = new PlainConsoleReporter();

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

    var consoleMode = args.Contains("--progress", StringComparer.OrdinalIgnoreCase) ? "Progress"
        : args.Contains("--plain", StringComparer.OrdinalIgnoreCase) ? "Plain"
        : settings.ConsoleMode;
    semanticReporter = ConsoleReporterFactory.Create(consoleMode);

    semanticReporter.Begin(directories.Length, parallelOptions.MaxDegreeOfParallelism);

    // The work is network-bound, so concurrent async calls are used instead of dedicated threads.
    await Parallel.ForEachAsync(
        directories,
        parallelOptions,
        async (directory, _) => await EnsureSemanticIndexAsync(Path.GetFileName(directory), directory));

    semanticReporter.End();

    return;
}

var metadataOnly = settings.MetadataOnly
    || args.Contains("--metadata-only", StringComparer.OrdinalIgnoreCase);
var verifyCommentCount = settings.VerifyCommentCount
    || args.Contains("--verify-comments", StringComparer.OrdinalIgnoreCase);
var hasVideoId = !string.IsNullOrWhiteSpace(settings.VideoId);
var hasChannel = !string.IsNullOrWhiteSpace(settings.Channel);

if (!metadataOnly && !hasVideoId && !hasChannel)
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

var quotaReported = 0;
using var quotaCancellation = new CancellationTokenSource();

if (metadataOnly)
{
    // Only videos.list is called (1 quota unit per 50 videos); no comments, transcripts or channel listing.
    var metadataCorpus = ResolveCorpusDirectory(settings.OutputDirectory);

    if (!Directory.Exists(metadataCorpus))
    {
        Console.Error.WriteLine($"Output directory not found: {metadataCorpus}");
        Environment.ExitCode = 1;
        return;
    }

    using var metadataYoutube = new YoutubeService(apiKey);
    var corpusEntries = Directory.EnumerateDirectories(metadataCorpus)
        .Select(directory => (VideoId: Path.GetFileName(directory), Directory: directory));

    await WriteMissingMetadataAsync(metadataYoutube, corpusEntries);
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
catch (GoogleApiException exception) when (YoutubeService.IsQuotaExceeded(exception))
{
    ReportQuotaExceeded();
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

// Writes metadata.json for every video that lacks it, including already-downloaded ones.
await WriteMissingMetadataAsync(
    youtubeService,
    videoIds
        .Select(id => (VideoId: id, Directory: isSingleVideo ? outputDirectory : Path.Combine(outputDirectory, id)))
        .Concat(isSingleVideo
            ? []
            : Directory.EnumerateDirectories(outputDirectory)
                .Select(directory => (VideoId: Path.GetFileName(directory), Directory: directory))));

if (quotaReported == 1)
{
    return;
}

// Downloads are limited to a small degree of parallelism to stay within YouTube API quotas.
try
{
await Parallel.ForEachAsync(
    videoIds,
    new ParallelOptions
    {
        MaxDegreeOfParallelism = Math.Max(1, settings.DownloadConcurrency),
        CancellationToken = quotaCancellation.Token
    },
    async (videoId, cancellationToken) =>
{
    var channel = isSingleVideo ? "" : settings.Channel;
    var videoOutputDirectory = isSingleVideo
        ? outputDirectory
        : Path.Combine(outputDirectory, videoId);
    Directory.CreateDirectory(videoOutputDirectory);

    long? remoteCommentCount = null;

    if (verifyCommentCount)
    {
        try
        {
            remoteCommentCount = await youtubeService.GetCommentCountAsync(videoId, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            Console.Error.WriteLine(
                $"Could not verify the comment count for {videoId}: {exception.Message}");
        }
            catch (GoogleApiException exception) when (YoutubeService.IsQuotaExceeded(exception))
            {
                ReportQuotaExceeded();
                return;
            }
    }

    var commentCountChanged = remoteCommentCount is { } remoteCount
        && await VideoOutputCache.HasCommentCountChangedAsync(
            videoOutputDirectory,
            remoteCount,
            jsonOptions,
            cancellationToken);

    if (!settings.OverwriteExisting
        && !commentCountChanged
        && await VideoOutputCache.IsCompleteAsync(
            videoId,
            videoOutputDirectory,
            jsonOptions))
    {
        Console.WriteLine($"Valid output already exists for {videoId}; skipping download.");
        await EnsureSemanticIndexAsync(videoId, videoOutputDirectory);
        return;
    }

    if (commentCountChanged)
    {
        Console.WriteLine($"Comment count changed for {videoId} (YouTube reports {remoteCommentCount}); downloading again.");
    }

    VideoOutputCache.ClearCompletionState(videoOutputDirectory);
    Console.WriteLine($"Processing video {videoId}...");

    List<YoutubeComment> comments;

    try
    {
        comments = await youtubeService.GetCommentsAsync(videoId, cancellationToken);
    }
    catch (GoogleApiException exception) when (YoutubeService.IsQuotaExceeded(exception))
    {
        ReportQuotaExceeded();
        return;
    }
    catch (OperationCanceledException) when (quotaCancellation.IsCancellationRequested)
    {
        return;
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
            channel,
            remoteCommentCount);
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
        channel,
        remoteCommentCount);

    Console.WriteLine(
        $"Downloaded {analysis.TotalComments} comments and transcript "
        + $"({transcript.LanguageName}, auto-generated: {transcript.IsAutoGenerated}) "
        + $"to {videoOutputDirectory}");

    await EnsureSemanticIndexAsync(videoId, videoOutputDirectory);
});
}
catch (OperationCanceledException) when (quotaCancellation.IsCancellationRequested)
{
    // The quota message was already reported; unfinished videos are retried on the next run.
}

void ReportQuotaExceeded()
{
    if (Interlocked.Exchange(ref quotaReported, 1) == 1)
    {
        return;
    }

    quotaCancellation.Cancel();
    Console.WriteLine(
        "YouTube API daily quota reached. Processing stopped; videos already downloaded were kept.");
    Console.WriteLine(
        $"The quota resets at midnight Pacific Time; try again after {YoutubeService.GetNextQuotaReset().ToLocalTime():yyyy-MM-dd HH:mm} (local time).");
}

async Task WriteMissingMetadataAsync(
    YoutubeService youtube,
    IEnumerable<(string VideoId, string Directory)> entries)
{
    try
    {
        var missing = entries
            .DistinctBy(entry => entry.VideoId)
            .Where(entry => !File.Exists(Path.Combine(entry.Directory, "metadata.json")))
            .ToDictionary(entry => entry.VideoId, entry => entry.Directory);

        if (missing.Count == 0)
        {
            Console.WriteLine("All videos already have metadata.json.");
            return;
        }

        Console.WriteLine($"Fetching metadata for {missing.Count} videos...");
        var items = await youtube.GetVideoMetadataAsync(missing.Keys.ToList());

        foreach (var metadata in items)
        {
            if (!missing.TryGetValue(metadata.VideoId, out var directory))
            {
                continue;
            }

            Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(
                Path.Combine(directory, "metadata.json"),
                JsonUnicode.UnescapeSurrogatePairs(JsonSerializer.Serialize(metadata, jsonOptions)));
        }

        Console.WriteLine($"Metadata written for {items.Count} of {missing.Count} videos.");
    }
    catch (HttpRequestException exception)
    {
        Console.Error.WriteLine($"Could not retrieve video metadata after retries: {exception.Message}");
    }
    catch (GoogleApiException exception) when (YoutubeService.IsQuotaExceeded(exception))
    {
        ReportQuotaExceeded();
    }
}

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
        semanticReporter.VideoCompleted(VideoOutcome.Skipped, "");
        return;
    }

    var videoStartedAt = DateTimeOffset.Now;
    var videoStopwatch = System.Diagnostics.Stopwatch.StartNew();

    try
    {
        var cachedTranscript = JsonSerializer.Deserialize<VideoTranscript>(
            await File.ReadAllTextAsync(transcriptPath),
            jsonOptions);

        if (cachedTranscript is null)
        {
            semanticReporter.VideoCompleted(VideoOutcome.Skipped, "");
            return;
        }

        var generated = await semanticIndexService.EnsureAsync(
            videoId,
            videoOutputDirectory,
            cachedTranscript,
            jsonOptions,
            semanticReporter.WriteDiagnostic);

        semanticReporter.VideoCompleted(
            generated ? VideoOutcome.Generated : VideoOutcome.UpToDate,
            generated
                ? $"Generated semantic index for {videoId}. Started {videoStartedAt:HH:mm:ss}, took {videoStopwatch.Elapsed:mm\\:ss\\.f}."
                : $"Semantic index for {videoId} is up to date.",
            videoStopwatch.Elapsed);
    }
    // The client's retry policy wraps exhausted timeouts in an AggregateException; no cancellation token is
    // passed in this mode, so any cancellation here is a request timeout and must not stop the whole batch.
    catch (Exception exception) when (exception is OperationCanceledException
        or AggregateException { InnerExceptions: [OperationCanceledException, ..] })
    {
        semanticReporter.VideoCompleted(
            VideoOutcome.Failed,
            $"Semantic index timed out for {videoId} (started {videoStartedAt:HH:mm:ss}, after {videoStopwatch.Elapsed:mm\\:ss\\.f}). "
            + "Rerun to retry it, or lower SemanticIndex:MaxConcurrency / MaxTranscriptCharacters, or raise RequestTimeoutSeconds.");
    }
    catch (Exception exception) when (exception is ClientResultException
        or RequestFailedException
        or AuthenticationFailedException
        or HttpRequestException
        or JsonException
        or InvalidOperationException)
    {
        semanticReporter.VideoCompleted(
            VideoOutcome.Failed,
            $"Semantic index failed for {videoId} (started {videoStartedAt:HH:mm:ss}, after {videoStopwatch.Elapsed:mm\\:ss\\.f}): {exception.Message}");
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

    /// <summary>"Plain" (one line per video) or "Progress" (live progress bar) for --semantic-only.</summary>
    public string ConsoleMode { get; init; } = "Plain";
    public bool MetadataOnly { get; init; }
    public bool VerifyCommentCount { get; init; }
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
