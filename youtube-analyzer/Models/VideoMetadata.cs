namespace YoutubeAnalyzer.Models;

public sealed record VideoMetadata
{
    public string VideoId { get; init; } = "";
    public string Title { get; init; } = "";
    public string ChannelId { get; init; } = "";
    public string ChannelTitle { get; init; } = "";
    public DateTimeOffset? PublishedAt { get; init; }
}
