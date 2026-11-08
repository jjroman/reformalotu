using System.Security.Claims;
using Reformk.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Reformk.Pages.Projects;

[Authorize]
public sealed class OpenVideoModel(ApplicationDbContext dbContext, VideoCatalogDbContext catalogContext) : PageModel
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

        if (!await catalogContext.Videos.AnyAsync(video => video.VideoId == videoId, cancellationToken))
        {
            return NotFound();
        }

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO VideoOpenCounts (VideoId, Count) VALUES ({videoId}, 1)
            ON CONFLICT (VideoId) DO UPDATE SET Count = Count + 1
            """,
            cancellationToken);
        return Redirect($"https://www.youtube.com/watch?v={Uri.EscapeDataString(videoId)}");
    }
}
