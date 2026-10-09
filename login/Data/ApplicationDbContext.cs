using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace login.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext(options)
{
    public DbSet<VideoCatalogItem> Videos => Set<VideoCatalogItem>();
    public DbSet<ReformProject> Projects => Set<ReformProject>();
    public DbSet<ProjectRecommendation> ProjectRecommendations => Set<ProjectRecommendation>();

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
            entity.Property(video => video.Materials).IsRequired();
            entity.Property(video => video.Equipment).IsRequired();
        });

        builder.Entity<ReformProject>(entity =>
        {
            entity.HasKey(project => project.Id);
            entity.Property(project => project.Name).IsRequired().HasMaxLength(200);
            entity.Property(project => project.Description).IsRequired();
            entity.Property(project => project.OwnerId).IsRequired();
            entity.HasIndex(project => new { project.OwnerId, project.CreatedAt });
            entity.HasOne(project => project.Owner)
                .WithMany()
                .HasForeignKey(project => project.OwnerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ProjectRecommendation>(entity =>
        {
            entity.HasKey(recommendation => new { recommendation.ProjectId, recommendation.VideoId });
            entity.HasIndex(recommendation => new { recommendation.ProjectId, recommendation.Position })
                .IsUnique();
            entity.HasOne(recommendation => recommendation.Project)
                .WithMany(project => project.Recommendations)
                .HasForeignKey(recommendation => recommendation.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(recommendation => recommendation.Video)
                .WithMany()
                .HasForeignKey(recommendation => recommendation.VideoId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
