namespace Reformk.Data;

public sealed class VideoTaskDefinition
{
    public string VideoId { get; set; } = string.Empty;
    public VideoCatalogItem Video { get; set; } = null!;
    public int Order { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double? EstimatedMinutes { get; set; }
    public List<string> MaterialIds { get; set; } = [];
    public List<string> EquipmentIds { get; set; } = [];
    public List<string> LaborIds { get; set; } = [];
    public int Confidence { get; set; }
}
