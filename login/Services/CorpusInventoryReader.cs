using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace login.Services;

public sealed record CorpusVideo(
    string VideoId,
    string Title,
    string TranscriptText,
    string Categories,
    string Keywords,
    IReadOnlyList<string> Materials,
    IReadOnlyList<string> Equipment,
    CorpusSemanticIndex? SemanticIndex = null);

/// <summary>Artifact produced by youtube-analyzer, already validated against the current transcript.</summary>
public sealed record CorpusSemanticIndex(
    string Summary,
    float[] Embedding,
    string TranscriptHash,
    string SummaryModel,
    string EmbeddingModel,
    DateTimeOffset GeneratedAt);

public sealed record CorpusInventory(
    int FolderCount,
    IReadOnlyList<CorpusVideo> Videos,
    IReadOnlyDictionary<string, int> OmittedByReason);

public sealed class CorpusInventoryReader(
    IWebHostEnvironment environment,
    IConfiguration configuration,
    IOptions<VideoCatalogOptions> options,
    IOptions<VideoInventoryOptions> inventoryOptions)
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

            var materials = FindInventoryTerms(normalizedText, inventoryOptions.Value.Materials);
            var equipment = FindInventoryTerms(normalizedText, inventoryOptions.Value.Equipment);
            videos.Add(new CorpusVideo(
                videoId,
                ReadTitle(folder),
                transcript,
                string.Join("|", matchedCategories.Distinct(StringComparer.OrdinalIgnoreCase)),
                string.Join("|", matchedKeywords.Distinct(StringComparer.OrdinalIgnoreCase)),
                materials,
                equipment,
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
        if (!string.IsNullOrWhiteSpace(transcript) &&
            Regex.IsMatch(Regex.Replace(transcript, @"\[Música\]", " ", RegexOptions.IgnoreCase), @"\p{L}{3,}"))
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

            return new CorpusSemanticIndex(
                summary.GetString()!,
                embedding.EnumerateArray().Select(value => value.GetSingle()).ToArray(),
                currentHash,
                summaryModel.GetString()!,
                embeddingModel.GetString()!,
                generated);
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

    private static IReadOnlyList<string> FindInventoryTerms(
        string normalizedText,
        IEnumerable<VideoInventoryTermOptions> terms) =>
        terms
            .Where(term => term.Keywords.Any(keyword =>
                ContainsTerm(normalizedText, NormalizeForMatching(keyword))))
            .Select(term => term.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static string NormalizeForMatching(string value) =>
        Regex.Replace(TextNormalizer.Normalize(value), @"[^\p{L}\p{N}]+", " ").Trim();

    private static void AddOmission(IDictionary<string, int> omitted, string reason)
    {
        omitted[reason] = omitted.TryGetValue(reason, out var count) ? count + 1 : 1;
    }
}
