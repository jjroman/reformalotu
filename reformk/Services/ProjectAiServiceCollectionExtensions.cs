using Azure.AI.OpenAI;
using System.ClientModel;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Options;

namespace Reformk.Services;

public static class ProjectAiServiceCollectionExtensions
{
    public static IServiceCollection AddProjectAi(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        services.AddOptions<ProjectAiOptions>()
            .Bind(configuration.GetSection(ProjectAiOptions.SectionName))
            .Validate(
                options => !options.Enabled ||
                    Uri.TryCreate(options.Endpoint, UriKind.Absolute, out var endpoint) &&
                    endpoint.Scheme == Uri.UriSchemeHttps &&
                    !string.IsNullOrWhiteSpace(options.Deployment),
                "ProjectAi:Endpoint must be HTTPS and ProjectAi:Deployment is required when project AI is enabled.")
            .ValidateOnStart();

        var settings = configuration.GetSection(ProjectAiOptions.SectionName);
        if (settings.GetValue<bool>(nameof(ProjectAiOptions.Enabled)))
        {
            services.AddSingleton<TokenCredential>(_ =>
                environment.IsDevelopment() ? new AzureCliCredential() : new DefaultAzureCredential());
            services.AddSingleton(provider =>
            {
                var options = provider.GetRequiredService<IOptions<ProjectAiOptions>>().Value;
                var endpoint = new Uri(options.Endpoint!);
                var apiKey = configuration[$"{ProjectAiOptions.SectionName}:ApiKey"];
                return string.IsNullOrWhiteSpace(apiKey)
                    ? new AzureOpenAIClient(endpoint, provider.GetRequiredService<TokenCredential>())
                    : new AzureOpenAIClient(endpoint, new ApiKeyCredential(apiKey));
            });
            services.AddScoped<IProjectWorkDescriptionGenerator, ProjectWorkDescriptionGenerator>();
        }

        services.AddScoped<ProjectVideoService>();
        return services;
    }
}
