namespace login.Services;

public sealed class ProjectAiOptions
{
    public const string SectionName = "ProjectAi";


    public bool Enabled { get; init; }
    public string? Endpoint { get; init; }
    public string? Deployment { get; init; }
}
