using login.Data;
using login.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace login.Tests.Services;

public sealed class ProjectVideoServiceTests
{
    [Fact]
    public async Task ReplacingVideoCopiesIndependentInputsAndTaskReferencesThenClearsOldLists()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = CreateDbContext(connection);
        var owner = new IdentityUser { Id = "owner", UserName = "owner", NormalizedUserName = "OWNER" };
        var firstVideo = CreateVideo("video-one");
        firstVideo.Inputs.Add(new VideoInput
        {
            VideoId = firstVideo.VideoId,
            SourceId = "m1",
            Kind = "material",
            Name = "Pintura",
            Quantity = 2,
            Unit = "litros"
        });
        firstVideo.Inputs.Add(new VideoInput
        {
            VideoId = firstVideo.VideoId,
            SourceId = "e1",
            Kind = "equipment",
            Name = "Rodillo"
        });
        firstVideo.Tasks.Add(new VideoTaskDefinition
        {
            VideoId = firstVideo.VideoId,
            Order = 1,
            Title = "Pintar",
            Description = "Aplicar la pintura.",
            MaterialIds = ["m1"],
            EquipmentIds = ["e1"]
        });
        var secondVideo = CreateVideo("video-two");
        secondVideo.Inputs.Add(new VideoInput
        {
            VideoId = secondVideo.VideoId,
            SourceId = "m2",
            Kind = "material",
            Name = "Barniz"
        });
        dbContext.Users.Add(owner);
        dbContext.Videos.AddRange(firstVideo, secondVideo);
        var project = new ReformProject
        {
            Id = Guid.NewGuid(),
            OwnerId = owner.Id,
            Name = "Proyecto",
            Description = "Renovar",
            CreatedAt = DateTime.UtcNow
        };
        dbContext.Projects.Add(project);
        await dbContext.SaveChangesAsync();

        var service = new ProjectVideoService(dbContext);
        service.ReplaceVideo(project, await service.FindVideoAsync(firstVideo.VideoId, default));
        await dbContext.SaveChangesAsync();

        var savedProject = await dbContext.Projects
            .Include(item => item.Inputs)
            .Include(item => item.Tasks)
                .ThenInclude(task => task.Inputs)
            .SingleAsync(item => item.Id == project.Id);
        var copiedMaterial = Assert.Single(savedProject.Inputs, input => input.Kind == "material");
        Assert.False(copiedMaterial.IsManual);
        Assert.Equal(2, copiedMaterial.Quantity);
        Assert.Equal(2, Assert.Single(savedProject.Tasks).Inputs.Count);

        copiedMaterial.Name = "Pintura modificada en el proyecto";
        await dbContext.SaveChangesAsync();
        Assert.Equal("Pintura", (await dbContext.VideoInputs.SingleAsync(input => input.SourceId == "m1")).Name);

        dbContext.ProjectInputs.Remove(copiedMaterial);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        var savedTaskAfterInputRemoval = await dbContext.ProjectTasks
            .AsNoTracking()
            .Include(task => task.Inputs)
            .SingleAsync();
        Assert.Single(savedTaskAfterInputRemoval.Inputs);

        savedProject = await dbContext.Projects
            .Include(item => item.Inputs)
            .Include(item => item.Tasks)
            .SingleAsync(item => item.Id == project.Id);
        service.ReplaceVideo(savedProject, await service.FindVideoAsync(secondVideo.VideoId, default));
        await dbContext.SaveChangesAsync();
        var replacedProject = await dbContext.Projects
            .Include(item => item.Inputs)
            .Include(item => item.Tasks)
            .SingleAsync(item => item.Id == project.Id);
        Assert.Equal("video-two", replacedProject.SelectedVideoId);
        Assert.Equal("Barniz", Assert.Single(replacedProject.Inputs).Name);
        Assert.Empty(replacedProject.Tasks);

        service.ReplaceVideo(replacedProject, null);
        await dbContext.SaveChangesAsync();
        var projectWithoutVideo = await dbContext.Projects
            .Include(item => item.Inputs)
            .Include(item => item.Tasks)
            .SingleAsync(item => item.Id == project.Id);
        Assert.Null(projectWithoutVideo.SelectedVideoId);
        Assert.Empty(projectWithoutVideo.Inputs);
        Assert.Empty(projectWithoutVideo.Tasks);
    }

    private static VideoCatalogItem CreateVideo(string videoId) => new()
    {
        VideoId = videoId,
        Title = videoId,
        TranscriptText = "Transcripción",
        Categories = string.Empty,
        Keywords = string.Empty
    };

    private static ApplicationDbContext CreateDbContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        var dbContext = new ApplicationDbContext(options);
        dbContext.Database.EnsureCreated();
        return dbContext;
    }
}
