using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.ClientModel;
using Azure.AI.OpenAI;
using Azure.Identity;
using OpenAI.Chat;
using OpenAI.Embeddings;
using YoutubeAnalyzer.Models;

namespace YoutubeAnalyzer.Services;

internal sealed class SemanticIndexSettings
{
    public string Endpoint { get; init; } = "";
    public string SummaryDeployment { get; init; } = "";
    public string EmbeddingDeployment { get; init; } = "";
    public int EmbeddingDimensions { get; init; } = 1536;
    public int MaxTranscriptCharacters { get; init; } = 60_000;

    /// <summary>Videos processed at the same time in --semantic-only mode.</summary>
    public int MaxConcurrency { get; init; } = 4;

    public bool IsConfigured =>
        Uri.TryCreate(Endpoint, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && !string.IsNullOrWhiteSpace(SummaryDeployment)
        && !string.IsNullOrWhiteSpace(EmbeddingDeployment)
        && EmbeddingDimensions > 0
        && MaxTranscriptCharacters > 0;
}

/// <summary>
/// Generates and caches, per video, a Spanish summary and its embedding.
/// </summary>
internal sealed class SemanticIndexService
{
    public const string FileName = "semantic-index.json";

    private const int PromptVersion = 2;

    private const string SummaryInstructions =
        "Eres un asistente que analiza vídeos de bricolaje y reformas del hogar para preparar análisis de insumos unitarios. "
        + "Con el transcript que recibirás devuelve un JSON con: "
        + "1) 'summary': resumen en español, en un único párrafo de 80 a 150 palabras, con el objetivo, los materiales y herramientas principales y los pasos clave. "
        + "2) 'materials': todos los materiales con el mayor detalle posible: nombre, marca, especificación (tipo, medidas, color, grado, etc.), cantidad y unidad. "
        + "3) 'equipment': herramientas y equipos (incluidos los de protección) con nombre, categoría, marca, especificación y cantidad. "
        + "4) 'labor': la mano de obra necesaria. Indica siempre el rol principal según el oficio (p. ej. trabajo en madera = carpintero; level 'lead') "
        + "y los ayudantes (level 'helper') con cuántos serían (headcount), sus tareas y, si es razonable, las horas estimadas. "
        + "Si un insumo no aparece explícito pero se puede inferir razonablemente del trabajo mostrado, inclúyelo con sus datos estimados. "
        + "A cada insumo asigna 'confidence' de 1 a 10: 1 = completamente inferido, 5 = deducido con apoyo parcial del transcript, 10 = sacado textualmente del transcript. "
        + "Usa null cuando un dato (marca, cantidad, unidad, etc.) no conste ni pueda estimarse; no inventes marcas. Escribe los textos en español.";

    private static readonly JsonSerializerOptions ResultJsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly BinaryData ResultSchema = BinaryData.FromString(BuildResultSchema());

    private sealed record SummaryResult
    {
        public string Summary { get; init; } = "";
        public IReadOnlyList<MaterialInput> Materials { get; init; } = [];
        public IReadOnlyList<EquipmentInput> Equipment { get; init; } = [];
        public IReadOnlyList<LaborInput> Labor { get; init; } = [];
    }

    private static string BuildResultSchema()
    {
        static object Nullable(string type) => new { type = new[] { type, "null" } };
        var confidence = new { type = "integer", description = "1 = completely inferred, 10 = verbatim from transcript" };

        static object Obj(Dictionary<string, object> properties) => new
        {
            type = "object",
            properties,
            required = properties.Keys.ToArray(),
            additionalProperties = false
        };

        static object Arr(object items) => new { type = "array", items };

        var material = Obj(new()
        {
            ["name"] = new { type = "string" },
            ["brand"] = Nullable("string"),
            ["specification"] = Nullable("string"),
            ["quantity"] = Nullable("number"),
            ["unit"] = Nullable("string"),
            ["notes"] = Nullable("string"),
            ["confidence"] = confidence
        });

        var equipment = Obj(new()
        {
            ["name"] = new { type = "string" },
            ["category"] = new { type = "string", @enum = new[] { "manual_tool", "power_tool", "machinery", "safety", "other" } },
            ["brand"] = Nullable("string"),
            ["specification"] = Nullable("string"),
            ["quantity"] = Nullable("number"),
            ["notes"] = Nullable("string"),
            ["confidence"] = confidence
        });

        var labor = Obj(new()
        {
            ["role"] = new { type = "string" },
            ["level"] = new { type = "string", @enum = new[] { "lead", "helper" } },
            ["headcount"] = Nullable("integer"),
            ["tasks"] = Nullable("string"),
            ["estimatedHours"] = Nullable("number"),
            ["notes"] = Nullable("string"),
            ["confidence"] = confidence
        });

        return JsonSerializer.Serialize(Obj(new()
        {
            ["summary"] = new { type = "string" },
            ["materials"] = Arr(material),
            ["equipment"] = Arr(equipment),
            ["labor"] = Arr(labor)
        }));
    }

    private static int ClampConfidence(int value) => Math.Clamp(value, 1, 10);

    private static VideoInputs Normalize(SummaryResult result) => new()
    {
        Materials = [.. result.Materials.Select(item => item with { Confidence = ClampConfidence(item.Confidence) })],
        Equipment = [.. result.Equipment.Select(item => item with { Confidence = ClampConfidence(item.Confidence) })],
        Labor = [.. result.Labor.Select(item => item with { Confidence = ClampConfidence(item.Confidence) })]
    };

    private readonly SemanticIndexSettings _settings;
    private readonly ChatClient _chatClient;
    private readonly EmbeddingClient _embeddingClient;

