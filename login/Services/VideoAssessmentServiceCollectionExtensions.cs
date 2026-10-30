using Azure.AI.OpenAI;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Options;

namespace login.Services;

public static class VideoAssessmentServiceCollectionExtensions
{
    public static IServiceCollection AddVideoAssessment(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        var section = configuration.GetSection(VideoAssessmentOptions.SectionName);
        var assessmentEnabled = section.GetValue<bool>(nameof(VideoAssessmentOptions.Enabled));
        var hasAzureConfiguration =
            IsValidEndpoint(section[nameof(VideoAssessmentOptions.Endpoint)]) &&
            !string.IsNullOrWhiteSpace(section[nameof(VideoAssessmentOptions.Deployment)]);

        services.AddOptions<VideoAssessmentOptions>()
            .Bind(section)
            .PostConfigure<ILogger<VideoAssessmentOptions>>((options, logger) =>
            {
                if (options.Enabled && !hasAzureConfiguration)
                {
                    options.Enabled = false;
                    logger.LogWarning(
                        "Video assessment is enabled but its Azure OpenAI endpoint or deployment is missing or invalid. Video assessment has been disabled.");
                }
            })
            .Validate(
                options => !options.Enabled || IsValidEndpoint(options.Endpoint),
                "VideoAssessment:Endpoint must be a valid HTTPS Azure OpenAI endpoint when assessment is enabled.")
            .Validate(
                options => !options.Enabled || !string.IsNullOrWhiteSpace(options.Deployment),
                "VideoAssessment:Deployment is required when assessment is enabled.")
            .Validate(
                options => !options.Enabled ||
                    options.MinimumRelevanceScore is >= 0 and <= 100,
                "VideoAssessment:MinimumRelevanceScore must be between 0 and 100.")
            .Validate(
                options => !options.Enabled || options.MaxCandidates > 0,
                "VideoAssessment:MaxCandidates must be greater than zero.")
            .Validate(
                options => !options.Enabled ||
                    options.MaxContextCharacters > 0 &&
                    options.MaxTranscriptCharacters >= 0,
                "VideoAssessment context and transcript limits must be positive (transcript may be zero).")
            .ValidateOnStart();

        if (assessmentEnabled && hasAzureConfiguration)
        {
            services.AddSingleton<TokenCredential>(_ =>
                environment.IsDevelopment()
                    ? new AzureCliCredential()
                    : new DefaultAzureCredential());
            services.AddSingleton(provider =>
            {
                var options = provider.GetRequiredService<IOptions<VideoAssessmentOptions>>().Value;
                return new AzureOpenAIClient(
                    new Uri(options.Endpoint!),
                    provider.GetRequiredService<TokenCredential>());
            });
            services.AddSingleton<IVideoAssessmentAgent, VideoAssessmentAgent>();
        }

        return services;
    }

    private static bool IsValidEndpoint(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        Uri.TryCreate(value, UriKind.Absolute, out var endpoint) &&
        endpoint.Scheme == Uri.UriSchemeHttps &&
        string.IsNullOrEmpty(endpoint.UserInfo) &&
        string.IsNullOrEmpty(endpoint.Query) &&
        string.IsNullOrEmpty(endpoint.Fragment);
}
