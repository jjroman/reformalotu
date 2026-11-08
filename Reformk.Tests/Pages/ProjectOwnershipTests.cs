using System.Security.Claims;
using Reformk.Data;
using Reformk.Pages.Projects;
using Reformk.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Reformk.Tests.Pages;

public sealed class ProjectOwnershipTests
{
    [Fact]
    public async Task OwnerCanAddProjectInputAndTask()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var dbContext = new ApplicationDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();
        await using var catalogContext = CreateCatalogContext();

        var owner = new IdentityUser { Id = "project-owner", UserName = "owner" };
        var project = new ReformProject
        {
            Id = Guid.NewGuid(),
            OwnerId = owner.Id,
            Name = "Proyecto",
            Description = "Descripción",
            CreatedAt = DateTime.UtcNow
        };
        dbContext.Users.Add(owner);
        dbContext.Projects.Add(project);
        await dbContext.SaveChangesAsync();

        var model = CreateModel(dbContext, catalogContext, owner.Id);
        model.Input = new DetailsModel.InputForm
        {
            Kind = "material",
            Name = "Pintura",
            Quantity = 0,
            Unit = "litros"
        };
        var addInputResult = await model.OnPostAddInputAsync(project.Id, CancellationToken.None);
        Assert.IsType<RedirectToPageResult>(addInputResult);

        model.TaskInput = new DetailsModel.TaskForm { Title = "Preparar la pared" };
        var addTaskResult = await model.OnPostAddTaskAsync(project.Id, CancellationToken.None);
        Assert.IsType<RedirectToPageResult>(addTaskResult);
        Assert.True(await dbContext.ProjectInputs.AnyAsync(input =>
            input.ProjectId == project.Id && input.Name == "Pintura" && input.IsManual));
        Assert.True(await dbContext.ProjectTasks.AnyAsync(task =>
            task.ProjectId == project.Id && task.Title == "Preparar la pared"));
    }

    [Fact]
    public async Task AddingInputToAnotherUsersProjectReturnsNotFound()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var dbContext = new ApplicationDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();
        await using var catalogContext = CreateCatalogContext();

        var owner = new IdentityUser { Id = "project-owner", UserName = "owner" };
        var project = new ReformProject
        {
            Id = Guid.NewGuid(),
            OwnerId = owner.Id,
            Name = "Proyecto privado",
            Description = "Descripción",
            CreatedAt = DateTime.UtcNow
        };
        dbContext.Users.Add(owner);
        dbContext.Projects.Add(project);
        await dbContext.SaveChangesAsync();

        var model = CreateModel(dbContext, catalogContext, "another-user");
        model.Input = new DetailsModel.InputForm { Kind = "material", Name = "Cemento" };

        var result = await model.OnPostAddInputAsync(project.Id, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        Assert.Empty(await dbContext.ProjectInputs.ToListAsync());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ProjectRemainsEditableWhenSelectedVideoIsMissingOrCatalogFails(bool catalogExists)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await dbContext.Database.EnsureCreatedAsync();
        await using var catalogConnection = new SqliteConnection("Data Source=:memory:");
        await catalogConnection.OpenAsync();
        await using var catalogContext = new VideoCatalogDbContext(
            new DbContextOptionsBuilder<VideoCatalogDbContext>().UseSqlite(catalogConnection).Options);
        if (catalogExists)
        {
            await catalogContext.Database.EnsureCreatedAsync();
        }

        dbContext.Users.Add(new IdentityUser { Id = "owner", UserName = "owner" });
        var project = new ReformProject
        {
            Id = Guid.NewGuid(), OwnerId = "owner", Name = "Proyecto", Description = "Descripción",
            SelectedVideoId = "missing", WorkDescription = "Trabajo guardado"
        };
        dbContext.Projects.Add(project);
        await dbContext.SaveChangesAsync();

        var model = CreateModel(dbContext, catalogContext, "owner");
        model.Input = new DetailsModel.InputForm { Kind = "material", Name = "Cemento" };
        var result = await model.OnPostAddInputAsync(project.Id, CancellationToken.None);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Null(model.Project.SelectedVideo);
        Assert.Equal(!catalogExists, model.VideoCatalogUnavailable);
        Assert.Equal("missing", model.Project.SelectedVideoId);
        Assert.Equal("Trabajo guardado", model.Project.WorkDescription);
        Assert.True(await dbContext.ProjectInputs.AnyAsync(input => input.Name == "Cemento"));
    }

    [Fact]
    public async Task VideoOpenCountsStayWithTheApplicationWhenCatalogChanges()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var dbContext = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await dbContext.Database.EnsureCreatedAsync();
        await using var catalogConnection = new SqliteConnection("Data Source=:memory:");
        await catalogConnection.OpenAsync();
        await using var catalog = new VideoCatalogDbContext(
            new DbContextOptionsBuilder<VideoCatalogDbContext>().UseSqlite(catalogConnection).Options);
        await catalog.Database.EnsureCreatedAsync();
        catalog.Videos.Add(new VideoCatalogItem { VideoId = "video", Title = "Vídeo" });
        await catalog.SaveChangesAsync();
        dbContext.Users.Add(new IdentityUser { Id = "owner", UserName = "owner" });
        var project = new ReformProject
        {
            Id = Guid.NewGuid(), OwnerId = "owner", Name = "Proyecto", Description = "Descripción", SelectedVideoId = "video"
        };
        dbContext.Projects.Add(project);
        await dbContext.SaveChangesAsync();
        var model = new OpenVideoModel(dbContext, catalog)
        {
            PageContext = new PageContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, "owner")], "test"))
                }
            }
        };

        Assert.IsType<RedirectResult>(await model.OnGetAsync(project.Id, "video", default));
        Assert.IsType<RedirectResult>(await model.OnGetAsync(project.Id, "video", default));
        await catalog.Videos.ExecuteDeleteAsync();
        Assert.IsType<NotFoundResult>(await model.OnGetAsync(project.Id, "video", default));
        Assert.Equal(2, (await dbContext.VideoOpenCounts.SingleAsync()).Count);
    }

    private static VideoCatalogDbContext CreateCatalogContext() =>
        new(new DbContextOptionsBuilder<VideoCatalogDbContext>().UseSqlite("Data Source=:memory:").Options);

    private static DetailsModel CreateModel(ApplicationDbContext dbContext, VideoCatalogDbContext catalogContext, string userId) =>
        new(
            dbContext,
            new ProjectVideoService(dbContext, catalogContext),
            null,
            NullLogger<DetailsModel>.Instance)
        {
            PageContext = new PageContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, userId)],
                        "test"))
                }
            }
        };
}
