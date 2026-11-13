using Reformk.Services;
using Xunit;

namespace Reformk.Tests.Services;

public sealed class EmailConfirmationTemplateRendererTests
{
    [Fact]
    public void RenderSubstitutesEveryConfirmationEmailPlaceholder()
    {
        const string template = "Hi {{name}}; {{app_name}}; {{confirmation_url}}; {{expiration_period}}.";
        const string confirmationLink = "https://example.test/confirm?code=abc%2Fdef&user=123";

        var rendered = EmailConfirmationTemplateRenderer.Render(
            template,
            "Alex",
            "Reformk",
            confirmationLink,
            "24 hours");

        Assert.Equal(
            $"Hi Alex; Reformk; {confirmationLink}; 24 hours.",
            rendered);
    }

    [Fact]
    public void RenderRejectsUnresolvedTemplatePlaceholders()
    {
        Assert.Throws<InvalidDataException>(() =>
            EmailConfirmationTemplateRenderer.Render(
                "Hi {{name}}; {{unknown}}",
                "Alex",
                "Reformk",
                "https://example.test/confirm",
                "24 hours"));
    }
}
