namespace Reformk.Data;

public sealed class VideoSemanticIndex
{
    public string VideoId { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;

    /// <summary>FLOAT32 vector as little-endian bytes, compatible with sqlite-vector.</summary>
    public byte[] Embedding { get; set; } = [];

    public string TranscriptHash { get; set; } = string.Empty;
    public int PromptVersion { get; set; }
    public string SummaryModel { get; set; } = string.Empty;
    public string EmbeddingModel { get; set; } = string.Empty;
    public int EmbeddingDimensions { get; set; }
    public DateTimeOffset GeneratedAt { get; set; }
    public VideoCatalogItem? Video { get; set; }
}
