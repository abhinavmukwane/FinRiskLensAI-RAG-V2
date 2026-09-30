using FinRiskLensAI.Core.Interfaces.IServices.Common;
using FinRiskLensAI.Core.Models.Common;
using FinRiskLensAI.Services.Templates;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FinRiskLensAI.Services.Implementation.Common
{
    /// <summary>
    /// SMTP email sender built on MailKit. Uses explicit STARTTLS ("TLS" in mail
    /// clients) when EnableSsl is true — the mode the finrisklensai.com mail
    /// server supports on port 587.
    /// </summary>
    public class EmailService : IEmailService
    {
        private readonly SmtpSettings _settings;
        private readonly ILogger<EmailService> _logger;

        public EmailService(SmtpSettings settings, ILogger<EmailService> logger)
        {
            _settings = settings;
            _logger = logger;
        }

        public async Task<bool> SendEmailAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(toEmail))
            {
                _logger.LogWarning("Email send skipped — no recipient address supplied (subject: {Subject})", subject);
                return false;
            }

            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromEmail));
                message.To.Add(MailboxAddress.Parse(toEmail.Trim()));
                message.Subject = subject;
                message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

                using var client = new SmtpClient
                {
                    Timeout = _settings.TimeoutSeconds * 1000
                };

                if (_settings.AllowCertificateNameMismatch)
                {
                    // Shared hosting: we connect as finrisklensai.com but the server
                    // presents its own valid *.webhostbox.net certificate. Accept the
                    // name mismatch alone — any other chain problem still fails.
                    client.ServerCertificateValidationCallback =
                        (sender, cert, chain, errors) =>
                            errors == SslPolicyErrors.None ||
                            errors == SslPolicyErrors.RemoteCertificateNameMismatch;
                }

                var security = _settings.EnableSsl
                    ? SecureSocketOptions.StartTls        // explicit TLS upgrade ("TLS" in mail clients)
                    : SecureSocketOptions.StartTlsWhenAvailable;

                await client.ConnectAsync(_settings.Host, _settings.Port, security, ct);
                await client.AuthenticateAsync(_settings.Username, _settings.Password, ct);
                await client.SendAsync(message, ct);
                await client.DisconnectAsync(quit: true, ct);

                _logger.LogInformation("Email sent to {To} (subject: {Subject})", toEmail, subject);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed sending email to {To} (subject: {Subject})", toEmail, subject);
                return false;
            }
        }

        public Task<bool> SendLoginOtpAsync(string toEmail, string otpCode, string? recipientName = null, int expiryMinutes = 10, string? theme = null, CancellationToken ct = default)
        {
            var subject = LoginOtpEmailTemplate.Subject(otpCode);
            var body = LoginOtpEmailTemplate.Build(otpCode, recipientName, expiryMinutes, theme);
            return SendEmailAsync(toEmail, subject, body, ct);
        }

    }
}
