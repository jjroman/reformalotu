using MailKit.Net.Smtp;
using MailKit.Security;
using MailKit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using MimeKit;
using IdentityEmailSender = Microsoft.AspNetCore.Identity.IEmailSender<Microsoft.AspNetCore.Identity.IdentityUser>;
using IdentityUiEmailSender = Microsoft.AspNetCore.Identity.UI.Services.IEmailSender;

namespace Reformk.Services;

public sealed class MailerSendIdentityEmailSender(
    IOptions<MailSenderOptions> options,
    IWebHostEnvironment environment) : IdentityEmailSender, IdentityUiEmailSender
{
    public async Task SendConfirmationLinkAsync(IdentityUser user, string email, string confirmationLink)
    {
        var templatePath = Path.Combine(environment.ContentRootPath, "emails", "emailconfirmation.txt");
        var template = await File.ReadAllTextAsync(templatePath);
        var body = EmailConfirmationTemplateRenderer.Render(
            template,
            user.UserName ?? email,
            options.Value.AppName,
            confirmationLink,
            "24 hours");

        await SendTextAsync(email, "Confirm your email address", body);
    }

    public Task SendPasswordResetLinkAsync(IdentityUser user, string email, string resetLink) =>
        SendTextAsync(
            email,
            "Reset your password",
            $"Use this link to reset your {options.Value.AppName} password:\r\n\r\n{resetLink}");

    public Task SendPasswordResetCodeAsync(IdentityUser user, string email, string resetCode) =>
        SendTextAsync(
            email,
            "Reset your password",
            $"Use this code to reset your {options.Value.AppName} password:\r\n\r\n{resetCode}");

    public Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        var message = CreateMessage(email, subject);
        message.Body = new TextPart("html") { Text = htmlMessage };
        return SendMessageAsync(message);
    }

    private Task SendTextAsync(string email, string subject, string text)
    {
        var message = CreateMessage(email, subject);
        message.Body = new TextPart("plain") { Text = text };
        return SendMessageAsync(message);
    }

    private MimeMessage CreateMessage(string email, string subject)
    {
        var settings = options.Value;
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(settings.FromName, settings.FromAddress));
        message.To.Add(MailboxAddress.Parse(email));
        message.Subject = subject;
        return message;
    }

    private async Task SendMessageAsync(MimeMessage message)
    {
        var settings = options.Value;
        using var client = new SmtpClient();
        await client.ConnectAsync(settings.SmtpHost, settings.SmtpPort, SecureSocketOptions.StartTls);
        await client.AuthenticateAsync(settings.SmtpUsername, settings.SmtpPassword);
        await client.SendAsync(FormatOptions.Default, new SendRequest(message), CancellationToken.None);
        await client.DisconnectAsync(true);
    }
}
