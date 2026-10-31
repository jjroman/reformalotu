namespace YoutubeAnalyzer.Models;

public sealed record VideoDownloadState
{
    public string VideoId { get; init; } = "";
    public bool TranscriptAvailable { get; init; }
    public string Channel { get; init; } = "";

    // Count reported by YouTube at download time; avoids re-downloading forever when it never matches the fetched total.
    public long? RemoteCommentCount { get; init; }
}
