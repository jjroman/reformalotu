using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace login.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext(options)
{
    public DbSet<VideoCatalogItem> Videos => Set<VideoCatalogItem>();
    public DbSet<VideoSemanticIndex> VideoSemanticIndexes => Set<VideoSemanticIndex>();
    public DbSet<ReformProject> Projects => Set<ReformProject>();
    public DbSet<VideoInput> VideoInputs => Set<VideoInput>();
    public DbSet<VideoTaskDefinition> VideoTaskDefinitions => Set<VideoTaskDefinition>();
    public DbSet<ProjectInput> ProjectInputs => Set<ProjectInput>();
    public DbSet<ProjectTask> ProjectTasks => Set<ProjectTask>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<VideoCatalogItem>(entity =>
        {
            entity.HasKey(video => video.VideoId);
            entity.Property(video => video.VideoId).HasMaxLength(11);
            entity.Property(video => video.Title).IsRequired().HasMaxLength(300).HasDefaultValue(string.Empty);
            entity.Property(video => video.TranscriptText).IsRequired();
            entity.Property(video => video.Categories).IsRequired();
            entity.Property(video => video.Keywords).IsRequired();
            entity.HasMany(video => video.Inputs)
                .WithOne(input => input.Video)
                .HasForeignKey(input => input.VideoId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(video => video.Tasks)
                .WithOne(task => task.Video)
                .HasForeignKey(task => task.VideoId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(video => video.SemanticIndex)
                .WithOne(index => index.Video)
                .HasForeignKey<VideoSemanticIndex>(index => index.VideoId)
                .OnDelete(DeleteBehavior.Cascade);
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

        builder.Entity<ReformProject>(entity =>
        {
            entity.HasKey(project => project.Id);
            entity.Property(project => project.Name).IsRequired().HasMaxLength(200);
            entity.Property(project => project.Description).IsRequired();
            entity.Property(project => project.OwnerId).IsRequired();
            entity.HasIndex(project => new { project.OwnerId, project.CreatedAt });
            entity.Property(project => project.WorkDescription);
            entity.HasOne(project => project.Owner)
                .WithMany()
                .HasForeignKey(project => project.OwnerId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(project => project.SelectedVideo)
                .WithMany()
                .HasForeignKey(project => project.SelectedVideoId)
                .OnDelete(DeleteBehavior.Restrict);
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
            entity.Property(task => task.MaterialIds)
                .HasConversion(
                    ids => System.Text.Json.JsonSerializer.Serialize(ids, (System.Text.Json.JsonSerializerOptions?)null),
                    json => System.Text.Json.JsonSerializer.Deserialize<List<string>>(json, (System.Text.Json.JsonSerializerOptions?)null) ?? new List<string>())
                .IsRequired();
            entity.Property(task => task.EquipmentIds)
                .HasConversion(
                    ids => System.Text.Json.JsonSerializer.Serialize(ids, (System.Text.Json.JsonSerializerOptions?)null),
                    json => System.Text.Json.JsonSerializer.Deserialize<List<string>>(json, (System.Text.Json.JsonSerializerOptions?)null) ?? new List<string>())
                .IsRequired();
            entity.Property(task => task.LaborIds)
                .HasConversion(
                    ids => System.Text.Json.JsonSerializer.Serialize(ids, (System.Text.Json.JsonSerializerOptions?)null),
                    json => System.Text.Json.JsonSerializer.Deserialize<List<string>>(json, (System.Text.Json.JsonSerializerOptions?)null) ?? new List<string>())
                .IsRequired();
        });

        builder.Entity<ProjectInput>(entity =>
        {
            entity.HasKey(input => input.Id);
            entity.Property(input => input.Kind).IsRequired().HasMaxLength(20);
            entity.Property(input => input.Name).IsRequired().HasMaxLength(300);
            entity.HasOne(input => input.Project)
                .WithMany(project => project.Inputs)
                .HasForeignKey(input => input.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ProjectTask>(entity =>
        {
            entity.HasKey(task => task.Id);
            entity.Property(task => task.Title).IsRequired().HasMaxLength(300);
            entity.Property(task => task.Description).IsRequired();
            entity.HasOne(task => task.Project)
                .WithMany(project => project.Tasks)
                .HasForeignKey(task => task.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(task => task.Inputs)
                .WithMany(input => input.Tasks)
                .UsingEntity(join => join.ToTable("ProjectTaskInputs"));
        });
    }
}
