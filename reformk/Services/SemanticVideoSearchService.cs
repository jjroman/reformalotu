using System.Globalization;
using System.Numerics.Tensors;
using System.Runtime.InteropServices;
using Reformk.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Reformk.Services;

public sealed record SemanticMatch(
    string VideoId,
    string Title,
    string Summary,
    double Similarity,
    int MaterialCount = 0,
    int EquipmentCount = 0,
    int LaborCount = 0);

/// <param name="Matches">Videos at or above the threshold, best first.</param>
/// <param name="BestSimilarity">Best similarity found even if below the threshold; null when the index is empty.</param>
public sealed record SemanticSearchResult(
    IReadOnlyList<SemanticMatch> Matches,
    double? BestSimilarity,
    bool HasMore = false);

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
        return await SearchPageAsync(
            query,
            0,
            options.Value.MaxResults,
            minimumSimilarity,
            cancellationToken);
    }

    public async Task<SemanticSearchResult> SearchPageAsync(
        string query,
        int skip,
        int take,
        double? minimumSimilarity = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        ArgumentOutOfRangeException.ThrowIfNegative(skip);
        if (take is < 1 or > 10_000)
        {
            throw new ArgumentOutOfRangeException(nameof(take), "Page size must be between 1 and 10000.");
        }
        if (skip > 10_000 || (long)skip + take > 10_000)
        {
            throw new ArgumentOutOfRangeException(nameof(skip), "Search result window exceeds the maximum supported size.");
        }

        var settings = options.Value;
        var threshold = minimumSimilarity ?? settings.MinimumSimilarity;
        if (!await dbContext.VideoSemanticIndexes.AsNoTracking().AnyAsync(cancellationToken))
        {
            return new SemanticSearchResult([], null);
        }

        var queryVector = await embeddingClient.EmbedAsync(query.Trim(), cancellationToken);

        var candidates = await FindClosestAsync(
            queryVector,
            Math.Max(settings.MaxResults, checked(skip + take + 1)),
            cancellationToken);
        if (candidates.Count == 0)
        {
            return new SemanticSearchResult([], null);
        }

        var accepted = candidates
            .Where(candidate => candidate.Similarity >= threshold)
            .Skip(skip)
            .Take(take + 1)
            .ToArray();
        var hasMore = accepted.Length > take;
        var page = accepted.Take(take).ToArray();
        var ids = page.Select(candidate => candidate.VideoId).ToArray();
        var details = await dbContext.VideoSemanticIndexes
            .AsNoTracking()
            .Where(index => ids.Contains(index.VideoId))
            .Select(index => new
            {
                index.VideoId,
                index.Summary,
                Title = index.Video!.Title,
                MaterialCount = index.Video.Inputs.Count(input => input.Kind == "material"),
                EquipmentCount = index.Video.Inputs.Count(input => input.Kind == "equipment"),
                LaborCount = index.Video.Inputs.Count(input => input.Kind == "labor")
            })
            .ToDictionaryAsync(index => index.VideoId, StringComparer.Ordinal, cancellationToken);

        var matches = page
            .Where(candidate => details.ContainsKey(candidate.VideoId))
            .Select(candidate =>
            {
                var detail = details[candidate.VideoId];
                return new SemanticMatch(
                    candidate.VideoId,
                    detail.Title,
                    detail.Summary,
                    candidate.Similarity,
                    detail.MaterialCount,
                    detail.EquipmentCount,
                    detail.LaborCount);
            })
            .ToArray();
        return new SemanticSearchResult(matches, candidates[0].Similarity, hasMore);
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
                .ThenBy(candidate => candidate.VideoId, StringComparer.Ordinal)
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
            .ThenBy(candidate => candidate.VideoId, StringComparer.Ordinal)
            .Take(count)
            .ToArray();
    }

    private sealed class ScanRow
    {
        public string VideoId { get; set; } = string.Empty;
        public double Distance { get; set; }
    }
}
