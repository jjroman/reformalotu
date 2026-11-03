using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace ReformApp.Services;

public sealed record CorpusVideo(
    string VideoId,
    string Title,
    string TranscriptText,
    string Categories,
    string Keywords,
    CorpusSemanticIndex? SemanticIndex = null);

public sealed record CorpusVideoInput(
    string SourceId,
    string Kind,
    string Name,
    string? Brand,
    string? Specification,
    double? Quantity,
    string? Unit,
    string? Category,
    string? Level,
    int? Headcount,
    double? EstimatedHours,
    string? Notes,
    int Confidence);

public sealed record CorpusVideoTask(
    int Order,
    string Title,
    string Description,
    double? EstimatedMinutes,
    IReadOnlyList<string> MaterialIds,
    IReadOnlyList<string> EquipmentIds,
    IReadOnlyList<string> LaborIds,
    int Confidence);

/// <summary>Artifact produced by youtube-analyzer, already validated against the current transcript.</summary>
public sealed record CorpusSemanticIndex(
    string Summary,
    float[] Embedding,
    string TranscriptHash,
    string SummaryModel,
    string EmbeddingModel,
    DateTimeOffset GeneratedAt,
    int PromptVersion,
    IReadOnlyList<CorpusVideoInput> Inputs,
    IReadOnlyList<CorpusVideoTask> Tasks);

public sealed record CorpusInventory(
    int FolderCount,
    IReadOnlyList<CorpusVideo> Videos,
    IReadOnlyDictionary<string, int> OmittedByReason);

