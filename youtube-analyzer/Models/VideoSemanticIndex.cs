namespace YoutubeAnalyzer.Models;

public sealed record VideoSemanticIndex
{
    public string VideoId { get; init; } = "";
    public string Summary { get; init; } = "";
    public float[] Embedding { get; init; } = [];
    public string TranscriptHash { get; init; } = "";
    public string SummaryModel { get; init; } = "";

    /// <summary>Version of the summary/inputs/tasks prompt and schema that produced this artifact.</summary>
    public int PromptVersion { get; init; }
    public VideoInputs Inputs { get; init; } = new();
    public IReadOnlyList<VideoTask> Tasks { get; init; } = [];
    public string EmbeddingModel { get; init; } = "";
    public int EmbeddingDimensions { get; init; }
    public DateTimeOffset GeneratedAt { get; init; }
}
