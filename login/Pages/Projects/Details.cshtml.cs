using System.Security.Claims;
using login.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace login.Pages.Projects;

[Authorize]
public sealed class DetailsModel(ApplicationDbContext dbContext) : PageModel
{
    public ReformProject Project { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (ownerId is null)
        {
            return Forbid();
        }

        var project = await dbContext.Projects
            .AsNoTracking()
            .Where(item => item.Id == id && item.OwnerId == ownerId)
            .Include(item => item.Recommendations.OrderBy(recommendation => recommendation.Position))
                .ThenInclude(recommendation => recommendation.Video)
            .FirstOrDefaultAsync(cancellationToken);

        if (project is null)
        {
            return NotFound();
        }

        Project = project;
        return Page();
    }
}
