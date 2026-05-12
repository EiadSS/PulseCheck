using System.Globalization;
using System.Net;
using PulseCheck.Api.Models;
using MonitorEntity = PulseCheck.Api.Models.Monitor;

namespace PulseCheck.Api.Services;

public sealed record NotificationEmailContent(string Subject, string TextBody, string HtmlBody);

public static class NotificationEmailTemplate
{
    public static NotificationEmailContent Build(
        Notification notification,
        ApplicationUser? user,
        MonitorEntity? monitor,
        Incident? incident,
        string frontendBaseUrl)
    {
        var actionUrl = BuildActionUrl(frontendBaseUrl, monitor?.Id);
        var eventLabel = GetEventLabel(notification.Type);
        var eventStatus = GetEventStatus(notification, monitor, incident);
        var checkedAt = FormatUtc(monitor?.LastCheckedAt ?? notification.CreatedAt);
        var subject = $"PulseCheck: {notification.Title}";
        var tone = GetTone(notification.Type);

        var textRows = new List<(string Label, string? Value)>
        {
            ("Workspace", user?.WorkspaceName),
            ("Monitor", monitor?.Name),
            ("Monitor URL", monitor?.Url),
            ("Event", eventLabel),
            ("Status", eventStatus),
            ("Checked at", checkedAt),
            ("HTTP status", monitor?.LastStatusCode?.ToString(CultureInfo.InvariantCulture)),
            ("Response time", monitor?.LastResponseTimeMs is null ? null : $"{monitor.LastResponseTimeMs} ms"),
            ("Incident", incident is null ? null : incident.Status.ToString()),
            ("SSL certificate", FormatSsl(monitor)),
            ("Details", notification.Message),
            ("Open in PulseCheck", actionUrl)
        };

        var textBody = $"""
            {notification.Title}

            {notification.Message}

            {FormatTextRows(textRows)}

            You are receiving this because email alerts are enabled for your PulseCheck account.
            """;

        var htmlRows = string.Concat(textRows
            .Where(row => !string.IsNullOrWhiteSpace(row.Value))
            .Select(row => DetailRow(row.Label, row.Value!)));

        var htmlBody = $"""
            <!doctype html>
            <html lang="en">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>{Encode(subject)}</title>
            </head>
            <body style="margin:0;background:#f8fafc;color:#0f172a;font-family:Arial,Helvetica,sans-serif;">
              <div style="display:none;max-height:0;overflow:hidden;opacity:0;">{Encode(notification.Message)}</div>
              <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background:#f8fafc;padding:32px 16px;">
                <tr>
                  <td align="center">
                    <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="max-width:640px;background:#ffffff;border:1px solid #e2e8f0;border-radius:12px;overflow:hidden;">
                      <tr>
                        <td style="padding:24px 28px 20px 28px;border-bottom:1px solid #e2e8f0;">
                          <div style="font-size:13px;font-weight:700;color:#0284c7;text-transform:uppercase;letter-spacing:.04em;">PulseCheck alert</div>
                          <h1 style="margin:10px 0 8px 0;font-size:24px;line-height:1.25;color:#0f172a;">{Encode(notification.Title)}</h1>
                          <span style="display:inline-block;border-radius:999px;background:{tone.Background};color:{tone.Foreground};padding:6px 10px;font-size:13px;font-weight:700;">{Encode(eventLabel)}</span>
                        </td>
                      </tr>
                      <tr>
                        <td style="padding:24px 28px;">
                          <p style="margin:0 0 20px 0;font-size:15px;line-height:1.6;color:#334155;">{Encode(notification.Message)}</p>
                          <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="border-collapse:collapse;border:1px solid #e2e8f0;border-radius:10px;overflow:hidden;">
                            {htmlRows}
                          </table>
                          <div style="margin-top:24px;">
                            <a href="{Encode(actionUrl)}" style="display:inline-block;background:#0284c7;color:#ffffff;text-decoration:none;border-radius:8px;padding:12px 16px;font-size:14px;font-weight:700;">View in PulseCheck</a>
                          </div>
                        </td>
                      </tr>
                      <tr>
                        <td style="padding:16px 28px;background:#f8fafc;border-top:1px solid #e2e8f0;color:#64748b;font-size:12px;line-height:1.5;">
                          You are receiving this because email alerts are enabled for your PulseCheck account.
                        </td>
                      </tr>
                    </table>
                  </td>
                </tr>
              </table>
            </body>
            </html>
            """;

        return new NotificationEmailContent(subject, textBody, htmlBody);
    }

