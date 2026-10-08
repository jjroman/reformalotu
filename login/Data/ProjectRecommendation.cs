namespace login.Data;

public sealed class ProjectRecommendation
{
    public Guid ProjectId { get; set; }
    public ReformProject Project { get; set; } = null!;
    public string VideoId { get; set; } = string.Empty;
    public VideoCatalogItem Video { get; set; } = null!;
    public int Position { get; set; }
    public int Score { get; set; }
}
