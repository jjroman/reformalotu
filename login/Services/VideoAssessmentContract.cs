using System.Text.Json;

namespace login.Services;

public sealed record VideoAssessmentRequest(
    string ProjectDescription,
    string? ProjectCategory,
    IReadOnlyList<VideoAssessmentCandidate> Candidates);

public sealed record VideoAssessmentCandidate(
    string VideoId,
    string Title,
    string Categories,
    string Keywords,
    IReadOnlyList<string> Materials,
    IReadOnlyList<string> Equipment,
    string TranscriptExcerpt);

public sealed record VideoAssessmentMatch(string VideoId, int Score, string Explanation);

public sealed record VideoAssessmentResponse(IReadOnlyList<VideoAssessmentMatch> Matches);

public static class VideoAssessmentPolicy
{
    public const int MinimumRelevanceScore = 70;

    public static bool MeetsMinimumScore(int score) => score >= MinimumRelevanceScore;
}

public static class VideoAssessmentResponseValidator
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static VideoAssessmentResponse ParseAndValidate(
        string? json,
        IReadOnlyCollection<VideoAssessmentCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        if (string.IsNullOrWhiteSpace(json))
        {
            throw new JsonException("The assessment response is empty.");
        }

        var candidateIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var candidate in candidates)
        {
            if (candidate is null ||
                string.IsNullOrWhiteSpace(candidate.VideoId) ||
                !candidateIds.Add(candidate.VideoId))
            {
                throw new ArgumentException(
                    "Candidate video IDs must be non-empty and unique.",
                    nameof(candidates));
            }
        }

        var parsed = JsonSerializer.Deserialize<ResponsePayload>(json, SerializerOptions);
        if (parsed?.Matches is null)
        {
            throw new JsonException("The assessment response must contain a matches array.");
        }

        var matches = new List<VideoAssessmentMatch>(parsed.Matches.Count);
        var assessedIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var match in parsed.Matches)
        {
            if (match is null ||
                string.IsNullOrWhiteSpace(match.VideoId) ||
                match.Score is null ||
                string.IsNullOrWhiteSpace(match.Explanation))
            {
                throw new JsonException("Each assessment match must contain an ID, score, and explanation.");
            }

            if (match.Score is < 0 or > 100)
            {
                throw new JsonException("Assessment scores must be between 0 and 100.");
            }

            if (!candidateIds.Contains(match.VideoId))
            {
                throw new JsonException("The assessment contains an ID outside the supplied candidates.");
            }

            if (!assessedIds.Add(match.VideoId))
            {
                throw new JsonException("The assessment contains duplicate video IDs.");
            }

            matches.Add(new VideoAssessmentMatch(match.VideoId, match.Score.Value, match.Explanation));
        }

        return new VideoAssessmentResponse(matches);
    }

    private sealed class ResponsePayload
    {
        public List<MatchPayload?>? Matches { get; init; }
    }

    private sealed class MatchPayload
    {
        public string? VideoId { get; init; }

        public int? Score { get; init; }

        public string? Explanation { get; init; }
    }
}
