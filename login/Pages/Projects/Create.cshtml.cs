using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using login.Data;
using login.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace login.Pages.Projects;

[Authorize]
public sealed class CreateModel(
    ApplicationDbContext dbContext,
    VideoRecommendationService recommendationService,
    IOptions<VideoCatalogOptions> options) : PageModel
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
            return Page();
        }

        var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (ownerId is null)
        {
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
        await dbContext.SaveChangesAsync(cancellationToken);

        Recommendations = await recommendationService.RecommendAsync(
            project.Description,
            project.Category,
            cancellationToken);
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

        await dbContext.SaveChangesAsync(cancellationToken);
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
