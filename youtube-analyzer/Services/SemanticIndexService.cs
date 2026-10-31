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

    private const string SummaryInstructions =
        "Eres un asistente que resume vídeos de bricolaje y reformas del hogar. "
        + "Resume el transcript que recibirás en español, en un único párrafo de 80 a 150 palabras, "
        + "indicando el objetivo, los materiales y herramientas principales y los pasos clave. "
        + "Usa únicamente la información del transcript y no inventes datos.";

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
            && cached.SummaryModel == _settings.SummaryDeployment;
        var embeddingCurrent = summaryCurrent
            && cached!.EmbeddingModel == _settings.EmbeddingDeployment
            && cached.EmbeddingDimensions == _settings.EmbeddingDimensions
            && cached.Embedding.Length == _settings.EmbeddingDimensions;

        if (embeddingCurrent)
        {
            return false;
        }

        var summary = summaryCurrent
            ? cached!.Summary
            : await SummarizeAsync(transcript, cancellationToken);
        var embedding = await EmbedAsync(summary, cancellationToken);

        var index = new VideoSemanticIndex
        {
            VideoId = videoId,
            Summary = summary,
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

    private async Task<string> SummarizeAsync(
        VideoTranscript transcript,
        CancellationToken cancellationToken)
    {
        var text = string.Join(" ", transcript.Segments.Select(segment => segment.Text));

        if (text.Length > _settings.MaxTranscriptCharacters)
        {
            text = text[.._settings.MaxTranscriptCharacters];
        }

        // The OpenAI client already retries transient failures (429, 5xx).
        var response = await _chatClient.CompleteChatAsync(
            [
                new SystemChatMessage(SummaryInstructions),
                new UserChatMessage(text)
            ],
            cancellationToken: cancellationToken);

        var summary = string.Concat(response.Value.Content.Select(part => part.Text)).Trim();

        return string.IsNullOrWhiteSpace(summary)
            ? throw new InvalidOperationException("The summary model returned an empty response.")
            : summary;
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
