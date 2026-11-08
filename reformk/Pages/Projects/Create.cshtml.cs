using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.ClientModel;
using Azure;
using Azure.Core;
using Azure.Identity;
using Reformk.Data;
using Reformk.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Data.Sqlite;

namespace Reformk.Pages.Projects;

[Authorize]
public sealed class CreateModel(
    ApplicationDbContext dbContext,
    ProjectVideoService projectVideoService,
    SemanticVideoSearchService? semanticSearchService,
    IProjectWorkDescriptionGenerator? workDescriptionGenerator,
    IOptions<VideoCatalogOptions> options,
    ILogger<CreateModel> logger) : PageModel
{
    private const int PageSize = 3;

    [BindProperty]
    public ProjectForm Input { get; set; } = new();

    [BindProperty]
    public Guid? ExistingProjectId { get; set; }

    [BindProperty]
    public bool ConfirmReplacement { get; set; }

    public IReadOnlyList<VideoCategoryOptions> Categories => options.Value.Categories;
    public IReadOnlyList<SemanticMatch> Matches { get; private set; } = [];
    public bool SearchPerformed { get; private set; }
    public bool HasMore { get; private set; }
    public int NextOffset { get; private set; }
    public string? SearchMessage { get; private set; }
    public bool IsChangingVideo => ExistingProjectId.HasValue;

    public async Task<IActionResult> OnGetAsync(Guid? projectId, CancellationToken cancellationToken)
    {
        if (projectId is null)
        {
            return Page();
        }

        var project = await FindOwnedProjectAsync(projectId.Value, cancellationToken);
        if (project is null)
        {
            return NotFound();
        }

        ExistingProjectId = project.Id;
        Input = new ProjectForm
        {
            Name = project.Name,
            Description = project.Description,
            Category = project.Category
        };
        return Page();
    }

    public async Task<IActionResult> OnPostSearchAsync(CancellationToken cancellationToken)
    {
        if (!await ValidateRequestAsync(cancellationToken))
        {
            return Page();
        }

        SearchPerformed = true;
        await SearchAsync(0, PageSize, cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostMoreAsync(
        int offset,
        bool append = false,
        CancellationToken cancellationToken = default)
    {
        if (offset is < 0 or > 9_997 ||
            !await ValidateRequestAsync(cancellationToken))
        {
            if (append)
            {
                return new JsonResult(new { error = "No se pudo continuar la búsqueda. Revisa los datos del proyecto." })
                {
                    StatusCode = StatusCodes.Status400BadRequest
                };
            }

            return Page();
        }

        SearchPerformed = true;
        if (append)
        {
            var searchMessage = await TrySearchAsync(offset, PageSize, cancellationToken);
            if (searchMessage is not null)
            {
                return new JsonResult(new { error = searchMessage })
                {
                    StatusCode = StatusCodes.Status503ServiceUnavailable
                };
            }

            return new JsonResult(new
            {
                matches = Matches.Select(match => new
                {
                    videoId = match.VideoId,
                    match.Title,
                    match.Summary,
                    score = Math.Clamp((int)Math.Round(match.Similarity * 100), 0, 100),
                    match.MaterialCount,
                    match.EquipmentCount,
                    match.LaborCount
                }),
                hasMore = HasMore,
                nextOffset = NextOffset
            });
        }

        await SearchAsync(0, offset + PageSize, cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostChooseAsync(
        string selectedVideoId,
        CancellationToken cancellationToken)
    {
        if (!await ValidateRequestAsync(cancellationToken))
        {
            return Page();
        }

        var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (ownerId is null)
        {
            return Forbid();
        }

        ReformProject project;
        if (ExistingProjectId is { } existingId)
        {
            if (!ConfirmReplacement)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Confirma que quieres cambiar el vídeo y reemplazar todos los insumos y tareas del proyecto.");
                return await ReturnToSearchAsync(cancellationToken);
            }

            var ownedProject = await dbContext.Projects
                .Include(item => item.Inputs)
                .Include(item => item.Tasks)
                .FirstOrDefaultAsync(
                    item => item.Id == existingId && item.OwnerId == ownerId,
                    cancellationToken);
            if (ownedProject is null)
            {
                return NotFound();
            }

            project = ownedProject;
            project.Name = Input.Name.Trim();
            project.Description = Input.Description.Trim();
            project.Category = Input.Category;
        }
        else
        {
            project = new ReformProject
            {
                Id = Guid.NewGuid(),
                OwnerId = ownerId,
                Name = Input.Name.Trim(),
                Description = Input.Description.Trim(),
                Category = Input.Category,
                CreatedAt = DateTime.UtcNow
            };
            dbContext.Projects.Add(project);
        }

        var video = await projectVideoService.FindVideoAsync(selectedVideoId, cancellationToken);
        if (video is null)
        {
            return NotFound();
        }

        projectVideoService.ReplaceVideo(project, video);
        await dbContext.SaveChangesAsync(cancellationToken);
        await TryGenerateWorkDescriptionAsync(project, video, cancellationToken);
        return RedirectToPage("./Details", new { id = project.Id });
    }

    public async Task<IActionResult> OnPostWithoutVideoAsync(CancellationToken cancellationToken)
    {
        if (ExistingProjectId is not null)
        {
            return NotFound();
        }

        if (!await ValidateRequestAsync(cancellationToken))
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
        return RedirectToPage("./Details", new { id = project.Id });
    }

    private async Task<bool> ValidateRequestAsync(CancellationToken cancellationToken)
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

        if (ExistingProjectId is { } projectId &&
            await FindOwnedProjectAsync(projectId, cancellationToken) is null)
        {
            return false;
        }

        return ModelState.IsValid;
    }

    private async Task<IActionResult> ReturnToSearchAsync(CancellationToken cancellationToken)
    {
        SearchPerformed = true;
        await SearchAsync(0, PageSize, cancellationToken);
        return Page();
    }

    private async Task SearchAsync(int skip, int take, CancellationToken cancellationToken)
    {
        SearchMessage = await TrySearchAsync(skip, take, cancellationToken);
    }

    private async Task<string?> TrySearchAsync(int skip, int take, CancellationToken cancellationToken)
    {
        if (semanticSearchService is null)
        {
            Matches = [];
            HasMore = false;
            NextOffset = skip;
            return SearchMessage = "La búsqueda semántica no está configurada. Puedes ajustar la descripción o continuar sin vídeo.";
        }

        var query = string.IsNullOrWhiteSpace(Input.Category)
            ? Input.Description
            : $"{Input.Description}\nCategoría: {Input.Category}";
        try
        {
            var result = await semanticSearchService.SearchPageAsync(query, skip, take, cancellationToken: cancellationToken);
            Matches = result.Matches;
            HasMore = result.HasMore;
            NextOffset = skip + Matches.Count;
            SearchMessage = Matches.Count == 0
                ? result.BestSimilarity is null
                    ? "El catálogo no tiene vídeos con índice semántico para buscar. Configura Corpus:Path e importa el catálogo o crea el proyecto sin vídeo."
                    : "No encontramos vídeos semánticamente relacionados. Puedes ajustar la descripción o crear el proyecto sin vídeo."
                : null;
            return SearchMessage;
        }
        catch (Exception exception) when (exception is not OperationCanceledException &&
            exception is (ClientResultException or AuthenticationFailedException or HttpRequestException or
                SqliteException or RequestFailedException or InvalidOperationException))
        {
            logger.LogError(exception, "Semantic project-video search failed.");
            Matches = [];
            HasMore = false;
            NextOffset = skip;
            return SearchMessage = "No se pudo completar la búsqueda de vídeos. Inténtalo de nuevo o crea el proyecto sin vídeo.";
        }
    }

    private async Task TryGenerateWorkDescriptionAsync(
        ReformProject project,
        VideoCatalogItem video,
        CancellationToken cancellationToken)
    {
        if (workDescriptionGenerator is null)
        {
            logger.LogInformation("Project AI is not configured; work description can be generated later.");
            TempData["WorkDescriptionNotice"] = "El vídeo quedó guardado, pero la descripción del trabajo no está disponible ahora. Puedes reintentarlo desde el proyecto.";
            return;
        }

        try
        {
            project.WorkDescription = await workDescriptionGenerator.GenerateAsync(project, video, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException &&
            exception is (ClientResultException or AuthenticationFailedException or HttpRequestException or
                RequestFailedException or InvalidOperationException))
        {
            logger.LogError(exception, "Could not generate work description for project {ProjectId}.", project.Id);
            TempData["WorkDescriptionNotice"] = "El vídeo se guardó, pero no se pudo generar la descripción del trabajo. Puedes reintentarlo desde el proyecto.";
        }
    }

    private Task<ReformProject?> FindOwnedProjectAsync(Guid id, CancellationToken cancellationToken)
    {
        var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return ownerId is null
            ? Task.FromResult<ReformProject?>(null)
            : dbContext.Projects.FirstOrDefaultAsync(
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
