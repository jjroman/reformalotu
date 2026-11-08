using Reformk.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Reformk.Pages.Interno;

public sealed record VideoListItem(string VideoId, string Title, string[] Keywords);

[Authorize]
public sealed class VideosModel(ApplicationDbContext dbContext) : PageModel
{
    private const int PageSize = 10;

    [BindProperty(SupportsGet = true)]
    public string? Q { get; set; }

    [BindProperty(SupportsGet = true)]
    public List<string> K { get; set; } = [];

    [BindProperty(SupportsGet = true, Name = "page")]
    public int PageNumber { get; set; } = 1;

    public IReadOnlyList<string> AllKeywords { get; private set; } = [];
    public IReadOnlyList<VideoListItem> Videos { get; private set; } = [];
    public int TotalMatches { get; private set; }
    public int TotalPages { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        // Unlisted utility page: keep it out of search engines.
        Response.Headers["X-Robots-Tag"] = "noindex, nofollow";

        var keywordColumns = await dbContext.Videos
            .AsNoTracking()
            .Select(video => video.Keywords)
            .ToListAsync(cancellationToken);
        AllKeywords = keywordColumns
            .SelectMany(SplitKeywords)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var selected = K
            .Where(keyword => !string.IsNullOrWhiteSpace(keyword))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var query = dbContext.Videos.AsNoTracking();

        var text = Q?.Trim();
        if (!string.IsNullOrEmpty(text))
        {
            query = query.Where(video =>
                video.Title.Contains(text) ||
                video.TranscriptText.Contains(text));
        }

        foreach (var keyword in selected)
        {
            var current = keyword;
            query = query.Where(video => video.Keywords.Contains(current));
        }

        var rows = await query
            .OrderBy(video => video.Title)
            .ThenBy(video => video.VideoId)
            .Select(video => new { video.VideoId, video.Title, video.Keywords })
            .ToListAsync(cancellationToken);

        // Substring match in SQL is only a prefilter; enforce exact keyword tokens here.
        var matches = rows
            .Select(row => new VideoListItem(row.VideoId, row.Title, SplitKeywords(row.Keywords).ToArray()))
            .Where(item => selected.All(keyword =>
                item.Keywords.Contains(keyword, StringComparer.OrdinalIgnoreCase)))
            .ToList();

        TotalMatches = matches.Count;
        TotalPages = (int)Math.Ceiling(TotalMatches / (double)PageSize);
        PageNumber = TotalPages == 0
            ? 1
            : Math.Clamp(PageNumber, 1, TotalPages);
        Videos = matches
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .ToList();
    }

    public bool IsSelected(string keyword) =>
        K.Contains(keyword, StringComparer.OrdinalIgnoreCase);

    private static IEnumerable<string> SplitKeywords(string keywords) =>
        keywords.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
