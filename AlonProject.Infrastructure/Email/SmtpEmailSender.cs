using System.Net;
using System.Net.Mail;
using AlonProject.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AlonProject.Infrastructure.Email;

/// <summary>
/// SMTP email sender configured via the "Email" configuration section:
///   Email:Host, Email:Port, Email:UseSsl, Email:Username, Email:Password, Email:From.
/// When no host is configured (local development), emails are not sent —
/// their full content is written to the log instead, so flows like email
/// verification remain fully testable without an SMTP account.
/// </summary>
public class SmtpEmailSender : IEmailSender
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IConfiguration configuration, ILogger<SmtpEmailSender> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendAsync(string to, string subject, string htmlBody)
    {
        var host = _configuration["Email:Host"];

        if (string.IsNullOrWhiteSpace(host))
        {
            // Development fallback: log instead of sending
            _logger.LogWarning(
                "EMAIL (log-only mode — no Email:Host configured)\nTo: {To}\nSubject: {Subject}\nBody:\n{Body}",
                to, subject, htmlBody);
            return;
        }

        var port = int.TryParse(_configuration["Email:Port"], out var parsedPort) ? parsedPort : 587;
        var useSsl = !bool.TryParse(_configuration["Email:UseSsl"], out var parsedSsl) || parsedSsl;
        var username = _configuration["Email:Username"];
        var password = _configuration["Email:Password"];
        var from = _configuration["Email:From"] ?? username ?? "no-reply@localhost";

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = useSsl
        };
        if (!string.IsNullOrEmpty(username))
        {
            client.Credentials = new NetworkCredential(username, password);
        }

        using var message = new MailMessage(from, to, subject, htmlBody)
        {
            IsBodyHtml = true
        };

        await client.SendMailAsync(message);
        _logger.LogInformation("Email sent to {To}: {Subject}", to, subject);
    }
}
