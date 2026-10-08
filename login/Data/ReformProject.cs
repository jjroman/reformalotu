using Microsoft.AspNetCore.Identity;

namespace login.Data;

public sealed class ReformProject
{
    public Guid Id { get; set; }
    public string OwnerId { get; set; } = string.Empty;
    public IdentityUser Owner { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Category { get; set; }
    public DateTime CreatedAt { get; set; }
    public ICollection<ProjectRecommendation> Recommendations { get; set; } = [];
}
