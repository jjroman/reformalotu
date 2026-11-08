using Reformk.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Reformk.Pages;

[Authorize]
public sealed class MetricsModel(ApplicationDbContext dbContext) : PageModel
{
    public long ProjectCount { get; private set; }
    public long VideoOpenCount { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        ProjectCount = await dbContext.Projects.LongCountAsync(cancellationToken);
        VideoOpenCount = await dbContext.VideoOpenCounts
            .Select(video => (long?)video.Count)
            .SumAsync(cancellationToken) ?? 0;
    }
}
