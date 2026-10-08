using System.Text.Json;
using YoutubeAnalyzer.Models;

namespace YoutubeAnalyzer.Services;

internal static class VideoOutputCache
{
    private const string CommentsFileName = "comments.json";
    private const string AnalysisFileName = "analysis.json";
    private const string TranscriptFileName = "transcript.json";
    private const string TranscriptTextFileName = "transcript.txt";
    private const string StateFileName = "download-state.json";

    public static async Task<bool> IsCompleteAsync(
        string videoId,
        string outputDirectory,
        JsonSerializerOptions jsonOptions,
        CancellationToken cancellationToken = default)
    {
        var commentsPath = Path.Combine(outputDirectory, CommentsFileName);
        var analysisPath = Path.Combine(outputDirectory, AnalysisFileName);
        var transcriptPath = Path.Combine(outputDirectory, TranscriptFileName);
        var transcriptTextPath = Path.Combine(outputDirectory, TranscriptTextFileName);
        var statePath = Path.Combine(outputDirectory, StateFileName);

        if (!File.Exists(commentsPath) || !File.Exists(analysisPath))
        {
            return false;
        }

        try
        {
            var comments = JsonSerializer.Deserialize<List<YoutubeComment>>(
                await File.ReadAllTextAsync(commentsPath, cancellationToken),
                jsonOptions);
            var analysis = JsonSerializer.Deserialize<AnalysisResult>(
                await File.ReadAllTextAsync(analysisPath, cancellationToken),
                jsonOptions);

            if (comments is null
                || analysis is null
                || comments.Any(comment => comment is null || comment.Replies is null))
            {
                return false;
            }

            var calculatedAnalysis = CommentAnalysisService.Analyze(comments);

            if (analysis.TotalComments != calculatedAnalysis.TotalComments
                || analysis.TotalLikes != calculatedAnalysis.TotalLikes
                || analysis.AverageLikesPerComment != calculatedAnalysis.AverageLikesPerComment)
            {
                Console.Error.WriteLine($"Cached comment data is inconsistent for video {videoId}.");
                return false;
            }

            if (File.Exists(statePath))
            {
                var state = JsonSerializer.Deserialize<VideoDownloadState>(
                    await File.ReadAllTextAsync(statePath, cancellationToken),
                    jsonOptions);

                if (state is null || state.VideoId != videoId)
                {
                    return false;
                }

                return state.TranscriptAvailable
                    ? await IsTranscriptValidAsync(
                        videoId,
                        transcriptPath,
                        transcriptTextPath,
                        jsonOptions,
                        cancellationToken)
                    : !File.Exists(transcriptPath) && !File.Exists(transcriptTextPath);
            }

            return await IsTranscriptValidAsync(
                videoId,
                transcriptPath,
                transcriptTextPath,
                jsonOptions,
                cancellationToken);
        }
        catch (JsonException exception)
        {
            Console.Error.WriteLine(
                $"Cached output is invalid for video {videoId}: {exception.Message}");
            return false;
        }
        catch (OverflowException exception)
        {
            Console.Error.WriteLine(
                $"Cached comment totals are invalid for video {videoId}: {exception.Message}");
            return false;
        }
    }

    public static void ClearCompletionState(string outputDirectory)
    {
        var statePath = Path.Combine(outputDirectory, StateFileName);

        if (File.Exists(statePath))
        {
            File.Delete(statePath);
        }
    }

    public static async Task MarkCompleteAsync(
        string videoId,
        bool transcriptAvailable,
        string outputDirectory,
        JsonSerializerOptions jsonOptions,
        CancellationToken cancellationToken = default)
    {
        var state = new VideoDownloadState
        {
            VideoId = videoId,
            TranscriptAvailable = transcriptAvailable
        };

        await File.WriteAllTextAsync(
            Path.Combine(outputDirectory, StateFileName),
            JsonSerializer.Serialize(state, jsonOptions),
            cancellationToken);
    }

    private static async Task<bool> IsTranscriptValidAsync(
        string videoId,
        string transcriptPath,
        string transcriptTextPath,
        JsonSerializerOptions jsonOptions,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(transcriptPath) || !File.Exists(transcriptTextPath))
        {
            return false;
        }

        var transcript = JsonSerializer.Deserialize<VideoTranscript>(
            await File.ReadAllTextAsync(transcriptPath, cancellationToken),
            jsonOptions);

        if (transcript is null
            || transcript.VideoId != videoId
            || transcript.Segments is null)
        {
            return false;
        }

        var expectedText = string.Join(
            Environment.NewLine,
            transcript.Segments.Select(segment => segment.Text));
        var actualText = await File.ReadAllTextAsync(transcriptTextPath, cancellationToken);

        return actualText == expectedText;
    }
}
