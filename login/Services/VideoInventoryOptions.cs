namespace login.Services;

public sealed class VideoInventoryOptions
{
    public List<VideoInventoryTermOptions> Materials { get; init; } = [];
    public List<VideoInventoryTermOptions> Equipment { get; init; } = [];
}

public sealed class VideoInventoryTermOptions
{
    public string Name { get; init; } = string.Empty;
    public string[] Keywords { get; init; } = [];
}
