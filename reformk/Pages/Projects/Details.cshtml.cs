using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.ClientModel;
using Azure;
using Azure.Core;
using Azure.Identity;
using Ganss.Xss;
using Reformk.Data;
using Reformk.Services;
using Markdig;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;

namespace Reformk.Pages.Projects;

[Authorize]
public sealed class DetailsModel(
    ApplicationDbContext dbContext,
    ProjectVideoService projectVideoService,
    IProjectWorkDescriptionGenerator? workDescriptionGenerator,
    ILogger<DetailsModel> logger) : PageModel
{
    private static readonly MarkdownPipeline MarkdownPipeline = new MarkdownPipelineBuilder()
        .DisableHtml()
        .Build();
    private static readonly HtmlSanitizer HtmlSanitizer = CreateHtmlSanitizer();

    [BindProperty]
    public InputForm Input { get; set; } = new();

    [BindProperty]
    public TaskForm TaskInput { get; set; } = new();

    [BindProperty]
    public bool ConfirmRemoval { get; set; }

    public ReformProject Project { get; private set; } = null!;
    public string DescriptionHtml { get; private set; } = string.Empty;
    public string? WorkDescriptionNotice { get; private set; }
    public IReadOnlyList<ProjectInput> Materials => Project.Inputs
        .Where(input => input.Kind == "material")
        .OrderBy(input => input.Name, StringComparer.OrdinalIgnoreCase)
        .ToArray();
    public IReadOnlyList<ProjectInput> Equipment => Project.Inputs
        .Where(input => input.Kind == "equipment")
        .OrderBy(input => input.Name, StringComparer.OrdinalIgnoreCase)
        .ToArray();
    public IReadOnlyList<ProjectInput> Labor => Project.Inputs
        .Where(input => input.Kind == "labor")
        .OrderBy(input => input.Name, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!await LoadOwnedProjectAsync(id, cancellationToken))
        {
            return NotFound();
        }

        WorkDescriptionNotice = TempData["WorkDescriptionNotice"] as string;
        return Page();
    }

    public async Task<IActionResult> OnPostAddInputAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!await LoadOwnedProjectAsync(id, cancellationToken))
        {
            return NotFound();
        }

        if (Input.Kind is not ("material" or "equipment" or "labor") ||
            string.IsNullOrWhiteSpace(Input.Name) ||
            Input.Name.Length > 300 ||
            Input.Quantity is < 0 ||
            Input.Headcount is < 1 ||
            Input.EstimatedHours is < 0)
        {
            ModelState.AddModelError(string.Empty, "Revisa los datos del insumo antes de añadirlo.");
            return Page();
        }

        var input = new ProjectInput
        {
            Id = Guid.NewGuid(),
            ProjectId = Project.Id,
            Kind = Input.Kind,
            Name = Input.Name.Trim(),
            Quantity = Input.Quantity,
            Unit = string.IsNullOrWhiteSpace(Input.Unit) ? null : Input.Unit.Trim(),
            Level = Input.Kind == "labor" ? Input.Level : null,
            Headcount = Input.Kind == "labor" ? Input.Headcount : null,
            EstimatedHours = Input.Kind == "labor" ? Input.EstimatedHours : null,
            IsManual = true
        };
        dbContext.ProjectInputs.Add(input);
        await dbContext.SaveChangesAsync(cancellationToken);
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostDeleteInputAsync(
        Guid id,
        Guid inputId,
        CancellationToken cancellationToken)
    {
        if (!await LoadOwnedProjectAsync(id, cancellationToken))
        {
            return NotFound();
        }

        var input = Project.Inputs.FirstOrDefault(item => item.Id == inputId);
        if (input is null)
        {
            return NotFound();
        }

        dbContext.ProjectInputs.Remove(input);
        await dbContext.SaveChangesAsync(cancellationToken);
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostAddTaskAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!await LoadOwnedProjectAsync(id, cancellationToken))
        {
            return NotFound();
        }

        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(TaskInput.Title))
        {
            ModelState.AddModelError("TaskInput.Title", "Escribe un título para la tarea.");
            return Page();
        }

        var task = new ProjectTask
        {
            Id = Guid.NewGuid(),
            ProjectId = Project.Id,
            Order = Project.Tasks.Count == 0 ? 1 : Project.Tasks.Max(task => task.Order) + 1,
            Title = TaskInput.Title.Trim(),
            Description = TaskInput.Description.Trim()
        };
        dbContext.ProjectTasks.Add(task);
        await dbContext.SaveChangesAsync(cancellationToken);
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostDeleteTaskAsync(
        Guid id,
        Guid taskId,
        CancellationToken cancellationToken)
    {
        if (!await LoadOwnedProjectAsync(id, cancellationToken))
        {
            return NotFound();
        }

        var task = Project.Tasks.FirstOrDefault(item => item.Id == taskId);
        if (task is null)
        {
            return NotFound();
        }

        dbContext.ProjectTasks.Remove(task);
        await dbContext.SaveChangesAsync(cancellationToken);
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostToggleTaskAsync(
        Guid id,
        Guid taskId,
        CancellationToken cancellationToken)
    {
        if (!await LoadOwnedProjectAsync(id, cancellationToken))
        {
            return NotFound();
        }

        var task = Project.Tasks.FirstOrDefault(item => item.Id == taskId);
        if (task is null)
        {
            return NotFound();
        }

        task.IsDone = !task.IsDone;
        await dbContext.SaveChangesAsync(cancellationToken);
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostRemoveVideoAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!ConfirmRemoval)
        {
            ModelState.AddModelError(string.Empty, "Confirma que quieres quitar el vídeo y eliminar los insumos y tareas del proyecto.");
            if (!await LoadOwnedProjectAsync(id, cancellationToken))
            {
                return NotFound();
            }
            return Page();
        }

        if (!await LoadOwnedProjectAsync(id, cancellationToken))
        {
            return NotFound();
        }

        projectVideoService.ReplaceVideo(Project, null);
        await dbContext.SaveChangesAsync(cancellationToken);
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostDeleteProjectAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!await LoadOwnedProjectAsync(id, cancellationToken))
        {
            return NotFound();
        }

        dbContext.Projects.Remove(Project);
        await dbContext.SaveChangesAsync(cancellationToken);
        return RedirectToPage("./Index");
    }

    public async Task<IActionResult> OnPostRetryWorkDescriptionAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!await LoadOwnedProjectAsync(id, cancellationToken))
        {
            return NotFound();
        }

        if (Project.SelectedVideo is null)
        {
            return NotFound();
        }
        if (workDescriptionGenerator is null)
        {
            WorkDescriptionNotice = "La generación con IA no está configurada. Puedes intentarlo de nuevo cuando esté disponible.";
            return Page();
        }

        try
        {
            Project.WorkDescription = await workDescriptionGenerator.GenerateAsync(
                Project,
                Project.SelectedVideo,
                cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            return RedirectToPage(new { id });
        }
        catch (Exception exception) when (exception is not OperationCanceledException &&
            exception is (ClientResultException or AuthenticationFailedException or HttpRequestException or
                RequestFailedException or InvalidOperationException))
        {
            logger.LogError(exception, "Could not retry work description generation for project {ProjectId}.", id);
            WorkDescriptionNotice = "No se pudo generar la descripción del trabajo. El vídeo y sus listas siguen guardados; puedes reintentarlo más tarde.";
            return Page();
        }
    }

    private async Task<bool> LoadOwnedProjectAsync(Guid id, CancellationToken cancellationToken)
    {
        var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (ownerId is null)
        {
            return false;
        }

        var project = await dbContext.Projects
            .Include(item => item.Inputs)
                .ThenInclude(input => input.Tasks)
            .Include(item => item.Tasks)
                .ThenInclude(task => task.Inputs)
            .Include(item => item.SelectedVideo)
                .ThenInclude(video => video!.Inputs)
            .Include(item => item.SelectedVideo)
                .ThenInclude(video => video!.Tasks)
            .Include(item => item.SelectedVideo)
                .ThenInclude(video => video!.SemanticIndex)
            .FirstOrDefaultAsync(
                item => item.Id == id && item.OwnerId == ownerId,
                cancellationToken);
        if (project is null)
        {
            return false;
        }

        Project = project;
        DescriptionHtml = HtmlSanitizer.Sanitize(Markdown.ToHtml(project.Description, MarkdownPipeline));
        return true;
    }

    private static HtmlSanitizer CreateHtmlSanitizer()
    {
        var sanitizer = new HtmlSanitizer();
        sanitizer.AllowedTags.Clear();
        sanitizer.AllowedTags.UnionWith([
            "a", "blockquote", "br", "code", "del", "em", "h1", "h2", "h3", "h4", "h5", "h6",
            "hr", "li", "ol", "p", "pre", "strong", "ul"
        ]);
        sanitizer.AllowedAttributes.Clear();
        sanitizer.AllowedAttributes.Add("href");
        sanitizer.AllowedSchemes.Clear();
        sanitizer.AllowedSchemes.UnionWith(["http", "https", "mailto"]);
        return sanitizer;
    }

    public sealed class InputForm
    {
        [StringLength(300)]
        public string Name { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public double? Quantity { get; set; }
        [StringLength(100)]
        public string? Unit { get; set; }
        public string? Level { get; set; }
        public int? Headcount { get; set; }
        public double? EstimatedHours { get; set; }
    }

    public sealed class TaskForm
    {
        [Required(ErrorMessage = "Escribe un título para la tarea.")]
        [StringLength(300)]
        public string Title { get; set; } = string.Empty;
        [StringLength(4000)]
        public string Description { get; set; } = string.Empty;
    }
}
