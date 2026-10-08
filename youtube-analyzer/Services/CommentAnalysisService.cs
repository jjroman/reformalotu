using YoutubeAnalyzer.Models;

namespace YoutubeAnalyzer.Services;

internal static class CommentAnalysisService
{
    public static AnalysisResult Analyze(IReadOnlyCollection<YoutubeComment> comments)
    {
        ArgumentNullException.ThrowIfNull(comments);

        var allComments = FlattenComments(comments).ToList();
        var totalLikes = allComments.Aggregate(
            0L,
            (sum, comment) => checked(sum + comment.Likes));

        return new AnalysisResult
        {
            TotalComments = allComments.Count,
            TotalLikes = totalLikes,
            AverageLikesPerComment = allComments.Count == 0
                ? 0
                : (double)totalLikes / allComments.Count
        };
    }

    private static IEnumerable<YoutubeComment> FlattenComments(
        IEnumerable<YoutubeComment> comments)
    {
        foreach (var comment in comments)
        {
            yield return comment;

            foreach (var reply in FlattenComments(comment.Replies))
            {
                yield return reply;
            }
        }
    }
}
