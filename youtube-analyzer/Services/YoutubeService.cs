using Google.Apis.Services;
using Google.Apis.YouTube.v3;
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
            var response = await NetworkRetry.ExecuteAsync(
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
