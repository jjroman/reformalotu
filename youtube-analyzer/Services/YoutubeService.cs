using Google;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using YoutubeAnalyzer.Models;
using YoutubeApiService = Google.Apis.YouTube.v3.YouTubeService;

namespace YoutubeAnalyzer.Services;

internal sealed class YoutubeService : IDisposable
{
    private readonly YoutubeApiService _youtube;

    public YoutubeService(string apiKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

        _youtube = new YoutubeApiService(new BaseClientService.Initializer
        {
            ApiKey = apiKey,
            ApplicationName = "ReformaloTu-CustomerDiscovery"
        });
    }

    public async Task<List<YoutubeComment>> GetCommentsAsync(
        string videoId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(videoId);

        var comments = new List<YoutubeComment>();
        string? nextPageToken = null;

        do
        {
            CommentThreadListResponse response;

            try
            {
                response = await NetworkRetry.ExecuteAsync(
                    async retryToken =>
                    {
                        var request = _youtube.CommentThreads.List("snippet");
                        request.VideoId = videoId;
                        request.MaxResults = 100;
                        request.PageToken = nextPageToken;
                        request.TextFormat = CommentThreadsResource.ListRequest.TextFormatEnum.PlainText;

                        return await request.ExecuteAsync(retryToken);
                    },
                    $"fetching comments for video {videoId}",
                    cancellationToken);
            }
            catch (GoogleApiException exception) when (IsCommentsDisabled(exception))
            {
                Console.WriteLine($"Comments are disabled for video {videoId}; continuing without them.");
                return comments;
            }

            foreach (var item in response.Items ?? [])
            {
                var threadSnippet = item.Snippet;
                var topLevelComment = threadSnippet?.TopLevelComment;
                var topComment = topLevelComment?.Snippet;

                if (topComment is null || string.IsNullOrWhiteSpace(topLevelComment?.Id))
                {
                    continue;
                }

                var replies = threadSnippet?.TotalReplyCount > 0
                    ? await GetRepliesAsync(topLevelComment.Id, cancellationToken)
                    : [];

                comments.Add(new YoutubeComment
                {
                    Author = topComment.AuthorDisplayName ?? "",
                    Text = topComment.TextDisplay ?? "",
                    Likes = topComment.LikeCount ?? 0L,
                    PublishedAt = topComment.PublishedAtDateTimeOffset,
                    Replies = replies
                });
            }

            nextPageToken = response.NextPageToken;
        }
        while (!string.IsNullOrEmpty(nextPageToken));

        return comments;
    }

    /// <summary>
    /// Returns the comment count YouTube reports for the video (replies included),
    /// or 0 when the video hides it because comments are disabled. Costs 1 quota unit.
    /// </summary>
    public async Task<long> GetCommentCountAsync(
        string videoId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(videoId);

        var response = await NetworkRetry.ExecuteAsync(
            retryToken =>
            {
                var request = _youtube.Videos.List("statistics");
                request.Id = videoId;

                return request.ExecuteAsync(retryToken);
            },
            $"fetching comment count for video {videoId}",
            cancellationToken);

        return (long?)response.Items?.FirstOrDefault()?.Statistics?.CommentCount ?? 0L;
    }

    internal static bool IsQuotaExceeded(GoogleApiException exception) =>
        exception.Error?.Errors?.Any(error => error.Reason is "quotaExceeded" or "dailyLimitExceeded") == true;

    /// <summary>The daily quota resets at midnight Pacific Time.</summary>
    internal static DateTimeOffset GetNextQuotaReset()
    {
        TimeZoneInfo pacific;

        try
        {
            pacific = TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles");
        }
        catch (TimeZoneNotFoundException)
        {
            pacific = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
        }

        var nowPacific = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, pacific);
        var midnight = new DateTime(nowPacific.Year, nowPacific.Month, nowPacific.Day).AddDays(1);

