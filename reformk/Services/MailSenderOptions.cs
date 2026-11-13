using System.ComponentModel.DataAnnotations;

namespace Reformk.Services;

public sealed class MailSenderOptions
{
    public const string SectionName = "MailSender";

    [Required]
    public string SmtpHost { get; set; } = "smtp.mailersend.net";

    [Range(1, 65535)]
    public int SmtpPort { get; set; } = 587;

    [Required]
    public string SmtpUsername { get; set; } = string.Empty;

    [Required]
    public string SmtpPassword { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string FromAddress { get; set; } = string.Empty;

    [Required]
    public string FromName { get; set; } = "Reformk";

    [Required]
    public string AppName { get; set; } = "Reformk";
}
