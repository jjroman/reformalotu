using System.Security.Claims;
using Reformk.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Reformk.Pages.Projects;

[Authorize]
public sealed class IndexModel(ApplicationDbContext dbContext) : PageModel
{
    private const int PageSize = 10;
    private const int MaxSearchLength = 100;

    public IReadOnlyList<ReformProject> Projects { get; private set; } = [];

    [BindProperty(SupportsGet = true, Name = "q")]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true, Name = "p")]
    public int PageNumber { get; set; } = 1;

    public int TotalPages { get; private set; } = 1;

    public int TotalCount { get; private set; }

    public bool HasProjectsOverall { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (ownerId is null)
        {
            return Forbid();
        }

        Search = Search?.Trim();
        if (Search?.Length > MaxSearchLength)
        {
            Search = Search[..MaxSearchLength];
        }

        var owned = dbContext.Projects
            .AsNoTracking()
            .Where(project => project.OwnerId == ownerId);

        HasProjectsOverall = await owned.AnyAsync(cancellationToken);

        var query = owned;
        if (!string.IsNullOrEmpty(Search))
        {
            // Contains se traduce a LIKE en SQLite y PostgreSQL; ToLower mantiene la búsqueda insensible a mayúsculas en ambos.
            var term = Search.ToLower();
            query = query.Where(project =>
                project.Name.ToLower().Contains(term)
                || project.Description.ToLower().Contains(term)
                || (project.Category != null && project.Category.ToLower().Contains(term)));
        }

        TotalCount = await query.CountAsync(cancellationToken);
        TotalPages = Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
        PageNumber = Math.Clamp(PageNumber, 1, TotalPages);

        Projects = await query
            .OrderByDescending(project => project.CreatedAt)
            .ThenBy(project => project.Id)
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync(cancellationToken);

        return Page();
    }
}
