namespace login.Data;

public sealed class VideoInput
{
    public string VideoId { get; set; } = string.Empty;
    public VideoCatalogItem Video { get; set; } = null!;
    public string SourceId { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public string? Specification { get; set; }
    public double? Quantity { get; set; }
    public string? Unit { get; set; }
    public string? Category { get; set; }
    public string? Level { get; set; }
    public int? Headcount { get; set; }
    public double? EstimatedHours { get; set; }
    public string? Notes { get; set; }
    public int Confidence { get; set; }
}
