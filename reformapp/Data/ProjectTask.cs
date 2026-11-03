namespace ReformApp.Data;

public sealed class ProjectTask
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public ReformProject Project { get; set; } = null!;
    public int Order { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsDone { get; set; }
    public ICollection<ProjectInput> Inputs { get; set; } = [];
}
