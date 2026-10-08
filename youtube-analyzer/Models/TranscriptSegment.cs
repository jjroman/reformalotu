namespace YoutubeAnalyzer.Models;

public sealed record TranscriptSegment
{
    public double StartSeconds { get; init; }
    public double DurationSeconds { get; init; }
    public string Text { get; init; } = "";
}
