using System.Threading.Tasks;

namespace OpportunityHub.Services;

/// <summary>
/// Interface for email service.
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Sends an email asynchronously.
    /// </summary>
    /// <param name="toEmail">Recipient email address</param>
    /// <param name="subject">Email subject</param>
    /// <param name="htmlBody">HTML body content</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task SendEmailAsync(string toEmail, string subject, string htmlBody);
}