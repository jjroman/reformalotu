using login.Data;
using Microsoft.EntityFrameworkCore;

namespace login.Services;

public sealed record VideoCatalogImportSummary(
    CorpusInventory Inventory,
    int Inserted,
    int Updated,
    int Skipped);

public sealed class VideoCatalogImporter(
    CorpusInventoryReader inventoryReader,
    ApplicationDbContext dbContext,
    IWebHostEnvironment environment,
    ILogger<VideoCatalogImporter> logger)
{
    public async Task<VideoCatalogImportSummary> ImportAsync(CancellationToken cancellationToken = default)
    {
        var inventory = inventoryReader.Read();
        var existingVideos = await dbContext.Videos.ToDictionaryAsync(
            video => video.VideoId,
            cancellationToken);
        var inserted = 0;
        var updated = 0;
        var skipped = inventory.OmittedByReason.Values.Sum();

        foreach (var item in inventory.Videos)
        {
            if (!existingVideos.TryGetValue(item.VideoId, out var existing))
            {
                dbContext.Videos.Add(new VideoCatalogItem
                {
                    VideoId = item.VideoId,
                    TranscriptText = item.TranscriptText,
                    Categories = item.Categories,
                    Keywords = item.Keywords,
                    Materials = item.Materials.ToList(),
                    Equipment = item.Equipment.ToList()
                });
                inserted++;
                continue;
            }

            if (existing.TranscriptText == item.TranscriptText &&
                existing.Categories == item.Categories &&
            existing.Keywords == item.Keywords &&
            existing.Materials.SequenceEqual(item.Materials, StringComparer.Ordinal) &&
            existing.Equipment.SequenceEqual(item.Equipment, StringComparer.Ordinal))
            {
                skipped++;
                continue;
            }

            existing.TranscriptText = item.TranscriptText;
            existing.Categories = item.Categories;
            existing.Keywords = item.Keywords;
            existing.Materials = item.Materials.ToList();
            existing.Equipment = item.Equipment.ToList();
            updated++;
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

        return new VideoCatalogImportSummary(inventory, inserted, updated, skipped);
    }
}
