using System.Text.RegularExpressions;
using login.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace login.Services;

public sealed record VideoRecommendation(VideoCatalogItem Video, int Score);

public sealed class VideoRecommendationService(
    ApplicationDbContext dbContext,
    IOptions<VideoCatalogOptions> options)
{
    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
    {
        "a", "al", "algo", "con", "como", "de", "del", "desde", "el", "en", "es",
        "esta", "este", "la", "las", "lo", "los", "mi", "para", "por", "que", "se",
        "sin", "su", "un", "una", "y"
    };

    public async Task<IReadOnlyList<VideoRecommendation>> RecommendAsync(
        string description,
        string? category,
        CancellationToken cancellationToken = default)
    {
        var queryTerms = GetTerms(description);
        var categoryOptions = options.Value.Categories.FirstOrDefault(
            item => string.Equals(item.Name, category, StringComparison.OrdinalIgnoreCase));
        if (categoryOptions is not null)
        {
            foreach (var term in categoryOptions.Keywords.SelectMany(GetTerms))
            {
                queryTerms.Add(term);
            }
        }

        if (queryTerms.Count == 0)
        {
            return [];
        }

        var videos = await dbContext.Videos.AsNoTracking().ToListAsync(cancellationToken);
        return videos
            .Select(video => new VideoRecommendation(
                video,
                GetTerms(video.TranscriptText).Count(queryTerms.Contains)))
            .Where(recommendation => recommendation.Score > 0)
            .OrderByDescending(recommendation => recommendation.Score)
            .ThenBy(recommendation => recommendation.Video.VideoId, StringComparer.Ordinal)
            .Take(3)
            .ToArray();
    }

    private static HashSet<string> GetTerms(string value) =>
        Regex.Split(TextNormalizer.Normalize(value), @"[^\p{L}\p{N}]+")
            .Where(term => term.Length > 2 && !StopWords.Contains(term))
            .ToHashSet(StringComparer.Ordinal);
}
