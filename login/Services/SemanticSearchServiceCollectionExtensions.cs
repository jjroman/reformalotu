using System.ClientModel;
using Azure.AI.OpenAI;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Options;

namespace login.Services;

public static class SemanticSearchServiceCollectionExtensions
{
    /// <summary>
    /// Registers semantic search. The query embedding client is only available when a valid
    /// HTTPS endpoint is configured (SemanticSearch:Endpoint or VideoAssessment:Endpoint).
    /// </summary>
    public static IServiceCollection AddSemanticSearch(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        services.AddOptions<SemanticSearchOptions>()
            .Bind(configuration.GetSection(SemanticSearchOptions.SectionName))
            .Validate(
                options => options.MinimumSimilarity is >= -1 and <= 1 && options.MaxResults > 0,
                "SemanticSearch:MinimumSimilarity must be within [-1, 1] and MaxResults must be positive.")
            .ValidateOnStart();
        services.AddScoped<SemanticVideoSearchService>();

        var endpointValue = configuration[$"{SemanticSearchOptions.SectionName}:Endpoint"];
        if (string.IsNullOrWhiteSpace(endpointValue))
        {
            endpointValue = configuration[$"{VideoAssessmentOptions.SectionName}:Endpoint"];
        }

        if (Uri.TryCreate(endpointValue, UriKind.Absolute, out var endpoint) &&
            endpoint.Scheme == Uri.UriSchemeHttps &&
            string.IsNullOrEmpty(endpoint.UserInfo))
        {
            // Reuses the VideoAssessment key (same Azure OpenAI resource); never committed.
            var apiKey = configuration[$"{SemanticSearchOptions.SectionName}:ApiKey"]
                ?? configuration[$"{VideoAssessmentOptions.SectionName}:ApiKey"];
            services.AddSingleton<IQueryEmbeddingClient>(provider =>
            {
                AzureOpenAIClient client;
                if (!string.IsNullOrWhiteSpace(apiKey))
                {
                    client = new AzureOpenAIClient(endpoint, new ApiKeyCredential(apiKey));
                }
                else
                {
                    TokenCredential credential = environment.IsDevelopment()
                        ? new AzureCliCredential()
                        : new DefaultAzureCredential();
                    client = new AzureOpenAIClient(endpoint, credential);
                }

                return new AzureQueryEmbeddingClient(
                    client,
                    provider.GetRequiredService<IOptions<SemanticSearchOptions>>(),
                    provider.GetRequiredService<IOptions<SqliteVectorOptions>>());
            });
        }

        return services;
    }
}