    private static string BuildActionUrl(string frontendBaseUrl, Guid? monitorId)
    {
        var baseUrl = string.IsNullOrWhiteSpace(frontendBaseUrl) ? "http://localhost:5173" : frontendBaseUrl.TrimEnd('/');
        return monitorId is null ? $"{baseUrl}/dashboard" : $"{baseUrl}/monitors/{monitorId}";
    }

    private static string GetEventLabel(NotificationType type)
    {
        return type switch
        {
            NotificationType.MonitorFailed => "Monitor failure",
            NotificationType.MonitorRecovered => "Monitor recovered",
            NotificationType.SslCertificateWarning => "SSL certificate warning",
            NotificationType.SslCertificateCritical => "SSL certificate critical",
            _ => "PulseCheck notification"
        };
    }

    private static string? GetEventStatus(Notification notification, MonitorEntity? monitor, Incident? incident)
    {
        return notification.Type switch
        {
            NotificationType.MonitorFailed => incident?.StartedStatus.ToString() ?? monitor?.CurrentStatus.ToString(),
            NotificationType.MonitorRecovered => incident?.ResolvedStatus?.ToString() ?? monitor?.CurrentStatus.ToString(),
            _ => monitor?.CurrentStatus.ToString()
        };
    }

    private static string? FormatSsl(MonitorEntity? monitor)
    {
        if (monitor is null || monitor.SslCertificateStatus == SslCertificateStatus.NotApplicable)
        {
            return null;
        }

        var details = monitor.SslCertificateDaysRemaining is null
            ? monitor.SslCertificateStatus.ToString()
            : $"{monitor.SslCertificateStatus} ({monitor.SslCertificateDaysRemaining} days remaining)";

        return monitor.SslCertificateExpiresAt is null
            ? details
            : $"{details}, expires {FormatUtc(monitor.SslCertificateExpiresAt.Value)}";
    }

    private static string FormatUtc(DateTimeOffset value)
    {
        return value.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture);
    }

    private static string FormatTextRows(IEnumerable<(string Label, string? Value)> rows)
    {
        return string.Join(
            Environment.NewLine,
            rows
                .Where(row => !string.IsNullOrWhiteSpace(row.Value))
                .Select(row => $"{row.Label}: {row.Value}"));
    }

    private static string DetailRow(string label, string value)
    {
        return $"""
            <tr>
              <td style="width:34%;padding:12px 14px;border-bottom:1px solid #e2e8f0;background:#f8fafc;color:#64748b;font-size:13px;font-weight:700;">{Encode(label)}</td>
              <td style="padding:12px 14px;border-bottom:1px solid #e2e8f0;color:#0f172a;font-size:14px;line-height:1.45;word-break:break-word;">{Encode(value)}</td>
            </tr>
            """;
    }

    private static string Encode(string value)
    {
        return WebUtility.HtmlEncode(value);
    }

    private static (string Background, string Foreground) GetTone(NotificationType type)
    {
        return type switch
        {
            NotificationType.MonitorRecovered => ("#ecfdf5", "#047857"),
            NotificationType.SslCertificateWarning => ("#fffbeb", "#b45309"),
            NotificationType.SslCertificateCritical => ("#fff1f2", "#be123c"),
            _ => ("#fff1f2", "#be123c")
        };
    }
}
