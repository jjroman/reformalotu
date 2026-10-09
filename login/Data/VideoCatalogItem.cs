namespace login.Data;

public sealed class VideoCatalogItem
{
    public string VideoId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string TranscriptText { get; set; } = string.Empty;
    public string Categories { get; set; } = string.Empty;
    public string Keywords { get; set; } = string.Empty;
    public List<string> Materials { get; set; } = [];
    public List<string> Equipment { get; set; } = [];
    public long OpenCount { get; set; }
}
