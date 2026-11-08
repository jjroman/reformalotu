using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Reformk.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext(options)
{
    public DbSet<ReformProject> Projects => Set<ReformProject>();
    public DbSet<VideoOpenCount> VideoOpenCounts => Set<VideoOpenCount>();
    public DbSet<ProjectInput> ProjectInputs => Set<ProjectInput>();
    public DbSet<ProjectTask> ProjectTasks => Set<ProjectTask>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

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
            entity.Ignore(project => project.SelectedVideo);
        });

        builder.Entity<VideoOpenCount>(entity =>
        {
            entity.HasKey(count => count.VideoId);
            entity.Property(count => count.VideoId).HasMaxLength(11);
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
