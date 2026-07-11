using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Mime;

namespace OpportunityHub.Services;

/// <summary>
/// SMTP-based email service for sending emails (configured for Amazon SES SMTP).
/// Reads settings from configuration (Smtp section). Credentials must be supplied via configuration or environment.
/// </summary>
public class SmtpEmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmtpEmailService> _logger;
    private readonly string _host;
    private readonly int _port;
    private readonly string _username;
    private readonly string _password;
    private readonly string _fromEmail;
    private readonly string? _fromName;

    public SmtpEmailService(IConfiguration configuration, ILogger<SmtpEmailService> logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _host = _configuration["Smtp:Host"] ?? throw new InvalidOperationException("Smtp:Host is not configured.");
        _fromEmail = _configuration["Smtp:FromEmail"] ?? throw new InvalidOperationException("Smtp:FromEmail is not configured.");
        _fromName = _configuration["Smtp:FromName"];

        if (!int.TryParse(_configuration["Smtp:Port"], out _port))
            _port = 587;

        _username = _configuration["Smtp:Username"] ?? string.Empty;
        _password = _configuration["Smtp:Password"] ?? string.Empty;
    }

    /// <summary>
    /// Sends an email asynchronously using SMTP configuration.
    /// Validates recipient address and logs errors. Credentials are not hardcoded.
    /// </summary>
    public async Task SendEmailAsync(string toEmail, string subject, string htmlBody)
    {
        if (string.IsNullOrWhiteSpace(toEmail)) throw new ArgumentException("Recipient email is required.", nameof(toEmail));
        if (string.IsNullOrWhiteSpace(subject)) throw new ArgumentException("Email subject is required.", nameof(subject));
        if (string.IsNullOrWhiteSpace(htmlBody)) throw new ArgumentException("Email body is required.", nameof(htmlBody));

        try
        {
            // Validate recipient email format
            try
            {
                var _ = new MailAddress(toEmail);
            }
            catch (FormatException)
            {
                throw new ArgumentException("Recipient email address is not in a valid format.", nameof(toEmail));
            }

            using var message = new MailMessage
            {
                From = !string.IsNullOrWhiteSpace(_fromName) ? new MailAddress(_fromEmail, _fromName) : new MailAddress(_fromEmail),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true,
                BodyEncoding = System.Text.Encoding.UTF8,
            };

            message.To.Add(new MailAddress(toEmail));

            using var client = new SmtpClient(_host, _port)
            {
                DeliveryMethod = SmtpDeliveryMethod.Network,
                EnableSsl = true,
                Timeout = 30000 // 30s
            };

            // Provide credentials only when configured
            if (!string.IsNullOrWhiteSpace(_username) && !string.IsNullOrWhiteSpace(_password))
            {
                client.UseDefaultCredentials = false;
                client.Credentials = new NetworkCredential(_username, _password);
            }
            else
            {
                // If no credentials are provided here, the hosting environment should provide credentials (e.g., environment vars or role-based access).
                client.UseDefaultCredentials = false;
                _logger.LogWarning("SMTP credentials not provided in configuration. Ensure SMTP credentials are supplied via user-secrets or environment variables.");
            }

            _logger.LogInformation("Sending email to {Email} via SMTP host {Host}:{Port}", toEmail, _host, _port);

            await client.SendMailAsync(message).ConfigureAwait(false);

            _logger.LogInformation("Email successfully sent to {Email}", toEmail);
        }
        catch (SmtpException ex)
        {
            _logger.LogError(ex, "SMTP error while sending email to {Email}", toEmail);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while sending email to {Email}", toEmail);
            throw;
        }
    }
}