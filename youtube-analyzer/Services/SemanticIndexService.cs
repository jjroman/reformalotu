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

    /// <summary>Network timeout per attempt; structured output over long transcripts can exceed the 100 s default.</summary>
    public int RequestTimeoutSeconds { get; init; } = 300;

    public bool IsConfigured =>
        Uri.TryCreate(Endpoint, UriKind.Absolute, out var uri)
        && RequestTimeoutSeconds > 0
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

    private const int PromptVersion = 3;

    private const string SummaryInstructions =
        "Analizas transcripciones de vídeos de bricolaje y reformas para preparar análisis de insumos unitarios. "
        + "Devuelve únicamente el JSON definido por el esquema. Los textos libres van en español y las claves en inglés camelCase. "
        + "summary debe ser un solo párrafo de 80 a 150 palabras, fiel al transcript y sin inferencias; es el único texto que se usará para crear el embedding. "
        + "Incluye inputs.materials, inputs.equipment e inputs.labor, aunque alguna lista esté vacía. "
        + "Describe materiales y equipos mencionados o detectados en el transcript; no inventes marcas. No añadas herramientas, equipos ni protección que no aparezcan o se detecten; si no hay, devuelve equipment: []. La marca solo se rellena si consta en el transcript. "
        + "Puedes inferir cantidades, características y mano de obra razonables, pero anota la incertidumbre en notes y confidence. "
        + "Asigna ids únicos dentro del vídeo en orden: materiales m1, m2..., equipos e1, e2..., mano de obra l1, l2.... "
        + "Si hay una tarea ejecutable, infiere siempre al menos un lead con el oficio principal más razonable aunque el transcript no hable de trabajadores; si no puedes precisar el oficio, describe la función concreta y añade '(manitas)' al final, nunca devuelvas solo 'manitas'. Añade helpers solo si están justificados. Si no hay tareas ni mano de obra inferible, devuelve labor: []. "
        + "Los confidence son enteros: 1 = completamente inferido, 5 = deducido con apoyo parcial del transcript, 10 = textual. "
        + "quantity, estimatedHours y estimatedMinutes deben ser mayores que cero o null. Si quantity tiene valor, unit también debe tenerlo. "
        + "Las tareas deben estar en orden de ejecución, con order consecutivo desde 1, title en infinitivo y description de una a tres frases. "
        + "Cada tarea referencia únicamente los ids de insumos que utiliza; usa listas vacías cuando no aplique. "
        + "Extrae como tareas todas las acciones prácticas que el vídeo muestra, explica o recomienda, no solo las de obra: también decorar, montar, plantar, cavar, abonar, limpiar o cuidar. Si el vídeo es un consejo o una guía, convierte los pasos en tareas ordenadas. "
        + "Deja tasks vacío únicamente si el vídeo es puramente informativo y no contiene ninguna acción que alguien pueda realizar. Cuando un dato no conste ni pueda estimarse, usa null; nunca omitas campos ni inventes marcas.";

    private static readonly JsonSerializerOptions ResultJsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly BinaryData ResultSchema = BinaryData.FromString(BuildResultSchema());

    private sealed record SummaryResult
    {
        public string Summary { get; init; } = "";
        public VideoInputs Inputs { get; init; } = new();
        public IReadOnlyList<VideoTask> Tasks { get; init; } = [];
    }

    private static string BuildResultSchema()
    {
        static object Nullable(string type) => new { type = new[] { type, "null" } };
        static object PositiveNumber() => new { type = new[] { "number", "null" }, minimum = 0 };
        static object PositiveInteger() => new { type = new[] { "integer", "null" }, minimum = 1 };
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
            ["id"] = new { type = "string" },
            ["name"] = new { type = "string" },
            ["brand"] = Nullable("string"),
            ["specification"] = Nullable("string"),
            ["quantity"] = PositiveNumber(),
            ["unit"] = Nullable("string"),
            ["notes"] = Nullable("string"),
            ["confidence"] = confidence
        });

        var equipment = Obj(new()
        {
            ["id"] = new { type = "string" },
            ["name"] = new { type = "string" },
            ["category"] = new { type = "string", @enum = new[] { "manual_tool", "power_tool", "machinery", "safety", "other" } },
            ["brand"] = Nullable("string"),
            ["specification"] = Nullable("string"),
            ["quantity"] = PositiveNumber(),
            ["notes"] = Nullable("string"),
            ["confidence"] = confidence
        });

        var labor = Obj(new()
        {
            ["id"] = new { type = "string" },
            ["role"] = new { type = "string" },
            ["level"] = new { type = "string", @enum = new[] { "lead", "helper" } },
            ["headcount"] = PositiveInteger(),
            ["estimatedHours"] = PositiveNumber(),
            ["notes"] = Nullable("string"),
            ["confidence"] = confidence
        });

        var task = Obj(new()
        {
            ["order"] = new { type = "integer" },
            ["title"] = new { type = "string" },
            ["description"] = new { type = "string" },
            ["estimatedMinutes"] = PositiveNumber(),
            ["materialIds"] = Arr(new { type = "string" }),
            ["equipmentIds"] = Arr(new { type = "string" }),
            ["laborIds"] = Arr(new { type = "string" }),
            ["confidence"] = confidence
        });

        var inputs = Obj(new()
        {
            ["materials"] = Arr(material),
            ["equipment"] = Arr(equipment),
            ["labor"] = Arr(labor)
        });

        return JsonSerializer.Serialize(Obj(new()
        {
            ["summary"] = new { type = "string" },
            ["inputs"] = inputs,
            ["tasks"] = Arr(task)
        }));
    }

    private static int ClampConfidence(int value) => Math.Clamp(value, 1, 10);

    private static (VideoInputs Inputs, IReadOnlyList<VideoTask> Tasks) Normalize(SummaryResult result)
    {
        ArgumentNullException.ThrowIfNull(result.Inputs);
        ArgumentNullException.ThrowIfNull(result.Inputs.Materials);
        ArgumentNullException.ThrowIfNull(result.Inputs.Equipment);
        ArgumentNullException.ThrowIfNull(result.Inputs.Labor);
        ArgumentNullException.ThrowIfNull(result.Tasks);
        if (result.Inputs.Materials.Any(item => item is null)
            || result.Inputs.Equipment.Any(item => item is null)
            || result.Inputs.Labor.Any(item => item is null)
            || result.Tasks.Any(task => task is null))
        {
            throw new InvalidOperationException("The summary model returned a null item in its structured analysis.");
        }

        var materialIds = new Dictionary<string, string>(StringComparer.Ordinal);
        var materials = result.Inputs.Materials
            .Select((item, index) =>
            {
                var id = $"m{index + 1}";
                if (!string.IsNullOrWhiteSpace(item.Id))
                {
                    materialIds.TryAdd(item.Id, id);
                }

                var quantity = PositiveOrNull(item.Quantity);

                return item with
                {
                    Id = id,
                    Quantity = quantity is not null && !string.IsNullOrWhiteSpace(item.Unit) ? quantity : null,
                    Unit = string.IsNullOrWhiteSpace(item.Unit) ? null : item.Unit.Trim(),
                    Confidence = ClampConfidence(item.Confidence)
                };
            })
            .ToArray();

        var equipmentIds = new Dictionary<string, string>(StringComparer.Ordinal);
        var equipment = result.Inputs.Equipment
            .Select((item, index) =>
            {
                var id = $"e{index + 1}";
                if (!string.IsNullOrWhiteSpace(item.Id))
                {
                    equipmentIds.TryAdd(item.Id, id);
                }

                return item with
                {
                    Id = id,
                    Quantity = PositiveOrNull(item.Quantity),
                    Confidence = ClampConfidence(item.Confidence)
                };
            })
            .ToArray();

        var fallbackRole = InferLaborRole(result.Tasks);
        var laborIds = new Dictionary<string, string>(StringComparer.Ordinal);
        var hasLead = false;
        var labor = result.Inputs.Labor
            .Select((item, index) =>
            {
                var id = $"l{index + 1}";
                if (!string.IsNullOrWhiteSpace(item.Id))
                {
                    laborIds.TryAdd(item.Id, id);
                }

                var level = !hasLead && item.Level == "lead" ? "lead" : "helper";
                hasLead |= level == "lead";

                return item with
                {
                    Id = id,
                    Role = IsGenericHandymanRole(item.Role) ? fallbackRole : item.Role,
                    Level = level,
                    Headcount = item.Headcount is >= 1 ? item.Headcount : null,
                    EstimatedHours = PositiveOrNull(item.EstimatedHours),
                    Confidence = ClampConfidence(item.Confidence)
                };
            })
            .ToArray();

        if (labor.Length > 0 && !hasLead)
        {
            labor[0] = labor[0] with { Level = "lead" };
        }

        var inferredLabor = labor.Length == 0 && result.Tasks.Count > 0;
        if (inferredLabor)
        {
            double? estimatedHours = result.Tasks.All(task => task.EstimatedMinutes is > 0)
                ? Math.Round(result.Tasks.Sum(task => task.EstimatedMinutes!.Value) / 60, 2)
                : null;
            labor = [
                new LaborInput
                {
                    Id = "l1",
                    Role = fallbackRole,
                    Level = "lead",
                    Headcount = 1,
                    EstimatedHours = estimatedHours is > 0 ? estimatedHours : null,
                    Notes = "Rol y dedicación inferidos a partir de las tareas ejecutables.",
                    Confidence = 1
                }
            ];
        }

        var tasks = result.Tasks
            .Select((task, index) => task with
            {
                Order = index + 1,
                EstimatedMinutes = PositiveOrNull(task.EstimatedMinutes),
                MaterialIds = NormalizeReferences(task.MaterialIds, materialIds),
                EquipmentIds = NormalizeReferences(task.EquipmentIds, equipmentIds),
                LaborIds = inferredLabor
                    ? ["l1"]
                    : NormalizeReferences(task.LaborIds, laborIds),
                Confidence = ClampConfidence(task.Confidence)
            })
            .ToArray();

        return (new VideoInputs { Materials = materials, Equipment = equipment, Labor = labor }, tasks);
    }

    private static bool IsGenericHandymanRole(string role) =>
        string.Equals(role.Trim(), "manitas", StringComparison.OrdinalIgnoreCase);

    private static string InferLaborRole(IReadOnlyList<VideoTask> tasks)
    {
        var taskText = string.Join(
            " ",
            tasks.Select(task => $"{task.Title} {task.Description}"));

        return taskText.Contains("persiana", StringComparison.OrdinalIgnoreCase)
            ? "Instalador de persianas (manitas)"
            : "Operario de instalación doméstica (manitas)";
    }

    private static double? PositiveOrNull(double? value) => value is > 0 ? value : null;

    private static IReadOnlyList<string> NormalizeReferences(
        IReadOnlyList<string> references,
        IReadOnlyDictionary<string, string> canonicalIds)
    {
        ArgumentNullException.ThrowIfNull(references);
        return references
            .Where(reference => !string.IsNullOrWhiteSpace(reference) && canonicalIds.ContainsKey(reference))
            .Select(reference => canonicalIds[reference])
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private readonly SemanticIndexSettings _settings;
    private readonly ChatClient _chatClient;
    private readonly EmbeddingClient _embeddingClient;

    public SemanticIndexService(SemanticIndexSettings settings, string? apiKey)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _settings = settings;
        var endpoint = new Uri(settings.Endpoint);
        // Falls back to Entra ID (az login) when no API key is provided.
        var clientOptions = new AzureOpenAIClientOptions
        {
            NetworkTimeout = TimeSpan.FromSeconds(settings.RequestTimeoutSeconds)
        };
        var client = string.IsNullOrWhiteSpace(apiKey)
            ? new AzureOpenAIClient(endpoint, new DefaultAzureCredential(), clientOptions)
            : new AzureOpenAIClient(endpoint, new ApiKeyCredential(apiKey), clientOptions);
        _chatClient = client.GetChatClient(settings.SummaryDeployment);
        _embeddingClient = client.GetEmbeddingClient(settings.EmbeddingDeployment);
    }

    /// <summary>Hash of the transcript text; must match the one computed by the ReformApp project.</summary>
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
        Action<string> writeDiagnostic,
        CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(videoOutputDirectory, FileName);
        var transcriptHash = ComputeTranscriptHash(transcript);
        // The ReformApp project reads this file with PascalCase keys, so the written format must not change.
        var readOptions = new JsonSerializerOptions(jsonOptions) { PropertyNameCaseInsensitive = true };
        var cached = await TryReadAsync(videoId, path, readOptions, writeDiagnostic, cancellationToken);

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
        IReadOnlyList<VideoTask> tasks;

        if (summaryCurrent)
        {
            summary = cached!.Summary;
            inputs = cached.Inputs;
            tasks = cached.Tasks;
        }
        else
        {
            (summary, inputs, tasks) = await AnalyzeAsync(transcript, cancellationToken);
        }

        var embedding = await EmbedAsync(summary, cancellationToken);

        var index = new VideoSemanticIndex
        {
            VideoId = videoId,
            Summary = summary,
            Inputs = inputs,
            PromptVersion = PromptVersion,
            Embedding = embedding,
            Tasks = tasks,
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
        Action<string> writeDiagnostic,
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
                && IsNormalized(index)
                    ? index
                    : InvalidCachedIndex(videoId, writeDiagnostic);
        }
        catch (JsonException exception)
        {
            writeDiagnostic($"Semantic index for {videoId} is invalid and will be regenerated: {exception.Message}");
            return null;
        }
    }

    private static VideoSemanticIndex? InvalidCachedIndex(string videoId, Action<string> writeDiagnostic)
    {
        writeDiagnostic(
            $"Semantic index for {videoId} does not satisfy the current data contract and will be regenerated.");
        return null;
    }

    private static bool IsNormalized(VideoSemanticIndex index)
    {
        if (index.Inputs is null
            || index.Inputs.Materials is null
            || index.Inputs.Equipment is null
            || index.Inputs.Labor is null
            || index.Tasks is null
            || index.Embedding is null
            || index.Inputs.Materials.Any(item => item is null)
            || index.Inputs.Equipment.Any(item => item is null)
            || index.Inputs.Labor.Any(item => item is null)
            || index.Tasks.Any(task => task is null))
        {
            return false;
        }

        var materialIds = index.Inputs.Materials.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        var equipmentIds = index.Inputs.Equipment.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        var laborIds = index.Inputs.Labor.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);

        return index.Inputs.Materials
            .Select((item, i) => item.Id == $"m{i + 1}"
                && item.Confidence is >= 1 and <= 10
                && (item.Quantity is null || item.Quantity > 0 && !string.IsNullOrWhiteSpace(item.Unit)))
            .All(valid => valid)
            && index.Inputs.Equipment
                .Select((item, i) => item.Id == $"e{i + 1}"
                    && item.Confidence is >= 1 and <= 10
                    && (item.Quantity is null || item.Quantity > 0))
                .All(valid => valid)
            && index.Inputs.Labor
                .Select((item, i) => item.Id == $"l{i + 1}"
                    && item.Confidence is >= 1 and <= 10
                    && (item.Level is "lead" or "helper")
                    && (item.Headcount is null || item.Headcount >= 1)
                    && (item.EstimatedHours is null || item.EstimatedHours > 0))
                .All(valid => valid)
            && (index.Inputs.Labor.Count == 0 || index.Inputs.Labor.Count(item => item.Level == "lead") == 1)
            && index.Tasks
                .Select((task, i) => task.Order == i + 1
                    && task.Confidence is >= 1 and <= 10
                    && (task.EstimatedMinutes is null || task.EstimatedMinutes > 0)
                    && task.MaterialIds is not null
                    && task.EquipmentIds is not null
                    && task.LaborIds is not null
                    && task.MaterialIds.Distinct(StringComparer.Ordinal).Count() == task.MaterialIds.Count
                    && task.EquipmentIds.Distinct(StringComparer.Ordinal).Count() == task.EquipmentIds.Count
                    && task.LaborIds.Distinct(StringComparer.Ordinal).Count() == task.LaborIds.Count
                    && task.MaterialIds.All(id => id is not null && materialIds.Contains(id))
                    && task.EquipmentIds.All(id => id is not null && equipmentIds.Contains(id))
                    && task.LaborIds.All(id => id is not null && laborIds.Contains(id)))
            .All(valid => valid);
    }

    private async Task<(string Summary, VideoInputs Inputs, IReadOnlyList<VideoTask> Tasks)> AnalyzeAsync(
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

        if (string.IsNullOrWhiteSpace(result.Summary))
        {
            throw new InvalidOperationException("The summary model returned an empty summary.");
        }

        var normalized = Normalize(result);
        return (result.Summary.Trim(), normalized.Inputs, normalized.Tasks);
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