public sealed class CorpusInventoryReader(
    IWebHostEnvironment environment,
    IConfiguration configuration,
    IOptions<VideoCatalogOptions> options)
{
    private static readonly Regex VideoIdPattern = new("^[A-Za-z0-9_-]{11}$", RegexOptions.Compiled);

    public CorpusInventory Read()
    {
        var configuredPath = configuration["Corpus:Path"];
        var root = string.IsNullOrWhiteSpace(configuredPath)
            ? Path.GetFullPath(Path.Combine(environment.ContentRootPath, "..", "youtube-analyzer", "output"))
            : Path.GetFullPath(Path.Combine(environment.ContentRootPath, configuredPath));

        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException($"The video corpus directory was not found: {root}");
        }

        var videos = new List<CorpusVideo>();
        var omitted = new Dictionary<string, int>(StringComparer.Ordinal);
        var folders = Directory.EnumerateDirectories(root).ToArray();

        foreach (var folder in folders)
        {
            var videoId = Path.GetFileName(folder);
            if (!VideoIdPattern.IsMatch(videoId))
            {
                AddOmission(omitted, "invalid-video-id");
                continue;
            }

            var transcript = ReadTranscript(folder, out var reason);
            if (transcript is null)
            {
                AddOmission(omitted, reason);
                continue;
            }

            var normalizedText = NormalizeForMatching(transcript);
            var matchedCategories = new List<string>();
            var matchedKeywords = new List<string>();

            foreach (var category in options.Value.Categories)
            {
                var categoryMatches = category.Keywords
                    .Where(keyword => ContainsTerm(normalizedText, NormalizeForMatching(keyword)))
                    .ToArray();

                if (categoryMatches.Length == 0)
                {
                    continue;
                }

                matchedCategories.Add(category.Name);
                matchedKeywords.AddRange(categoryMatches);
            }

            videos.Add(new CorpusVideo(
                videoId,
                ReadTitle(folder),
                transcript,
                string.Join("|", matchedCategories.Distinct(StringComparer.OrdinalIgnoreCase)),
                string.Join("|", matchedKeywords.Distinct(StringComparer.OrdinalIgnoreCase)),
                ReadSemanticIndex(folder)));
        }

        return new CorpusInventory(folders.Length, videos, omitted);
    }

    private static string ReadTitle(string folder)
    {
        var metadataPath = Path.Combine(folder, "metadata.json");
        if (!File.Exists(metadataPath))
        {
            return string.Empty;
        }

        using var metadata = JsonDocument.Parse(File.ReadAllText(metadataPath));
        return metadata.RootElement.TryGetProperty("Title", out var title) &&
            title.ValueKind == JsonValueKind.String
                ? title.GetString()?.Trim() ?? string.Empty
                : string.Empty;
    }

    private static string? ReadTranscript(string folder, out string reason)
    {
        var jsonPath = Path.Combine(folder, "transcript.json");
        var textPath = Path.Combine(folder, "transcript.txt");
        string? transcript = null;
        var jsonInvalid = false;
        var hadReadError = false;

        if (File.Exists(jsonPath))
        {
            try
            {
                transcript = ReadJsonTranscript(jsonPath);
                jsonInvalid = transcript is null;
            }
            catch (JsonException)
            {
                jsonInvalid = true;
            }
            catch (IOException)
            {
                hadReadError = true;
            }
            catch (UnauthorizedAccessException)
            {
                hadReadError = true;
            }
        }

        if (string.IsNullOrWhiteSpace(transcript) && File.Exists(textPath))
        {
            try
            {
                transcript = File.ReadAllText(textPath);
            }
            catch (IOException)
            {
                hadReadError = true;
            }
            catch (UnauthorizedAccessException)
            {
                hadReadError = true;
            }
        }

        transcript = transcript?.Trim();
        if (!string.IsNullOrWhiteSpace(transcript) && HasSpokenContent(transcript))
        {
            reason = string.Empty;
            return transcript;
        }

        reason = hadReadError
            ? "read-error"
            : jsonInvalid
                ? "invalid-transcript"
                : File.Exists(jsonPath) || File.Exists(textPath)
                    ? "empty-transcript"
                    : "transcript-missing";
        return null;
    }

    /// <summary>
    /// Ignores bracketed sound annotations ([Música], [Music]...) and the placeholder words the
    /// speech recognizer emits when nothing is said, so music-only videos are not imported.
    /// </summary>
    private static bool HasSpokenContent(string transcript)
    {
        var spoken = Regex.Replace(transcript, @"\[[^\]]*\]", " ");
        spoken = Regex.Replace(spoken, @"\b(foreign|thank you|thanks|gracias)\b", " ", RegexOptions.IgnoreCase);
        return Regex.IsMatch(spoken, @"\p{L}{3,}");
    }

    /// <summary>
    /// Reads semantic-index.json and returns it only when it matches the current transcript,
    /// so stale artifacts are treated as missing.
    /// </summary>
    private static CorpusSemanticIndex? ReadSemanticIndex(string folder)
    {
        var indexPath = Path.Combine(folder, "semantic-index.json");
        var transcriptPath = Path.Combine(folder, "transcript.json");

        if (!File.Exists(indexPath) || !File.Exists(transcriptPath))
        {
            return null;
        }

        try
        {
            using var transcript = JsonDocument.Parse(File.ReadAllText(transcriptPath));
            using var index = JsonDocument.Parse(File.ReadAllText(indexPath));
            var root = index.RootElement;

            if (!transcript.RootElement.TryGetProperty("Segments", out var segments) ||
                segments.ValueKind != JsonValueKind.Array ||
                !root.TryGetProperty("Summary", out var summary) || summary.ValueKind != JsonValueKind.String ||
                !root.TryGetProperty("TranscriptHash", out var hash) || hash.ValueKind != JsonValueKind.String ||
                !root.TryGetProperty("SummaryModel", out var summaryModel) || summaryModel.ValueKind != JsonValueKind.String ||
                !root.TryGetProperty("EmbeddingModel", out var embeddingModel) || embeddingModel.ValueKind != JsonValueKind.String ||
                !root.TryGetProperty("PromptVersion", out var promptVersion) || !promptVersion.TryGetInt32(out var version) ||
                !root.TryGetProperty("GeneratedAt", out var generatedAt) || !generatedAt.TryGetDateTimeOffset(out var generated) ||
                !root.TryGetProperty("Embedding", out var embedding) || embedding.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            // Must stay identical to SemanticIndexService.ComputeTranscriptHash in youtube-analyzer.
            var text = string.Join(
                "\n",
                segments.EnumerateArray().Select(segment =>
                    segment.ValueKind == JsonValueKind.Object &&
                    segment.TryGetProperty("Text", out var value) &&
                    value.ValueKind == JsonValueKind.String
                        ? value.GetString()
                        : string.Empty));
            var currentHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

            if (!string.Equals(hash.GetString(), currentHash, StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(summary.GetString()))
            {
                return null;
            }

            var inputs = version >= 3 ? ReadInputs(root) : [];
            var tasks = version >= 3 ? ReadTasks(root) : [];
            return new CorpusSemanticIndex(
                summary.GetString()!,
                embedding.EnumerateArray().Select(value => value.GetSingle()).ToArray(),
                currentHash,
                summaryModel.GetString()!,
                embeddingModel.GetString()!,
                generated,
                version,
                inputs,
                tasks);
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException or InvalidOperationException or FormatException)
        {
            return null;
        }
    }

    private static string? ReadJsonTranscript(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        if (!document.RootElement.TryGetProperty("Segments", out var segments) ||
            segments.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        return string.Join(
            " ",
            segments.EnumerateArray()
                .Where(segment => segment.ValueKind == JsonValueKind.Object &&
                                  segment.TryGetProperty("Text", out var text) &&
                                  text.ValueKind == JsonValueKind.String)
                .Select(segment => segment.GetProperty("Text").GetString())
                .Where(text => !string.IsNullOrWhiteSpace(text)));
    }

    private static bool ContainsTerm(string normalizedText, string normalizedTerm) =>
        !string.IsNullOrWhiteSpace(normalizedTerm) &&
        $" {normalizedText} ".Contains($" {normalizedTerm} ", StringComparison.Ordinal);

    private static IReadOnlyList<CorpusVideoInput> ReadInputs(JsonElement root)
    {
        if (!root.TryGetProperty("Inputs", out var inputs) ||
            inputs.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        var result = new List<CorpusVideoInput>();
        AddInputs(inputs, "Materials", "material", result, item => new CorpusVideoInput(
            RequiredString(item, "Id"),
            "material",
            RequiredString(item, "Name"),
            OptionalString(item, "Brand"),
            OptionalString(item, "Specification"),
            OptionalDouble(item, "Quantity"),
            OptionalString(item, "Unit"),
            null,
            null,
            null,
            null,
            OptionalString(item, "Notes"),
            OptionalInt(item, "Confidence")));
        AddInputs(inputs, "Equipment", "equipment", result, item => new CorpusVideoInput(
            RequiredString(item, "Id"),
            "equipment",
            RequiredString(item, "Name"),
            OptionalString(item, "Brand"),
            OptionalString(item, "Specification"),
            OptionalDouble(item, "Quantity"),
            null,
            OptionalString(item, "Category"),
            null,
            null,
            null,
            OptionalString(item, "Notes"),
            OptionalInt(item, "Confidence")));
        AddInputs(inputs, "Labor", "labor", result, item => new CorpusVideoInput(
            RequiredString(item, "Id"),
            "labor",
            RequiredString(item, "Role"),
            null,
            null,
            null,
            null,
            null,
            OptionalString(item, "Level"),
            OptionalInt(item, "Headcount"),
            OptionalDouble(item, "EstimatedHours"),
            OptionalString(item, "Notes"),
            OptionalInt(item, "Confidence")));

        if (result.Any(input => string.IsNullOrWhiteSpace(input.SourceId) || string.IsNullOrWhiteSpace(input.Name)) ||
            result.Select(input => input.SourceId).Distinct(StringComparer.Ordinal).Count() != result.Count)
        {
            throw new JsonException("Semantic index input IDs must be unique and input names must not be empty.");
        }

        return result;
    }

    private static void AddInputs(
        JsonElement inputs,
        string propertyName,
        string kind,
        ICollection<CorpusVideoInput> destination,
        Func<JsonElement, CorpusVideoInput> create)
    {
        if (!inputs.TryGetProperty(propertyName, out var array))
        {
            return;
        }

        if (array.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException($"Semantic index Inputs.{propertyName} must be an array.");
        }

        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException($"Semantic index Inputs.{propertyName} entries must be objects.");
            }

            var input = create(item);
            destination.Add(input with { Kind = kind });
        }
    }

    private static IReadOnlyList<CorpusVideoTask> ReadTasks(JsonElement root)
    {
        if (!root.TryGetProperty("Tasks", out var tasks))
        {
            return [];
        }

        if (tasks.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("Semantic index Tasks must be an array.");
        }

        var result = tasks.EnumerateArray().Select(task =>
        {
            if (task.ValueKind != JsonValueKind.Object ||
                !task.TryGetProperty("Order", out var order) || !order.TryGetInt32(out var taskOrder))
            {
                throw new JsonException("Semantic index task entries must contain an order.");
            }

            return new CorpusVideoTask(
                taskOrder,
                RequiredString(task, "Title"),
                OptionalString(task, "Description") ?? string.Empty,
                OptionalDouble(task, "EstimatedMinutes"),
                ReadStringArray(task, "MaterialIds"),
                ReadStringArray(task, "EquipmentIds"),
                ReadStringArray(task, "LaborIds"),
                OptionalInt(task, "Confidence"));
        }).OrderBy(task => task.Order).ToArray();

        if (result.Any(task => task.Order < 1 || string.IsNullOrWhiteSpace(task.Title)) ||
            result.Select(task => task.Order).Distinct().Count() != result.Length)
        {
            throw new JsonException("Semantic index task orders must be positive, unique, and have titles.");
        }

        return result;
    }

    private static List<string> ReadStringArray(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return [];
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException($"Semantic index {propertyName} must be an array.");
        }

        return value.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString()!)
            .ToList();
    }

    private static string RequiredString(JsonElement element, string propertyName) =>
        OptionalString(element, propertyName)
        ?? throw new JsonException($"Semantic index field {propertyName} must be a non-empty string.");

    private static string? OptionalString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static double? OptionalDouble(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Number &&
        value.TryGetDouble(out var number)
            ? number
            : null;

    private static int OptionalInt(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.TryGetInt32(out var number)
            ? number
            : 0;

    private static string NormalizeForMatching(string value) =>
        Regex.Replace(TextNormalizer.Normalize(value), @"[^\p{L}\p{N}]+", " ").Trim();

    private static void AddOmission(IDictionary<string, int> omitted, string reason)
    {
        omitted[reason] = omitted.TryGetValue(reason, out var count) ? count + 1 : 1;
    }
}
