namespace YoutubeAnalyzer.Models;

public sealed record AnalysisResult
{
    public int TotalComments { get; init; }
    public long TotalLikes { get; init; }
    public double AverageLikesPerComment { get; init; }
}
