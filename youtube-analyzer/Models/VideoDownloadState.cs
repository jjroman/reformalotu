namespace YoutubeAnalyzer.Models;

public sealed record VideoDownloadState
{
    public string VideoId { get; init; } = "";
    public bool TranscriptAvailable { get; init; }
}
