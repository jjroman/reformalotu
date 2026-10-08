using System.Text.Json;
using System.Text.Encodings.Web;
using System.Globalization;
using System.Text;
using System.Text.Unicode;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using YoutubeAnalyzer.Models;
using YoutubeAnalyzer.Services;

var secretConfiguration = new ConfigurationBuilder()
    .AddUserSecrets<AppSettings>(optional: true)
    .Build();

var settingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
await using var settingsFile = File.OpenRead(settingsPath);
var settings = await JsonSerializer.DeserializeAsync<AppSettings>(settingsFile)
    ?? throw new InvalidDataException("Could not read appsettings.json");

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

var jsonOptions = new JsonSerializerOptions
{
    WriteIndented = true,
    Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
};

foreach (var videoId in videoIds)
{
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
        continue;
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
        continue;
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
        continue;
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
            jsonOptions);
        continue;
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
        jsonOptions);

    Console.WriteLine(
        $"Downloaded {analysis.TotalComments} comments and transcript "
        + $"({transcript.LanguageName}, auto-generated: {transcript.IsAutoGenerated}) "
        + $"to {videoOutputDirectory}");
}

internal sealed class AppSettings
{
    public string VideoId { get; init; } = "";
    public string Channel { get; init; } = "";
    public string TranscriptLanguage { get; init; } = "es";
    public string OutputDirectory { get; init; } = "output";
    public bool OverwriteExisting { get; init; }
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
