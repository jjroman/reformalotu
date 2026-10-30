namespace login.Services;

public sealed class VideoAssessmentOptions
{
    public const string SectionName = "VideoAssessment";

    public bool Enabled { get; set; }

    public string? Endpoint { get; init; }

    public string? Deployment { get; init; }

    public int MinimumRelevanceScore { get; init; } = VideoAssessmentPolicy.MinimumRelevanceScore;

    public int MaxCandidates { get; init; } = 12;

    public int MaxContextCharacters { get; init; } = 24_000;

    public int MaxTranscriptCharacters { get; init; } = 4_000;
}
