namespace AlonProject.Domain.Interfaces;

/// <summary>
/// Outbound email contract. The implementation decides the transport
/// (real SMTP in production, log-only in development).
/// </summary>
public interface IEmailSender
{
    /// <summary>Sends an HTML email to a single recipient.</summary>
    Task SendAsync(string to, string subject, string htmlBody);
}
