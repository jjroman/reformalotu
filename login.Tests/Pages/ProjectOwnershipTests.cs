using System.Security.Claims;
using login.Data;
using login.Pages.Projects;
using login.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace login.Tests.Pages;

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

        var model = CreateModel(dbContext, owner.Id);
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

        var model = CreateModel(dbContext, "another-user");
        model.Input = new DetailsModel.InputForm { Kind = "material", Name = "Cemento" };

        var result = await model.OnPostAddInputAsync(project.Id, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        Assert.Empty(await dbContext.ProjectInputs.ToListAsync());
    }

    private static DetailsModel CreateModel(ApplicationDbContext dbContext, string userId) =>
        new(
            dbContext,
            new ProjectVideoService(dbContext),
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
