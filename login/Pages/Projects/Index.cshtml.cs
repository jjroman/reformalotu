using System.Security.Claims;
using login.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace login.Pages.Projects;

[Authorize]
public sealed class IndexModel(ApplicationDbContext dbContext) : PageModel
{
    public IReadOnlyList<ReformProject> Projects { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (ownerId is null)
        {
            return Forbid();
        }

        Projects = await dbContext.Projects
            .AsNoTracking()
            .Where(project => project.OwnerId == ownerId)
            .OrderByDescending(project => project.CreatedAt)
            .ToListAsync(cancellationToken);

        return Page();
    }
}
