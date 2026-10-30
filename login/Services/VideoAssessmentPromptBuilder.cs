using System.Text.Json;

namespace login.Services;

public static class VideoAssessmentPromptBuilder
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static string BuildUserPrompt(
        VideoAssessmentRequest request,
        VideoAssessmentOptions options)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(request.Candidates);

        var boundedCandidates = request.Candidates
            .Select(candidate =>
            {
                ArgumentNullException.ThrowIfNull(candidate);
                var transcriptLength = Math.Min(
                    candidate.TranscriptExcerpt.Length,
                    options.MaxTranscriptCharacters);
                return candidate with
                {
                    TranscriptExcerpt = candidate.TranscriptExcerpt[..transcriptLength]
                };
            })
            .ToArray();

        var prompt = Serialize(request, boundedCandidates);
        if (prompt.Length <= options.MaxContextCharacters)
        {
            return prompt;
        }

        var lowerBound = 0;
        var upperBound = options.MaxTranscriptCharacters;
        while (lowerBound < upperBound)
        {
            var midpoint = lowerBound + (upperBound - lowerBound + 1) / 2;
            var candidatePrompt = Serialize(
                request,
                boundedCandidates.Select((candidate, index) =>
                    candidate with
                    {
                        TranscriptExcerpt = request.Candidates[index].TranscriptExcerpt[
                            ..Math.Min(request.Candidates[index].TranscriptExcerpt.Length, midpoint)]
                    }).ToArray());

            if (candidatePrompt.Length <= options.MaxContextCharacters)
            {
                lowerBound = midpoint;
                prompt = candidatePrompt;
            }
            else
            {
                upperBound = midpoint - 1;
            }
        }

        prompt = Serialize(
            request,
            boundedCandidates.Select((candidate, index) =>
                candidate with
                {
                    TranscriptExcerpt = request.Candidates[index].TranscriptExcerpt[
                        ..Math.Min(request.Candidates[index].TranscriptExcerpt.Length, lowerBound)]
                }).ToArray());

        if (prompt.Length > options.MaxContextCharacters)
        {
            throw new InvalidOperationException(
                "Video assessment project and catalog data exceed the configured context limit.");
        }

        return prompt;
    }

    private static string Serialize(
        VideoAssessmentRequest request,
        IReadOnlyList<VideoAssessmentCandidate> candidates) =>
        JsonSerializer.Serialize(
            new VideoAssessmentRequest(
                request.ProjectDescription,
                request.ProjectCategory,
                candidates),
            SerializerOptions);
}