    public SemanticIndexService(SemanticIndexSettings settings, string? apiKey)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _settings = settings;
        var endpoint = new Uri(settings.Endpoint);
        // Falls back to Entra ID (az login) when no API key is provided.
        var client = string.IsNullOrWhiteSpace(apiKey)
            ? new AzureOpenAIClient(endpoint, new DefaultAzureCredential())
            : new AzureOpenAIClient(endpoint, new ApiKeyCredential(apiKey));
        _chatClient = client.GetChatClient(settings.SummaryDeployment);
        _embeddingClient = client.GetEmbeddingClient(settings.EmbeddingDeployment);
    }

    /// <summary>Hash of the transcript text; must match the one computed by the login project.</summary>
    public static string ComputeTranscriptHash(VideoTranscript transcript)
    {
        ArgumentNullException.ThrowIfNull(transcript);

        var text = string.Join("\n", transcript.Segments.Select(segment => segment.Text));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }

    /// <summary>
    /// Returns true when the artifact was generated (or reused) and is up to date.
    /// Reuses the cached summary when only the embedding model changed.
    /// </summary>
    public async Task<bool> EnsureAsync(
        string videoId,
        string videoOutputDirectory,
        VideoTranscript transcript,
        JsonSerializerOptions jsonOptions,
        CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(videoOutputDirectory, FileName);
        var transcriptHash = ComputeTranscriptHash(transcript);
        var cached = await TryReadAsync(videoId, path, jsonOptions, cancellationToken);

        var summaryCurrent = cached is not null
            && cached.TranscriptHash == transcriptHash
            && cached.SummaryModel == _settings.SummaryDeployment
            && cached.PromptVersion == PromptVersion;
        var embeddingCurrent = summaryCurrent
            && cached!.EmbeddingModel == _settings.EmbeddingDeployment
            && cached.EmbeddingDimensions == _settings.EmbeddingDimensions
            && cached.Embedding.Length == _settings.EmbeddingDimensions;

        if (embeddingCurrent)
        {
            return false;
        }

        string summary;
        VideoInputs inputs;

        if (summaryCurrent)
        {
            summary = cached!.Summary;
            inputs = cached.Inputs;
        }
        else
        {
            (summary, inputs) = await AnalyzeAsync(transcript, cancellationToken);
        }

        var embedding = await EmbedAsync(summary, cancellationToken);

        var index = new VideoSemanticIndex
        {
            VideoId = videoId,
            Summary = summary,
            Inputs = inputs,
            PromptVersion = PromptVersion,
            Embedding = embedding,
            TranscriptHash = transcriptHash,
            SummaryModel = _settings.SummaryDeployment,
            EmbeddingModel = _settings.EmbeddingDeployment,
            EmbeddingDimensions = embedding.Length,
            GeneratedAt = DateTimeOffset.UtcNow
        };

        // Write to a temp file first so an interrupted run never leaves a truncated artifact.
        var temporaryPath = path + ".tmp";
        await File.WriteAllTextAsync(
            temporaryPath,
            JsonSerializer.Serialize(index, jsonOptions),
            cancellationToken);
        File.Move(temporaryPath, path, overwrite: true);
        return true;
    }

    private static async Task<VideoSemanticIndex?> TryReadAsync(
        string videoId,
        string path,
        JsonSerializerOptions jsonOptions,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var index = JsonSerializer.Deserialize<VideoSemanticIndex>(
                await File.ReadAllTextAsync(path, cancellationToken),
                jsonOptions);

            return index is not null
                && index.VideoId == videoId
                && !string.IsNullOrWhiteSpace(index.Summary)
                    ? index
                    : null;
        }
        catch (JsonException exception)
        {
            Console.Error.WriteLine($"Semantic index for {videoId} is invalid and will be regenerated: {exception.Message}");
            return null;
        }
    }

    private async Task<(string Summary, VideoInputs Inputs)> AnalyzeAsync(
        VideoTranscript transcript,
        CancellationToken cancellationToken)
    {
        var text = string.Join(" ", transcript.Segments.Select(segment => segment.Text));

        if (text.Length > _settings.MaxTranscriptCharacters)
        {
            text = text[.._settings.MaxTranscriptCharacters];
        }

        var options = new ChatCompletionOptions
        {
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                "video_analysis",
                ResultSchema,
                jsonSchemaIsStrict: true)
        };

        // The OpenAI client already retries transient failures (429, 5xx).
        var response = await _chatClient.CompleteChatAsync(
            [
                new SystemChatMessage(SummaryInstructions),
                new UserChatMessage(text)
            ],
            options,
            cancellationToken);

        var json = string.Concat(response.Value.Content.Select(part => part.Text)).Trim();

        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidOperationException("The summary model returned an empty response.");
        }

        var result = JsonSerializer.Deserialize<SummaryResult>(json, ResultJsonOptions)
            ?? throw new InvalidOperationException("The summary model returned an unreadable response.");

        return string.IsNullOrWhiteSpace(result.Summary)
            ? throw new InvalidOperationException("The summary model returned an empty summary.")
            : (result.Summary.Trim(), Normalize(result));
    }

    private async Task<float[]> EmbedAsync(string summary, CancellationToken cancellationToken)
    {
        var response = await _embeddingClient.GenerateEmbeddingAsync(
            summary,
            new EmbeddingGenerationOptions { Dimensions = _settings.EmbeddingDimensions },
            cancellationToken);

        var embedding = response.Value.ToFloats().ToArray();

        return embedding.Length == _settings.EmbeddingDimensions
            ? embedding
            : throw new InvalidOperationException(
                $"Expected {_settings.EmbeddingDimensions} embedding dimensions but received {embedding.Length}.");
    }
}
