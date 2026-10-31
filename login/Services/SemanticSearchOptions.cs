namespace login.Services;

public sealed class SemanticSearchOptions
{
    public const string SectionName = "SemanticSearch";

    /// <summary>Azure OpenAI endpoint; falls back to the VideoAssessment endpoint when empty.</summary>
    public string? Endpoint { get; init; }

    /// <summary>Embedding deployment; must be the same model that generated the stored vectors.</summary>
    public string EmbeddingDeployment { get; init; } = "text-embedding-3-small";

    /// <summary>
    /// Cosine similarity (1 - cosine distance) below which a video is not considered a good match.
    /// Calibrate it with real queries on the test page before using it elsewhere.
    /// </summary>
    public double MinimumSimilarity { get; init; } = 0.35;

    public int MaxResults { get; init; } = 3;
}
