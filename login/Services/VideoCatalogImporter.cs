using System.Runtime.InteropServices;
using login.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace login.Services;

public sealed record VideoCatalogImportSummary(
    CorpusInventory Inventory,
    int Inserted,
    int Updated,
    int Skipped,
    int SemanticIndexesWritten = 0,
    int SemanticIndexesRemoved = 0,
    int SemanticIndexesPending = 0);

public sealed class VideoCatalogImporter(
    CorpusInventoryReader inventoryReader,
    ApplicationDbContext dbContext,
    IWebHostEnvironment environment,
    IOptions<SqliteVectorOptions> vectorOptions,
    ILogger<VideoCatalogImporter> logger)
{
    public async Task<VideoCatalogImportSummary> ImportAsync(CancellationToken cancellationToken = default)
    {
        var inventory = inventoryReader.Read();
        var existingVideos = await dbContext.Videos.ToDictionaryAsync(
            video => video.VideoId,
            cancellationToken);
        // Projection without the embedding blob keeps the startup scan cheap.
        var existingIndexes = await dbContext.VideoSemanticIndexes
            .AsNoTracking()
            .Select(index => new ExistingIndex(
                index.VideoId,
                index.TranscriptHash,
                index.SummaryModel,
                index.EmbeddingModel,
                index.GeneratedAt))
            .ToDictionaryAsync(index => index.VideoId, cancellationToken);
        var inserted = 0;
        var updated = 0;
        var skipped = inventory.OmittedByReason.Values.Sum();
        var indexesWritten = 0;
        var indexesRemoved = 0;
        var indexesPending = 0;

        foreach (var item in inventory.Videos)
        {
            var transcriptChanged = false;

            if (!existingVideos.TryGetValue(item.VideoId, out var existing))
            {
                dbContext.Videos.Add(new VideoCatalogItem
                {
                    VideoId = item.VideoId,
                    Title = item.Title,
                    TranscriptText = item.TranscriptText,
                    Categories = item.Categories,
                    Keywords = item.Keywords,
                    Materials = item.Materials.ToList(),
                    Equipment = item.Equipment.ToList()
                });
                inserted++;
            }
            else if (existing.TranscriptText == item.TranscriptText &&
                existing.Title == item.Title &&
                existing.Categories == item.Categories &&
                existing.Keywords == item.Keywords &&
                existing.Materials.SequenceEqual(item.Materials, StringComparer.Ordinal) &&
                existing.Equipment.SequenceEqual(item.Equipment, StringComparer.Ordinal))
            {
                skipped++;
            }
            else
            {
                transcriptChanged = existing.TranscriptText != item.TranscriptText;
                existing.Title = item.Title;
                existing.TranscriptText = item.TranscriptText;
                existing.Categories = item.Categories;
                existing.Keywords = item.Keywords;
                existing.Materials = item.Materials.ToList();
                existing.Equipment = item.Equipment.ToList();
                updated++;
            }

            // The semantic index is reconciled even when the catalog row did not change,
            // so existing videos are backfilled.
            existingIndexes.TryGetValue(item.VideoId, out var currentIndex);

            if (item.SemanticIndex is not { } artifact)
            {
                if (currentIndex is not null && transcriptChanged)
                {
                    dbContext.VideoSemanticIndexes.Remove(new VideoSemanticIndex { VideoId = item.VideoId });
                    indexesRemoved++;
                    currentIndex = null;
                }

                if (currentIndex is null)
                {
                    indexesPending++;
                }

                continue;
            }

            if (artifact.Embedding.Length != vectorOptions.Value.Dimensions)
            {
                logger.LogWarning(
                    "Semantic index for {VideoId} has {Dimensions} dimensions but {Expected} are configured; it was not imported.",
                    item.VideoId,
                    artifact.Embedding.Length,
                    vectorOptions.Value.Dimensions);
                indexesPending++;
                continue;
            }

            if (currentIndex is not null &&
                currentIndex.TranscriptHash == artifact.TranscriptHash &&
                currentIndex.SummaryModel == artifact.SummaryModel &&
                currentIndex.EmbeddingModel == artifact.EmbeddingModel &&
                currentIndex.GeneratedAt == artifact.GeneratedAt)
            {
                continue;
            }

            var entity = new VideoSemanticIndex
            {
                VideoId = item.VideoId,
                Summary = artifact.Summary,
                Embedding = MemoryMarshal.AsBytes(artifact.Embedding.AsSpan()).ToArray(),
                TranscriptHash = artifact.TranscriptHash,
                SummaryModel = artifact.SummaryModel,
                EmbeddingModel = artifact.EmbeddingModel,
                EmbeddingDimensions = artifact.Embedding.Length,
                GeneratedAt = artifact.GeneratedAt
            };

            if (currentIndex is null)
            {
                dbContext.VideoSemanticIndexes.Add(entity);
            }
            else
            {
                dbContext.Entry(entity).State = EntityState.Modified;
            }

            indexesWritten++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        if (environment.IsDevelopment())
        {
            logger.LogInformation(
                "Video catalog import: {InsertedCount} inserted, {UpdatedCount} updated, {SkippedCount} skipped. Inventory: {FolderCount} folders and {ValidCount} usable transcripts. Omitted by reason: {OmittedByReason}",
                inserted,
                updated,
                skipped,
                inventory.FolderCount,
                inventory.Videos.Count,
                string.Join(", ", inventory.OmittedByReason.Select(item => $"{item.Key}={item.Value}")));
        }

        if (indexesWritten > 0 || indexesRemoved > 0 || indexesPending > 0)
        {
            logger.LogInformation(
                "Semantic index sync: {WrittenCount} written, {RemovedCount} removed, {PendingCount} videos still without an up-to-date index.",
                indexesWritten,
                indexesRemoved,
                indexesPending);
        }

        return new VideoCatalogImportSummary(
            inventory,
            inserted,
            updated,
            skipped,
            indexesWritten,
            indexesRemoved,
            indexesPending);
    }

    private sealed record ExistingIndex(
        string VideoId,
        string TranscriptHash,
        string SummaryModel,
        string EmbeddingModel,
        DateTimeOffset GeneratedAt);
}
