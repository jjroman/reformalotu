namespace YoutubeAnalyzer.Models;

public sealed record YoutubeComment
{
    public string Author { get; init; } = "";
    public string Text { get; init; } = "";
    public long Likes { get; init; }
    public DateTimeOffset? PublishedAt { get; init; }
    public List<YoutubeComment> Replies { get; init; } = [];
}
