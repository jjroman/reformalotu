using System.ComponentModel.DataAnnotations;
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
public sealed class EditModel(
    ApplicationDbContext dbContext,
    IOptions<VideoCatalogOptions> options) : PageModel
{
    [BindProperty]
    public ProjectForm Input { get; set; } = new();

    public IReadOnlyList<VideoCategoryOptions> Categories => options.Value.Categories;

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        var project = await FindOwnedProjectAsync(id, cancellationToken);
        if (project is null)
        {
            return NotFound();
        }

        Input = new ProjectForm
        {
            Name = project.Name,
            Description = project.Description,
            Category = project.Category
        };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id, CancellationToken cancellationToken)
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

        var project = await FindOwnedProjectAsync(id, cancellationToken);
        if (project is null)
        {
            return NotFound();
        }

        project.Name = Input.Name.Trim();
        project.Description = Input.Description.Trim();
        project.Category = Input.Category;
        await dbContext.SaveChangesAsync(cancellationToken);
        return RedirectToPage("./Details", new { id });
    }

    private Task<ReformProject?> FindOwnedProjectAsync(Guid id, CancellationToken cancellationToken)
    {
        var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (ownerId is null)
        {
            return Task.FromResult<ReformProject?>(null);
        }

        return dbContext.Projects
            .FirstOrDefaultAsync(
                project => project.Id == id && project.OwnerId == ownerId,
                cancellationToken);
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
