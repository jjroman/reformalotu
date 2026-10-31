using System.Globalization;
using System.Numerics.Tensors;
using System.Runtime.InteropServices;
using login.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace login.Services;

public sealed record SemanticMatch(string VideoId, string Title, string Summary, double Similarity);

/// <param name="Matches">Videos at or above the threshold, best first.</param>
/// <param name="BestSimilarity">Best similarity found even if below the threshold; null when the index is empty.</param>
public sealed record SemanticSearchResult(IReadOnlyList<SemanticMatch> Matches, double? BestSimilarity);

public sealed class SemanticVideoSearchService(
    ApplicationDbContext dbContext,
    IQueryEmbeddingClient embeddingClient,
    IOptions<SemanticSearchOptions> options,
    ILogger<SemanticVideoSearchService> logger)
{
    private sealed record Candidate(string VideoId, double Similarity);

    /// <summary>Embeds the query and returns the closest videos above the similarity threshold.</summary>
    public async Task<SemanticSearchResult> SearchAsync(
        string query,
        double? minimumSimilarity = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        var settings = options.Value;
        var threshold = minimumSimilarity ?? settings.MinimumSimilarity;
        var queryVector = await embeddingClient.EmbedAsync(query.Trim(), cancellationToken);

        var candidates = await FindClosestAsync(queryVector, Math.Max(settings.MaxResults, 5), cancellationToken);
        if (candidates.Count == 0)
        {
            return new SemanticSearchResult([], null);
        }

        var accepted = candidates
            .Where(candidate => candidate.Similarity >= threshold)
            .Take(settings.MaxResults)
            .ToArray();
        var ids = accepted.Select(candidate => candidate.VideoId).ToArray();
        var details = await dbContext.VideoSemanticIndexes
            .AsNoTracking()
            .Where(index => ids.Contains(index.VideoId))
            .Select(index => new { index.VideoId, index.Summary, Title = index.Video!.Title })
            .ToDictionaryAsync(index => index.VideoId, StringComparer.Ordinal, cancellationToken);

        var matches = accepted
            .Where(candidate => details.ContainsKey(candidate.VideoId))
            .Select(candidate =>
            {
                var detail = details[candidate.VideoId];
                return new SemanticMatch(candidate.VideoId, detail.Title, detail.Summary, candidate.Similarity);
            })
            .ToArray();
        return new SemanticSearchResult(matches, candidates[0].Similarity);
    }

    private async Task<IReadOnlyList<Candidate>> FindClosestAsync(
        float[] queryVector,
        int count,
        CancellationToken cancellationToken)
    {
        try
        {
            var vectorLiteral = "[" + string.Join(',', queryVector.Select(v => v.ToString("R", CultureInfo.InvariantCulture))) + "]";
            var rows = await dbContext.Database
                .SqlQuery<ScanRow>($"""
                    select e.VideoId as VideoId, v.distance as Distance
                    from VideoSemanticIndexes e
                    join vector_full_scan('VideoSemanticIndexes', 'Embedding', vector_as_f32({vectorLiteral}), {count}) v
                        on e.rowid = v.rowid
                    """)
                .ToListAsync(cancellationToken);
            return rows
                .Select(row => new Candidate(row.VideoId, 1 - row.Distance))
                .OrderByDescending(candidate => candidate.Similarity)
                .ToArray();
        }
        catch (SqliteException exception)
        {
            // Extension missing (e.g. vector.dll not deployed): exact scan in memory is fine for a small catalog.
            logger.LogWarning(exception, "sqlite-vector search failed; falling back to an in-memory cosine scan.");
            return await ScanInMemoryAsync(queryVector, count, cancellationToken);
        }
    }

    private async Task<IReadOnlyList<Candidate>> ScanInMemoryAsync(
        float[] queryVector,
        int count,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.VideoSemanticIndexes
            .AsNoTracking()
            .Select(index => new { index.VideoId, index.Embedding })
            .ToListAsync(cancellationToken);
        return rows
            .Where(row => row.Embedding.Length == queryVector.Length * sizeof(float))
            .Select(row => new Candidate(
                row.VideoId,
                TensorPrimitives.CosineSimilarity(queryVector, MemoryMarshal.Cast<byte, float>(row.Embedding))))
            .OrderByDescending(candidate => candidate.Similarity)
            .Take(count)
            .ToArray();
    }

    private sealed class ScanRow
    {
        public string VideoId { get; set; } = string.Empty;
        public double Distance { get; set; }
    }
}
