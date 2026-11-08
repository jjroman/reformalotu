namespace Reformk.Data;

public sealed class ProjectInput
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public ReformProject Project { get; set; } = null!;
    public string Kind { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public string? Specification { get; set; }
    public double? Quantity { get; set; }
    public string? Unit { get; set; }
    public string? Category { get; set; }
    public string? Level { get; set; }
    public int? Headcount { get; set; }
    public double? EstimatedHours { get; set; }
    public string? Notes { get; set; }
    public bool IsManual { get; set; }
    public ICollection<ProjectTask> Tasks { get; set; } = [];
}
