using System.Text.Json;
using login.Services;
using Xunit;

namespace login.Tests.Services;

public sealed class VideoAssessmentResponseValidatorTests
{
    private static readonly VideoAssessmentCandidate[] Candidates =
    [
        new(
            "kitchen-cabinets",
            "Renovar muebles y armarios de cocina",
            "Cocinas",
            "muebles|armarios|almacenamiento",
            ["pintura", "tiradores"],
            ["destornillador"],
            "Cómo renovar los frentes de los armarios y aprovechar el almacenamiento."),
        new(
            "kitchen-lighting",
            "Iluminación cálida para cocinas",
            "Cocinas",
            "iluminación|luz cálida",
            ["lámparas"],
            ["taladro"],
            "Ideas de iluminación ambiental para una cocina."),
        new(
            "patio-floor",
            "Colocar pavimento en un patio",
            "Exteriores",
            "patio|pavimento",
            ["baldosas"],
            ["llana"],
            "Instalación de baldosas cerámicas en exteriores.")
    ];

    [Fact]
    public void ParseAndValidateAcceptsValidStructuredOutputAndPreservesMatchData()
    {
        const string json =
            """{"matches":[{"videoId":"kitchen-cabinets","score":91,"explanation":"Explica cómo renovar armarios de cocina y mejorar el almacenamiento."}]}""";

        var response = VideoAssessmentResponseValidator.ParseAndValidate(json, Candidates);

        var match = Assert.Single(response.Matches);
        Assert.Equal("kitchen-cabinets", match.VideoId);
        Assert.Equal(91, match.Score);
        Assert.Equal(
            "Explica cómo renovar armarios de cocina y mejorar el almacenamiento.",
            match.Explanation);
    }

    [Theory]
    [InlineData("{")]
    [InlineData("  ")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("""{"matches":[{"score":80,"explanation":"Explicación."}]}""")]
    [InlineData("""{"matches":[{"videoId":"kitchen-cabinets","explanation":"Explicación."}]}""")]
    [InlineData("""{"matches":[{"videoId":"kitchen-cabinets","score":80}]}""")]
    [InlineData("""{"matches":[null]}""")]
    public void ParseAndValidateRejectsMalformedOrIncompleteOutput(string json)
    {
        Assert.Throws<JsonException>(() =>
            VideoAssessmentResponseValidator.ParseAndValidate(json, Candidates));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void ParseAndValidateRejectsScoresOutsideTheAllowedRange(int score)
    {
        var json = $$"""{"matches":[{"videoId":"kitchen-cabinets","score":{{score}},"explanation":"Relevante."}]}""";

        Assert.Throws<JsonException>(() =>
            VideoAssessmentResponseValidator.ParseAndValidate(json, Candidates));
    }

    [Fact]
    public void ParseAndValidateRejectsDuplicateVideoIds()
    {
        const string json =
            """{"matches":[{"videoId":"kitchen-cabinets","score":91,"explanation":"Primera."},{"videoId":"kitchen-cabinets","score":80,"explanation":"Segunda."}]}""";

        Assert.Throws<JsonException>(() =>
            VideoAssessmentResponseValidator.ParseAndValidate(json, Candidates));
    }

    [Fact]
    public void ParseAndValidateRejectsIdsOutsideTheCandidateList()
    {
        const string json =
            """{"matches":[{"videoId":"unknown-video","score":91,"explanation":"Relevante."}]}""";

        Assert.Throws<JsonException>(() =>
            VideoAssessmentResponseValidator.ParseAndValidate(json, Candidates));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ParseAndValidateRejectsEmptyExplanations(string explanation)
    {
        var json = JsonSerializer.Serialize(new
        {
            matches = new[] { new { videoId = "kitchen-cabinets", score = 91, explanation } }
        });

        Assert.Throws<JsonException>(() =>
            VideoAssessmentResponseValidator.ParseAndValidate(json, Candidates));
    }

    [Fact]
    public void ParseAndValidateAcceptsAnExplicitNoMatchResponse()
    {
        var response = VideoAssessmentResponseValidator.ParseAndValidate(
            """{"matches":[]}""",
            Candidates);

        Assert.Empty(response.Matches);
    }

    [Fact]
    public void CuratedExamplesSeparateStrongAmbiguousAndUnrelatedVideos()
    {
        const string projectDescription = "Quiero renovar los muebles de mi cocina y mejorar el almacenamiento.";
        const string projectCategory = "Cocinas";
        var examples = new[]
        {
            new { ProjectDescription = projectDescription, ProjectCategory = projectCategory, VideoId = "kitchen-cabinets", Score = 91, ExpectedRelevant = true },
            new { ProjectDescription = projectDescription, ProjectCategory = projectCategory, VideoId = "kitchen-lighting", Score = 58, ExpectedRelevant = false },
            new { ProjectDescription = projectDescription, ProjectCategory = projectCategory, VideoId = "patio-floor", Score = 12, ExpectedRelevant = false }
        };

        Assert.Equal(
            examples.Select(example => example.ExpectedRelevant),
            examples.Select(example => VideoAssessmentPolicy.MeetsMinimumScore(example.Score)));
    }

    [Fact]
    public void MinimumScoreBoundaryIncludesSeventyAndExcludesTheScoreImmediatelyBelow()
    {
        Assert.False(VideoAssessmentPolicy.MeetsMinimumScore(69));
        Assert.True(VideoAssessmentPolicy.MeetsMinimumScore(70));
    }
}
