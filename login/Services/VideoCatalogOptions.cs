namespace login.Services;

public sealed class VideoCatalogOptions
{
    public List<VideoCategoryOptions> Categories { get; init; } = [];
}

public sealed class VideoCategoryOptions
{
    public string Name { get; init; } = string.Empty;
    public string[] Keywords { get; init; } = [];
}
