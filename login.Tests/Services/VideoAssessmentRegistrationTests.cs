using Azure.AI.OpenAI;
using Azure.Core;
using Azure.Identity;
using login.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace login.Tests.Services;

public sealed class VideoAssessmentRegistrationTests
{
    [Fact]
    public void AddVideoAssessmentAllowsStartupWithoutAzureConfigurationWhenDisabled()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddVideoAssessment(
            BuildConfiguration(new Dictionary<string, string?>()),
            CreateEnvironment(isDevelopment: true));

        using var provider = services.BuildServiceProvider();

        Assert.False(provider.GetRequiredService<IOptions<VideoAssessmentOptions>>().Value.Enabled);
        Assert.Null(provider.GetService<IVideoAssessmentAgent>());
        Assert.Null(provider.GetService<AzureOpenAIClient>());
    }

    [Fact]
    public void AddVideoAssessmentDisablesFeatureWithWarningWhenAzureConfigurationIsMissing()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddVideoAssessment(
            BuildConfiguration(new Dictionary<string, string?>
            {
                ["VideoAssessment:Enabled"] = "true"
            }),
            CreateEnvironment(isDevelopment: true));

        using var provider = services.BuildServiceProvider();

        Assert.False(provider.GetRequiredService<IOptions<VideoAssessmentOptions>>().Value.Enabled);
        Assert.Null(provider.GetService<IVideoAssessmentAgent>());
        Assert.Null(provider.GetService<AzureOpenAIClient>());
    }

    [Theory]
    [InlineData("VideoAssessment:MinimumRelevanceScore", "101", "MinimumRelevanceScore")]
    [InlineData("VideoAssessment:MaxCandidates", "0", "MaxCandidates")]
    [InlineData("VideoAssessment:MaxContextCharacters", "0", "context and transcript limits")]
    public void AddVideoAssessmentRejectsInvalidEnabledConfiguration(
        string setting,
        string value,
        string expectedError)
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["VideoAssessment:Enabled"] = "true",
            ["VideoAssessment:Endpoint"] = "https://example.openai.azure.com/",
            ["VideoAssessment:Deployment"] = "test-deployment",
            [setting] = value
        });
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddVideoAssessment(configuration, CreateEnvironment(isDevelopment: true));

        using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<VideoAssessmentOptions>>().Value);
        Assert.Contains(expectedError, exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddVideoAssessmentRegistersAzureCliCredentialForDevelopment()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddVideoAssessment(
            BuildConfiguration(ValidEnabledSettings()),
            CreateEnvironment(isDevelopment: true));

        using var provider = services.BuildServiceProvider();

        Assert.IsType<AzureCliCredential>(provider.GetRequiredService<TokenCredential>());
        Assert.NotNull(provider.GetRequiredService<AzureOpenAIClient>());
        Assert.NotNull(provider.GetRequiredService<IVideoAssessmentAgent>());
    }

    [Fact]
    public void AddVideoAssessmentUsesDefaultCredentialOutsideDevelopment()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddVideoAssessment(
            BuildConfiguration(ValidEnabledSettings()),
            CreateEnvironment(isDevelopment: false));

        using var provider = services.BuildServiceProvider();

        Assert.IsType<DefaultAzureCredential>(provider.GetRequiredService<TokenCredential>());
    }

    [Fact]
    public void BuildUserPromptBoundsTranscriptAndOverallContextWithoutNetworkAccess()
    {
        var request = new VideoAssessmentRequest(
            "Renovar una cocina",
            "Cocinas",
            [
                new VideoAssessmentCandidate(
                    "video-1",
                    "Renovar armarios",
                    "Cocinas",
                    "armarios",
                    ["madera"],
                    ["taladro"],
                    new string('x', 10_000))
            ]);
        var options = new VideoAssessmentOptions
        {
            Enabled = true,
            Deployment = "test-deployment",
            MaxContextCharacters = 5_000,
            MaxTranscriptCharacters = 2_000
        };

        var prompt = VideoAssessmentPromptBuilder.BuildUserPrompt(request, options);
        var promptRequest = System.Text.Json.JsonSerializer.Deserialize<VideoAssessmentRequest>(
            prompt,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));

        Assert.True(prompt.Length <= options.MaxContextCharacters);
        Assert.Equal(options.MaxTranscriptCharacters, Assert.Single(promptRequest!.Candidates).TranscriptExcerpt.Length);

        var endpoint = new Uri("https://example.openai.azure.com/");
        var client = new AzureOpenAIClient(endpoint, new NoOpTokenCredential());
        var agent = new VideoAssessmentAgent(client, Options.Create(options));
        Assert.NotNull(agent);
    }

    private static IConfiguration BuildConfiguration(IDictionary<string, string?> settings) =>
        new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

    private static Dictionary<string, string?> ValidEnabledSettings() => new()
    {
        ["VideoAssessment:Enabled"] = "true",
        ["VideoAssessment:Endpoint"] = "https://example.openai.azure.com/",
        ["VideoAssessment:Deployment"] = "test-deployment"
    };

    private static IHostEnvironment CreateEnvironment(bool isDevelopment) =>
        new TestHostEnvironment
        {
            EnvironmentName = isDevelopment ? Environments.Development : Environments.Production
        };

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;

        public string ApplicationName { get; set; } = nameof(VideoAssessmentRegistrationTests);

        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class NoOpTokenCredential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            new("unused-token", DateTimeOffset.UtcNow.AddHours(1));

        public override ValueTask<AccessToken> GetTokenAsync(
            TokenRequestContext requestContext,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(new AccessToken("unused-token", DateTimeOffset.UtcNow.AddHours(1)));
    }
}
