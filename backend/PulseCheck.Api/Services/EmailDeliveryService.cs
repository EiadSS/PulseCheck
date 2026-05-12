using System.Net;
using System.Net.Mail;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Mime;
using System.Text.Json;
using System.Text.Json.Serialization;
using PulseCheck.Api.Models;

namespace PulseCheck.Api.Services;

public sealed record EmailDeliveryResult(NotificationEmailStatus Status, string? ErrorMessage);

public sealed class EmailDeliveryService
{
    private const string DefaultResendApiUrl = "https://api.resend.com/emails";
    private static readonly JsonSerializerOptions ResendJsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailDeliveryService> _logger;
    private readonly HttpClient _httpClient;

    public EmailDeliveryService(IConfiguration configuration, ILogger<EmailDeliveryService> logger, HttpClient httpClient)
    {
        _configuration = configuration;
        _logger = logger;
        _httpClient = httpClient;
    }

    public bool IsConfigured()
    {
        return GetResendSettings() is not null || GetSmtpSettings() is not null;
    }

    public async Task<EmailDeliveryResult> SendAsync(
        string toEmail,
        string subject,
        string textBody,
        string? htmlBody,
        CancellationToken cancellationToken)
    {
        var resend = GetResendSettings();
        if (resend is not null)
        {
            return await SendWithResendAsync(resend, toEmail, subject, textBody, htmlBody, cancellationToken);
        }

        var smtp = GetSmtpSettings();
        if (smtp is not null)
        {
            return await SendWithSmtpAsync(smtp, toEmail, subject, textBody, htmlBody, cancellationToken);
        }

        _logger.LogInformation("Email delivery is not configured; skipping notification email to {Email}.", toEmail);
        return new EmailDeliveryResult(NotificationEmailStatus.NotConfigured, "Email delivery is not configured.");
    }

    private async Task<EmailDeliveryResult> SendWithResendAsync(
        ResendSettings settings,
        string toEmail,
        string subject,
        string textBody,
        string? htmlBody,
        CancellationToken cancellationToken)
    {
        var payload = new ResendEmailRequest(
            FormatSender(settings.FromEmail, settings.FromName),
            toEmail,
            subject,
            textBody,
            htmlBody);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, settings.ApiUrl)
            {
                Content = JsonContent.Create(payload, options: ResendJsonOptions)
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return new EmailDeliveryResult(NotificationEmailStatus.Sent, null);
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var error = $"Resend API returned {(int)response.StatusCode}: {TrimForStorage(body)}";
            _logger.LogWarning("Failed to send notification email to {Email}. {Error}", toEmail, error);
            return new EmailDeliveryResult(NotificationEmailStatus.Failed, error);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to send notification email to {Email} through Resend.", toEmail);
            return new EmailDeliveryResult(NotificationEmailStatus.Failed, ex.Message);
        }
    }

    private async Task<EmailDeliveryResult> SendWithSmtpAsync(
        SmtpSettings settings,
        string toEmail,
        string subject,
        string textBody,
        string? htmlBody,
        CancellationToken cancellationToken)
    {
        try
        {
            using var message = new MailMessage
            {
                From = new MailAddress(settings.FromEmail, settings.FromName),
                Subject = subject,
                Body = htmlBody ?? textBody,
                IsBodyHtml = !string.IsNullOrWhiteSpace(htmlBody)
            };
            message.To.Add(toEmail);

            if (!string.IsNullOrWhiteSpace(htmlBody))
            {
                message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(textBody, null, MediaTypeNames.Text.Plain));
                message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(htmlBody, null, MediaTypeNames.Text.Html));
            }

            using var client = new SmtpClient(settings.Host, settings.Port)
            {
                EnableSsl = settings.EnableSsl
            };

            if (!string.IsNullOrWhiteSpace(settings.Username))
            {
                client.Credentials = new NetworkCredential(settings.Username, settings.Password);
            }

            await client.SendMailAsync(message, cancellationToken);
            return new EmailDeliveryResult(NotificationEmailStatus.Sent, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to send notification email to {Email}.", toEmail);
            return new EmailDeliveryResult(NotificationEmailStatus.Failed, ex.Message);
        }
    }

    private ResendSettings? GetResendSettings()
    {
        var apiKey = FirstConfiguredValue("Resend:ApiKey", "RESEND_API_KEY");

        if (string.IsNullOrWhiteSpace(apiKey) && IsConfiguredForResendSmtp())
        {
            apiKey = FirstConfiguredValue("Smtp:Password", "SMTP_PASSWORD");
        }

        var fromEmail = FirstConfiguredValue("Resend:FromEmail", "RESEND_FROM_EMAIL", "Smtp:FromEmail", "SMTP_FROM_EMAIL");
        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(fromEmail))
        {
            return null;
        }

        return new ResendSettings(
            apiKey,
            fromEmail,
            FirstConfiguredValue("Resend:FromName", "RESEND_FROM_NAME", "Smtp:FromName", "SMTP_FROM_NAME") ?? "PulseCheck",
            FirstConfiguredValue("Resend:ApiUrl", "RESEND_API_URL") ?? DefaultResendApiUrl);
    }

    private SmtpSettings? GetSmtpSettings()
    {
        var host = _configuration["Smtp:Host"];
        var fromEmail = _configuration["Smtp:FromEmail"];
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(fromEmail))
        {
            return null;
        }

        var username = _configuration["Smtp:Username"];
        var password = _configuration["Smtp:Password"];
        if (!string.IsNullOrWhiteSpace(username) && string.IsNullOrWhiteSpace(password))
        {
            return null;
        }

        return new SmtpSettings(
            host,
            _configuration.GetValue("Smtp:Port", 587),
            _configuration.GetValue("Smtp:EnableSsl", true),
            username,
            password,
            fromEmail,
            _configuration["Smtp:FromName"] ?? "PulseCheck");
    }

    private bool IsConfiguredForResendSmtp()
    {
        var host = FirstConfiguredValue("Smtp:Host", "SMTP_HOST");
        return string.Equals(host, "smtp.resend.com", StringComparison.OrdinalIgnoreCase);
    }

    private string? FirstConfiguredValue(params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = _configuration[key];
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static string FormatSender(string fromEmail, string fromName)
    {
        if (string.IsNullOrWhiteSpace(fromName))
        {
            return fromEmail;
        }

        return $"{fromName.Replace("\"", string.Empty)} <{fromEmail}>";
    }

    private static string TrimForStorage(string value)
    {
        value = string.IsNullOrWhiteSpace(value) ? "No response body." : value.Trim();
        return value.Length <= 400 ? value : $"{value[..400]}...";
    }

    private sealed record ResendSettings(string ApiKey, string FromEmail, string FromName, string ApiUrl);

    private sealed record SmtpSettings(
        string Host,
        int Port,
        bool EnableSsl,
        string? Username,
        string? Password,
        string FromEmail,
        string FromName);

    private sealed record ResendEmailRequest(string From, string To, string Subject, string Text, string? Html);
}
