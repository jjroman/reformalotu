namespace YoutubeAnalyzer.Models;

/// <summary>
/// Unit-cost inputs (insumos unitarios) extracted from a video transcript.
/// Every item carries a confidence from 1 (fully inferred) to 10 (stated verbatim).
/// </summary>
public sealed record VideoInputs
{
    public IReadOnlyList<MaterialInput> Materials { get; init; } = [];
    public IReadOnlyList<EquipmentInput> Equipment { get; init; } = [];
    public IReadOnlyList<LaborInput> Labor { get; init; } = [];
}

public sealed record MaterialInput
{
    public string Name { get; init; } = "";
    public string? Brand { get; init; }

    /// <summary>Type, grade, color, dimensions or any other detail that identifies the material.</summary>
    public string? Specification { get; init; }
    public double? Quantity { get; init; }
    public string? Unit { get; init; }
    public string? Notes { get; init; }

    /// <summary>1 = completely inferred, 10 = taken verbatim from the transcript.</summary>
    public int Confidence { get; init; }
}

public sealed record EquipmentInput
{
    public string Name { get; init; } = "";

    /// <summary>manual_tool, power_tool, machinery, safety or other.</summary>
    public string Category { get; init; } = "other";
    public string? Brand { get; init; }
    public string? Specification { get; init; }
    public double? Quantity { get; init; }
    public string? Notes { get; init; }
    public int Confidence { get; init; }
}

public sealed record LaborInput
{
    /// <summary>Trade, e.g. carpintero, albañil, electricista.</summary>
    public string Role { get; init; } = "";

    /// <summary>"lead" for the main trade, "helper" for assistants.</summary>
    public string Level { get; init; } = "lead";
    public int? Headcount { get; init; }
    public string? Tasks { get; init; }
    public double? EstimatedHours { get; init; }
    public string? Notes { get; init; }
    public int Confidence { get; init; }
}
