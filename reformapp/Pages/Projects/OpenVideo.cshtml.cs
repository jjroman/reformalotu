using System.Security.Claims;
using ReformApp.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ReformApp.Pages.Projects;

[Authorize]
public sealed class OpenVideoModel(ApplicationDbContext dbContext) : PageModel
{
    public async Task<IActionResult> OnGetAsync(
        Guid projectId,
        string videoId,
        CancellationToken cancellationToken)
    {
        var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (ownerId is null)
        {
            return Forbid();
        }

        var belongsToProject = await dbContext.Projects
            .AnyAsync(
                project =>
                    project.Id == projectId &&
                    project.SelectedVideoId == videoId &&
                    project.OwnerId == ownerId,
                cancellationToken);
        if (!belongsToProject)
        {
            return NotFound();
        }

        var updatedRows = await dbContext.Videos
            .Where(video => video.VideoId == videoId)
            .ExecuteUpdateAsync(
                updates => updates.SetProperty(video => video.OpenCount, video => video.OpenCount + 1),
                cancellationToken);
        if (updatedRows != 1)
        {
            return NotFound();
        }

        return Redirect($"https://www.youtube.com/watch?v={Uri.EscapeDataString(videoId)}");
    }
}
