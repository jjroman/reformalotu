using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Security.Claims;
using login.Data;
using login.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace login.Pages.Projects;

[Authorize]
public sealed class CreateModel(
    ApplicationDbContext dbContext,
    VideoRecommendationService recommendationService,
    IOptions<VideoCatalogOptions> options,
    ILogger<CreateModel> logger) : PageModel
{
    [BindProperty]
    public ProjectForm Input { get; set; } = new();

    public IReadOnlyList<VideoCategoryOptions> Categories => options.Value.Categories;
    public IReadOnlyList<VideoRecommendation> Recommendations { get; private set; } = [];
    public Guid? ProjectId { get; private set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(Input.Name))
        {
            ModelState.AddModelError("Input.Name", "Escribe un nombre para el proyecto.");
        }

        if (string.IsNullOrWhiteSpace(Input.Description))
        {
            ModelState.AddModelError("Input.Description", "Escribe una descripción para el proyecto.");
        }

        if (Input.Category is not null &&
            !Categories.Any(category => string.Equals(
                category.Name,
                Input.Category,
                StringComparison.OrdinalIgnoreCase)))
        {
            ModelState.AddModelError("Input.Category", "Selecciona una categoría válida.");
        }

        if (!ModelState.IsValid)
        {
            logger.LogInformation(
                "Project creation request failed validation with {ValidationErrorCount} errors.",
                ModelState.ErrorCount);
            return Page();
        }

        var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (ownerId is null)
        {
            logger.LogWarning("Project creation request has no authenticated owner identifier.");
            return Forbid();
        }

        var project = new ReformProject
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            Name = Input.Name.Trim(),
            Description = Input.Description.Trim(),
            Category = Input.Category,
            CreatedAt = DateTime.UtcNow
        };
        dbContext.Projects.Add(project);
        logger.LogInformation("Saving project {ProjectId}.", project.Id);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            logger.LogError(exception, "Failed to save project {ProjectId}.", project.Id);
            throw;
        }

        logger.LogInformation("Saved project {ProjectId}.", project.Id);

        var recommendationTimer = Stopwatch.StartNew();
        logger.LogInformation("Starting video recommendation search for project {ProjectId}.", project.Id);
        Recommendations = await recommendationService.RecommendAsync(
            project.Description,
            project.Category,
            cancellationToken);
        logger.LogInformation(
            "Video recommendation search for project {ProjectId} completed with {RecommendationCount} results in {ElapsedMilliseconds} ms.",
            project.Id,
            Recommendations.Count,
            recommendationTimer.ElapsedMilliseconds);

        for (var index = 0; index < Recommendations.Count; index++)
        {
            var recommendation = Recommendations[index];
            dbContext.ProjectRecommendations.Add(new ProjectRecommendation
            {
                ProjectId = project.Id,
                VideoId = recommendation.Video.VideoId,
                Position = index + 1,
                Score = recommendation.Score
            });
        }

        logger.LogInformation(
            "Saving {RecommendationCount} video recommendations for project {ProjectId}.",
            Recommendations.Count,
            project.Id);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            logger.LogError(
                exception,
                "Failed to save video recommendations for project {ProjectId}.",
                project.Id);
            throw;
        }

        logger.LogInformation(
            "Saved {RecommendationCount} video recommendations for project {ProjectId}.",
            Recommendations.Count,
            project.Id);
        ProjectId = project.Id;
        return Page();
    }

    public sealed class ProjectForm
    {
        [Required(ErrorMessage = "Escribe un nombre para el proyecto.")]
        [StringLength(200, ErrorMessage = "El nombre no puede superar los 200 caracteres.")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Escribe una descripción para el proyecto.")]
        [StringLength(1000, ErrorMessage = "La descripción no puede superar los 1000 caracteres.")]
        public string Description { get; set; } = string.Empty;

        public string? Category { get; set; }
    }
}
