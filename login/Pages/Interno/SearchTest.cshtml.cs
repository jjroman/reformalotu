using System.Globalization;
using login.Data;
using login.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace login.Pages.Interno;

public sealed record SearchTestResult(string VideoId, string Title, string Score, string Summary);

/// <summary>Standalone sandbox to compare semantic and current search. Stores nothing.</summary>
[Authorize(Roles = "Admin")]
public sealed class SearchTestModel(
    ApplicationDbContext dbContext,
    VideoRecommendationService recommendationService,
    IServiceProvider services,
    IOptions<SemanticSearchOptions> options,
    ILogger<SearchTestModel> logger) : PageModel
{
    private const int MaxQueryLength = 500;

    [BindProperty(SupportsGet = true)]
    public string? Q { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool Semantic { get; set; }

    /// <summary>Optional threshold override (cosine similarity) used to calibrate the default.</summary>
    [BindProperty(SupportsGet = true)]
    public double? Threshold { get; set; }

    public double DefaultThreshold => options.Value.MinimumSimilarity;
    public bool Searched { get; private set; }
    public string? ErrorMessage { get; private set; }
    public double? BestSimilarity { get; private set; }
    public IReadOnlyList<SearchTestResult> Results { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Response.Headers["X-Robots-Tag"] = "noindex, nofollow";

        var query = Q?.Trim();
        if (string.IsNullOrEmpty(query))
        {
            return;
        }

        if (query.Length > MaxQueryLength)
        {
            ErrorMessage = $"La consulta no puede superar {MaxQueryLength} caracteres.";
            return;
        }

        Searched = true;
        try
        {
            Results = Semantic
                ? await SearchSemanticAsync(query, cancellationToken)
                : await SearchCurrentAsync(query, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Search test failed (semantic: {Semantic}).", Semantic);
            ErrorMessage = "No se pudo completar la búsqueda. Revisa los registros del servidor.";
            Results = [];
        }
    }

    private async Task<IReadOnlyList<SearchTestResult>> SearchSemanticAsync(
        string query,
        CancellationToken cancellationToken)
    {
        var search = services.GetService<SemanticVideoSearchService>();
        if (services.GetService<IQueryEmbeddingClient>() is null || search is null)
        {
            ErrorMessage = "La búsqueda semántica no está configurada (falta SemanticSearch:Endpoint).";
            return [];
        }

        var result = await search.SearchAsync(query, Threshold, cancellationToken);
        BestSimilarity = result.BestSimilarity;
        return result.Matches
            .Select(match => new SearchTestResult(
                match.VideoId,
                match.Title,
                match.Similarity.ToString("0.000", CultureInfo.InvariantCulture),
                match.Summary))
            .ToArray();
    }

    private async Task<IReadOnlyList<SearchTestResult>> SearchCurrentAsync(
        string query,
        CancellationToken cancellationToken)
    {
        var recommendations = await recommendationService.RecommendAsync(query, null, cancellationToken);
        var ids = recommendations.Select(item => item.Video.VideoId).ToArray();
        var summaries = await dbContext.VideoSemanticIndexes
            .AsNoTracking()
            .Where(index => ids.Contains(index.VideoId))
            .ToDictionaryAsync(index => index.VideoId, index => index.Summary, StringComparer.Ordinal, cancellationToken);

        return recommendations
            .Select(item => new SearchTestResult(
                item.Video.VideoId,
                item.Video.Title,
                item.Score.ToString(CultureInfo.InvariantCulture),
                summaries.GetValueOrDefault(item.Video.VideoId, item.Explanation)))
            .ToArray();
    }
}
