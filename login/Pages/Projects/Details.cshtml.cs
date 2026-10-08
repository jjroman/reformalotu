using System.Security.Claims;
using login.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Ganss.Xss;
using Markdig;

namespace login.Pages.Projects;

[Authorize]
public sealed class DetailsModel(ApplicationDbContext dbContext) : PageModel
{
    private static readonly MarkdownPipeline MarkdownPipeline = new MarkdownPipelineBuilder()
        .DisableHtml()
        .Build();
    private static readonly HtmlSanitizer HtmlSanitizer = CreateHtmlSanitizer();

    public ReformProject Project { get; private set; } = null!;
    public string DescriptionHtml { get; private set; } = string.Empty;

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
        DescriptionHtml = HtmlSanitizer.Sanitize(Markdown.ToHtml(project.Description, MarkdownPipeline));
        return Page();
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
}
