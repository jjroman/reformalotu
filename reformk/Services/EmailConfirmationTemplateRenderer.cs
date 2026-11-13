namespace Reformk.Services;

public static class EmailConfirmationTemplateRenderer
{
    public static string Render(
        string template,
        string name,
        string appName,
        string confirmationLink,
        string expirationPeriod)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(appName);
        ArgumentNullException.ThrowIfNull(confirmationLink);
        ArgumentNullException.ThrowIfNull(expirationPeriod);

        var rendered = template
            .Replace("{{name}}", name, StringComparison.Ordinal)
            .Replace("{{app_name}}", appName, StringComparison.Ordinal)
            .Replace("{{confirmation_url}}", confirmationLink, StringComparison.Ordinal)
            .Replace("{{expiration_period}}", expirationPeriod, StringComparison.Ordinal);

        if (rendered.Contains("{{", StringComparison.Ordinal))
        {
            throw new InvalidDataException("The email confirmation template contains an unresolved placeholder.");
        }

        return rendered;
    }
}
