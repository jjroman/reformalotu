using Azure.AI.OpenAI;
using Microsoft.Extensions.Options;
using OpenAI.Embeddings;

namespace login.Services;

/// <summary>Generates the embedding of a search query (external dependency, replaceable in tests).</summary>
public interface IQueryEmbeddingClient
{
    Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default);
}

internal sealed class AzureQueryEmbeddingClient(
    AzureOpenAIClient client,
    IOptions<SemanticSearchOptions> searchOptions,
    IOptions<SqliteVectorOptions> vectorOptions) : IQueryEmbeddingClient
{
    public async Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        var dimensions = vectorOptions.Value.Dimensions;
        var embeddingClient = client.GetEmbeddingClient(searchOptions.Value.EmbeddingDeployment);
        var response = await embeddingClient.GenerateEmbeddingAsync(
            text,
            new EmbeddingGenerationOptions { Dimensions = dimensions },
            cancellationToken);
        var embedding = response.Value.ToFloats().ToArray();

        return embedding.Length == dimensions
            ? embedding
            : throw new InvalidOperationException(
                $"Expected {dimensions} embedding dimensions but received {embedding.Length}.");
    }
}