        return new DateTimeOffset(midnight, pacific.GetUtcOffset(midnight));
    }

    private static bool IsCommentsDisabled(GoogleApiException exception) =>
        exception.Error?.Errors?.Any(error => error.Reason == "commentsDisabled") == true;

    /// <summary>Fetches title and channel metadata in batches of 50 (1 quota unit per batch).</summary>
    public async Task<List<VideoMetadata>> GetVideoMetadataAsync(
        IReadOnlyCollection<string> videoIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(videoIds);

        var result = new List<VideoMetadata>();

        foreach (var batch in videoIds.Chunk(50))
        {
            var response = await NetworkRetry.ExecuteAsync(
                retryToken =>
                {
                    var request = _youtube.Videos.List("snippet");
                    request.Id = string.Join(',', batch);
                    request.MaxResults = 50;

                    return request.ExecuteAsync(retryToken);
                },
                "fetching video metadata",
                cancellationToken);

            foreach (var item in response.Items ?? [])
            {
                if (string.IsNullOrWhiteSpace(item.Id) || item.Snippet is null)
                {
                    continue;
                }

                result.Add(new VideoMetadata
                {
                    VideoId = item.Id,
                    Title = item.Snippet.Title ?? "",
                    ChannelId = item.Snippet.ChannelId ?? "",
                    ChannelTitle = item.Snippet.ChannelTitle ?? "",
                    PublishedAt = item.Snippet.PublishedAtDateTimeOffset
                });
            }
        }

        return result;
    }

    public async Task<List<string>> GetVideoIdsForChannelAsync(
        string channelIdentifier,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channelIdentifier);

        var normalizedChannel = channelIdentifier.Trim();
        var channelResponse = await NetworkRetry.ExecuteAsync(
            retryToken =>
            {
                var request = _youtube.Channels.List("contentDetails");

                if (normalizedChannel.StartsWith("UC", StringComparison.OrdinalIgnoreCase))
                {
                    request.Id = normalizedChannel;
                }
                else
                {
                    request.ForHandle = normalizedChannel.TrimStart('@');
                }

                return request.ExecuteAsync(retryToken);
            },
            $"looking up channel {channelIdentifier}",
            cancellationToken);
        var uploadsPlaylistId = channelResponse.Items?
            .FirstOrDefault()?
            .ContentDetails?
            .RelatedPlaylists?
            .Uploads;

        if (string.IsNullOrWhiteSpace(uploadsPlaylistId))
        {
            throw new InvalidOperationException(
                $"Could not find an accessible channel or its uploads playlist: {channelIdentifier}");
        }

        var videoIds = new List<string>();
        string? nextPageToken = null;

        do
        {
            var response = await NetworkRetry.ExecuteAsync(
                retryToken =>
                {
                    var request = _youtube.PlaylistItems.List("contentDetails");
                    request.PlaylistId = uploadsPlaylistId;
                    request.MaxResults = 50;
                    request.PageToken = nextPageToken;

                    return request.ExecuteAsync(retryToken);
                },
                $"listing uploads for channel {channelIdentifier}",
                cancellationToken);

            foreach (var item in response.Items ?? [])
            {
                var videoId = item.ContentDetails?.VideoId;

                if (!string.IsNullOrWhiteSpace(videoId))
                {
                    videoIds.Add(videoId);
                }
            }

            nextPageToken = response.NextPageToken;
        }
        while (!string.IsNullOrEmpty(nextPageToken));

        return videoIds;
    }

    private async Task<List<YoutubeComment>> GetRepliesAsync(
        string parentCommentId,
        CancellationToken cancellationToken)
    {
        var replies = new List<YoutubeComment>();
        string? nextPageToken = null;

        do
        {
            var response = await NetworkRetry.ExecuteAsync(
                retryToken =>
                {
                    var request = _youtube.Comments.List("snippet");
                    request.ParentId = parentCommentId;
                    request.MaxResults = 100;
                    request.PageToken = nextPageToken;
                    request.TextFormat = CommentsResource.ListRequest.TextFormatEnum.PlainText;

                    return request.ExecuteAsync(retryToken);
                },
                $"fetching replies for comment {parentCommentId}",
                cancellationToken);

            foreach (var comment in response.Items ?? [])
            {
                var snippet = comment.Snippet;

                if (snippet is null)
                {
                    continue;
                }

                replies.Add(new YoutubeComment
                {
                    Author = snippet.AuthorDisplayName ?? "",
                    Text = snippet.TextDisplay ?? "",
                    Likes = snippet.LikeCount ?? 0L,
                    PublishedAt = snippet.PublishedAtDateTimeOffset
                });
            }

            nextPageToken = response.NextPageToken;
        }
        while (!string.IsNullOrEmpty(nextPageToken));

        return replies;
    }

    public void Dispose() => _youtube.Dispose();
}
