using System.Text.RegularExpressions;
using login.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace login.Services;

public sealed record VideoRecommendation(VideoCatalogItem Video, int Score, string Explanation);

public sealed class VideoRecommendationService(
    ApplicationDbContext dbContext,
    IOptions<VideoCatalogOptions> options)
{
    private const int MinimumRelevanceScore = 7;

    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
    {
        "a", "ahora", "algo", "al", "como", "con", "cual", "cuando", "de", "del",
        "desde", "donde", "el", "ella", "ellas", "ellos", "en", "entre", "era",
        "es", "esa", "ese", "eso", "esta", "este", "esto", "fue", "ha", "hace",
        "hacer", "hacia", "hasta", "hay", "la", "las", "lo", "los", "mas", "mi",
        "mis", "muy", "no", "nos", "o", "para", "pero", "por", "porque", "que",
        "se", "ser", "si", "sin", "sobre", "su", "sus", "tambien", "tener", "todo",
        "un", "una", "uno", "unos", "unas", "y", "ya", "quiero", "queremos",
        "ver", "foto", "imagen", "inspiracion", "referencia", "url"
    };

    private static readonly Regex MarkdownImagePattern = new(
        @"!\[[^\]]*\]\([^)]+\)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex UrlPattern = new(
        @"(?i)\b(?:https?://|www\.)[^\s<>()\]]+|\b(?:[\p{L}\p{N}-]+\.)+[\p{L}]{2,}(?:/[^\s<>()\]]*|#[^\s<>()\]]*|\?[^\s<>()\]]*)?",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex TermSeparatorPattern = new(
        @"[^\p{L}\p{N}]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly IReadOnlyList<Concept> Spaces =
    [
        new("patio o exterior", ["patio", "terraza", "jardin", "exterior"]),
        new("cocina", ["cocina"]),
        new("baño", ["bano", "ducha", "lavabo"]),
        new("salón", ["salon", "sala de estar", "living"]),
        new("dormitorio", ["dormitorio", "habitacion", "recamara"])
    ];

    private static readonly IReadOnlyList<Concept> Objectives =
    [
        new("pavimento", ["pavimento", "suelo", "solado", "baldosa", "baldosas", "azulejo", "azulejos", "ceramica"]),
        new("vegetación", ["vegetacion", "plantas", "jardineria", "paisajismo", "jardineras"]),
        new("almacenamiento", ["almacenamiento", "almacenaje", "organizacion", "organizar", "despensa", "cajones"]),
        new("muebles", ["mueble", "muebles", "mobiliario", "armario", "armarios", "frentes"]),
        new("amplitud", ["amplitud", "amplio", "amplia", "espacioso", "espaciosa", "optimizar espacio"]),
        new("sanitarios", ["sanitario", "sanitarios", "inodoro", "inodoros", "wc"])
    ];

    private static readonly IReadOnlyList<Concept> StylesAndMaterials =
    [
        new("madera", ["madera", "maderas"]),
        new("cerámica", ["ceramica", "porcelanico", "porcelanica"]),
        new("plantas", ["plantas", "vegetacion"]),
        new("luz cálida", ["luz calida", "iluminacion calida", "luz ambiental"]),
        new("estilo moderno", ["moderno", "moderna", "minimalista", "rustico", "rustica"])
    ];

    private static readonly string[] LiveVideoTerms = ["directo", "streaming", "live"];

    public async Task<IReadOnlyList<VideoRecommendation>> RecommendAsync(
        string description,
        string? category,
        CancellationToken cancellationToken = default)
    {
        var intent = GetIntent(description, category);
        if (intent.Spaces.Count == 0 && intent.Objectives.Count == 0 && intent.Styles.Count == 0)
        {
            return [];
        }

        var videos = await dbContext.Videos.AsNoTracking().ToListAsync(cancellationToken);
        return videos
            .Select(video => ScoreVideo(video, intent))
            .Where(recommendation => recommendation.Score >= MinimumRelevanceScore)
            .OrderByDescending(recommendation => recommendation.Score)
            .ThenBy(recommendation => recommendation.Video.VideoId, StringComparer.Ordinal)
            .Take(3)
            .ToArray();
    }

    public string ExplainMatch(string description, string? category, VideoCatalogItem video) =>
        CreateExplanation(video, GetIntent(description, category));

    private static VideoRecommendation ScoreVideo(VideoCatalogItem video, ProjectIntent intent)
    {
        var title = NormalizeText(video.Title);
        var metadata = NormalizeText(string.Join(
            ' ',
            video.Categories,
            video.Keywords,
            string.Join(' ', video.Materials),
            string.Join(' ', video.Equipment)));
        var transcript = NormalizeText(video.TranscriptText);

        var score = 0;
        score += ScoreConcepts(intent.Spaces, title, metadata, transcript, 8, 6, 2);
        score += ScoreConcepts(intent.Objectives, title, metadata, transcript, 10, 7, 3);
        score += ScoreConcepts(intent.Styles, title, metadata, transcript, 3, 2, 1);
        score -= ScoreConflictingSpaces(intent.Spaces, title, metadata);

        var isLive = LiveVideoTerms.Any(term => ContainsTerm(title, term) || ContainsTerm(metadata, term));
        if (isLive)
        {
            score -= 14;
        }

        return new VideoRecommendation(
            video,
            Math.Max(0, score),
            CreateExplanation(video, intent));
    }

    private static int ScoreConcepts(
        IReadOnlyList<Concept> concepts,
        string title,
        string metadata,
        string transcript,
        int titleWeight,
        int metadataWeight,
        int transcriptWeight)
    {
        var score = 0;
        foreach (var concept in concepts)
        {
            if (Matches(title, concept))
            {
                score += titleWeight;
            }
            else if (Matches(metadata, concept))
            {
                score += metadataWeight;
            }
            else if (Matches(transcript, concept))
            {
                score += transcriptWeight;
            }
        }

        return score;
    }

    private static int ScoreConflictingSpaces(
        IReadOnlyList<Concept> projectSpaces,
        string title,
        string metadata)
    {
        if (projectSpaces.Count == 0)
        {
            return 0;
        }

        var conflictingTitleSpaces = Spaces
            .Where(space => !projectSpaces.Contains(space) && Matches(title, space))
            .Count();
        var conflictingMetadataSpaces = Spaces
            .Where(space => !projectSpaces.Contains(space) && Matches(metadata, space))
            .Count();

        return (conflictingTitleSpaces * 12) + (conflictingMetadataSpaces * 8);
    }

    private ProjectIntent GetIntent(string description, string? category)
    {
        var normalizedDescription = NormalizeText(description);
        var projectSpaces = FindConcepts(normalizedDescription, Spaces).ToList();

        var categoryOptions = options.Value.Categories.FirstOrDefault(
            item => string.Equals(item.Name, category, StringComparison.OrdinalIgnoreCase));
        if (categoryOptions is not null)
        {
            projectSpaces.AddRange(FindConcepts(NormalizeText(categoryOptions.Name), Spaces));
        }

        return new ProjectIntent(
            projectSpaces.Distinct().ToArray(),
            FindConcepts(normalizedDescription, Objectives).ToArray(),
            FindConcepts(normalizedDescription, StylesAndMaterials).ToArray());
    }

    private static IEnumerable<Concept> FindConcepts(string text, IEnumerable<Concept> concepts) =>
        concepts.Where(concept => Matches(text, concept));

    private static bool Matches(string text, Concept concept) =>
        concept.Terms.Any(term => ContainsTerm(text, term));

    private static bool ContainsTerm(string text, string term)
    {
        var normalizedTerm = NormalizeText(term).Trim();
        return text.Contains($" {normalizedTerm} ", StringComparison.Ordinal);
    }

    private static string NormalizeText(string value)
    {
        var withoutImageMarkdown = MarkdownImagePattern.Replace(value, " ");
        var withoutUrls = UrlPattern.Replace(withoutImageMarkdown, " ");
        var terms = TermSeparatorPattern.Split(TextNormalizer.Normalize(withoutUrls))
            .Where(term => term.Length > 1 && !StopWords.Contains(term));
        return $" {string.Join(' ', terms)} ";
    }

    private static string CreateExplanation(VideoCatalogItem video, ProjectIntent intent)
    {
        var title = NormalizeText(video.Title);
        var metadata = NormalizeText(string.Join(
            ' ',
            video.Categories,
            video.Keywords,
            string.Join(' ', video.Materials),
            string.Join(' ', video.Equipment)));
        var transcript = NormalizeText(video.TranscriptText);
        var matched = intent.Spaces
            .Concat(intent.Objectives)
            .Concat(intent.Styles)
            .Where(concept => Matches(title, concept) || Matches(metadata, concept) || Matches(transcript, concept))
            .Select(concept => concept.Label)
            .Distinct(StringComparer.Ordinal)
            .Take(3)
            .ToArray();

        return matched.Length == 0
            ? "Aporta una referencia relacionada con tu proyecto."
            : $"Encaja con {string.Join(", ", matched)}.";
    }

    private sealed record Concept(string Label, string[] Terms);

    private sealed record ProjectIntent(
        IReadOnlyList<Concept> Spaces,
        IReadOnlyList<Concept> Objectives,
        IReadOnlyList<Concept> Styles);
}
