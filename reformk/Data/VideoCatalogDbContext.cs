using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Reformk.Data;

public sealed class VideoCatalogDbContext(DbContextOptions<VideoCatalogDbContext> options) : DbContext(options)
{
    private static readonly ValueComparer<List<string>> StringListComparer = new(
        (left, right) => left != null && right != null
            ? left.SequenceEqual(right, StringComparer.Ordinal)
            : left == right,
        value => value.Aggregate(0, (hash, item) => HashCode.Combine(
            hash, item == null ? 0 : StringComparer.Ordinal.GetHashCode(item))),
        value => value.ToList());

    public DbSet<VideoCatalogItem> Videos => Set<VideoCatalogItem>();
    public DbSet<VideoSemanticIndex> VideoSemanticIndexes => Set<VideoSemanticIndex>();
    public DbSet<VideoInput> VideoInputs => Set<VideoInput>();
    public DbSet<VideoTaskDefinition> VideoTaskDefinitions => Set<VideoTaskDefinition>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<VideoCatalogItem>(entity =>
        {
            entity.HasKey(video => video.VideoId);
            entity.Property(video => video.VideoId).HasMaxLength(11);
            entity.Property(video => video.Title).IsRequired().HasMaxLength(300).HasDefaultValue(string.Empty);
            entity.Property(video => video.TranscriptText).IsRequired();
            entity.Property(video => video.Categories).IsRequired();
            entity.Property(video => video.Keywords).IsRequired();
            entity.HasMany(video => video.Inputs).WithOne(input => input.Video)
                .HasForeignKey(input => input.VideoId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(video => video.Tasks).WithOne(task => task.Video)
                .HasForeignKey(task => task.VideoId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(video => video.SemanticIndex).WithOne(index => index.Video)
                .HasForeignKey<VideoSemanticIndex>(index => index.VideoId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<VideoSemanticIndex>(entity =>
        {
            entity.HasKey(index => index.VideoId);
            entity.Property(index => index.VideoId).HasMaxLength(11);
            entity.Property(index => index.Summary).IsRequired();
            entity.Property(index => index.Embedding).IsRequired();
            entity.Property(index => index.TranscriptHash).IsRequired().HasMaxLength(64);
            entity.Property(index => index.PromptVersion).IsRequired();
            entity.Property(index => index.SummaryModel).IsRequired().HasMaxLength(200);
            entity.Property(index => index.EmbeddingModel).IsRequired().HasMaxLength(200);
        });

        builder.Entity<VideoInput>(entity =>
        {
            entity.HasKey(input => new { input.VideoId, input.SourceId });
            entity.Property(input => input.Name).IsRequired().HasMaxLength(300);
            entity.Property(input => input.Kind).IsRequired().HasMaxLength(20);
        });

        builder.Entity<VideoTaskDefinition>(entity =>
        {
            entity.HasKey(task => new { task.VideoId, task.Order });
            entity.Property(task => task.Title).IsRequired().HasMaxLength(300);
            entity.Property(task => task.Description).IsRequired();
            foreach (var property in new[] { nameof(VideoTaskDefinition.MaterialIds), nameof(VideoTaskDefinition.EquipmentIds), nameof(VideoTaskDefinition.LaborIds) })
            {
                entity.Property<List<string>>(property)
                    .HasConversion(
                        ids => JsonSerializer.Serialize(ids, (JsonSerializerOptions?)null),
                        json => JsonSerializer.Deserialize<List<string>>(json, (JsonSerializerOptions?)null) ?? new List<string>())
                    .IsRequired()
                    .Metadata.SetValueComparer(StringListComparer);
            }
        });
    }
}
