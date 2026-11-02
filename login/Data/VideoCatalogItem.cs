namespace login.Data;

public sealed class VideoCatalogItem
{
    public string VideoId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string TranscriptText { get; set; } = string.Empty;
    public string Categories { get; set; } = string.Empty;
    public string Keywords { get; set; } = string.Empty;
    public string? AnalysisTranscriptHash { get; set; }
    public int? AnalysisPromptVersion { get; set; }
    public long OpenCount { get; set; }
    public ICollection<VideoInput> Inputs { get; set; } = [];
    public ICollection<VideoTaskDefinition> Tasks { get; set; } = [];
    public VideoSemanticIndex? SemanticIndex { get; set; }
}
