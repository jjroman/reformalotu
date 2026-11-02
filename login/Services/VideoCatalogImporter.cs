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
        var existingVideos = await dbContext.Videos
            .Include(video => video.Inputs)
            .Include(video => video.Tasks)
            .ToDictionaryAsync(
            video => video.VideoId,
            cancellationToken);
        // Projection without the embedding blob keeps the startup scan cheap.
        var existingIndexes = await dbContext.VideoSemanticIndexes
            .AsNoTracking()
            .Select(index => new ExistingIndex(
                index.VideoId,
                index.TranscriptHash,
                index.PromptVersion,
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
                existing = new VideoCatalogItem
                {
                    VideoId = item.VideoId,
                    Title = item.Title,
                    TranscriptText = item.TranscriptText,
                    Categories = item.Categories,
                    Keywords = item.Keywords
                };
                ApplyAnalysis(existing, item.SemanticIndex);
                dbContext.Videos.Add(existing);
                existingVideos.Add(existing.VideoId, existing);
                inserted++;
            }
            else if (existing.TranscriptText == item.TranscriptText &&
                existing.Title == item.Title &&
                existing.Categories == item.Categories &&
                existing.Keywords == item.Keywords)
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
                if (transcriptChanged && item.SemanticIndex is null)
                {
                    ApplyAnalysis(existing, null);
                }
                updated++;
            }

            if (item.SemanticIndex is { } analysis &&
                (existing.AnalysisTranscriptHash != analysis.TranscriptHash ||
                 existing.AnalysisPromptVersion != analysis.PromptVersion))
            {
                ApplyAnalysis(existing, analysis);
            }

            // The semantic index is reconciled even when the catalog row did not change,
            // so existing videos are backfilled.
            existingIndexes.TryGetValue(item.VideoId, out var currentIndex);

            if (item.SemanticIndex is not { } artifact)
            {
                if (currentIndex is not null && transcriptChanged)
                {
                    RemoveSemanticIndex(item.VideoId);
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
                currentIndex.PromptVersion == artifact.PromptVersion &&
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
                PromptVersion = artifact.PromptVersion,
                SummaryModel = artifact.SummaryModel,
                EmbeddingModel = artifact.EmbeddingModel,
                EmbeddingDimensions = artifact.Embedding.Length,
                GeneratedAt = artifact.GeneratedAt
            };

            if (currentIndex is null)
            {
                dbContext.VideoSemanticIndexes.Add(entity);
            }
            else if (dbContext.ChangeTracker.Entries<VideoSemanticIndex>()
                         .FirstOrDefault(entry => entry.Entity.VideoId == item.VideoId) is { } trackedIndex)
            {
                trackedIndex.CurrentValues.SetValues(entity);
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
        int PromptVersion,
        string SummaryModel,
        string EmbeddingModel,
        DateTimeOffset GeneratedAt);

    private static void ApplyAnalysis(VideoCatalogItem video, CorpusSemanticIndex? analysis)
    {
        video.Inputs.Clear();
        video.Tasks.Clear();
        video.AnalysisTranscriptHash = analysis?.TranscriptHash;
        video.AnalysisPromptVersion = analysis?.PromptVersion;

        if (analysis is null)
        {
            return;
        }

        foreach (var input in analysis.Inputs)
        {
            video.Inputs.Add(new VideoInput
            {
                VideoId = video.VideoId,
                SourceId = input.SourceId,
                Kind = input.Kind,
                Name = input.Name,
                Brand = input.Brand,
                Specification = input.Specification,
                Quantity = input.Quantity,
                Unit = input.Unit,
                Category = input.Category,
                Level = input.Level,
                Headcount = input.Headcount,
                EstimatedHours = input.EstimatedHours,
                Notes = input.Notes,
                Confidence = input.Confidence
            });
        }

        foreach (var task in analysis.Tasks)
        {
            video.Tasks.Add(new VideoTaskDefinition
            {
                VideoId = video.VideoId,
                Order = task.Order,
                Title = task.Title,
                Description = task.Description,
                EstimatedMinutes = task.EstimatedMinutes,
                MaterialIds = task.MaterialIds.ToList(),
                EquipmentIds = task.EquipmentIds.ToList(),
                LaborIds = task.LaborIds.ToList(),
                Confidence = task.Confidence
            });
        }
    }

    private void RemoveSemanticIndex(string videoId)
    {
        var trackedIndex = dbContext.ChangeTracker.Entries<VideoSemanticIndex>()
            .FirstOrDefault(entry => entry.Entity.VideoId == videoId);
        if (trackedIndex is not null)
        {
            trackedIndex.State = EntityState.Deleted;
            return;
        }

        dbContext.VideoSemanticIndexes.Remove(new VideoSemanticIndex { VideoId = videoId });
    }
}
