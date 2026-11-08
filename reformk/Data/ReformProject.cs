using Microsoft.AspNetCore.Identity;

namespace Reformk.Data;

public sealed class ReformProject
{
    public Guid Id { get; set; }
    public string OwnerId { get; set; } = string.Empty;
    public IdentityUser Owner { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Category { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? SelectedVideoId { get; set; }
    public VideoCatalogItem? SelectedVideo { get; set; }
    public string? WorkDescription { get; set; }
    public ICollection<ProjectInput> Inputs { get; set; } = [];
    public ICollection<ProjectTask> Tasks { get; set; } = [];
}
