using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PulseCheck.Api.Data;
using PulseCheck.Api.Models;
using MonitorEntity = PulseCheck.Api.Models.Monitor;

namespace PulseCheck.Api.Services;

public sealed class NotificationService
{
    private readonly EmailDeliveryService _emailDelivery;
    private readonly IConfiguration _configuration;

    public NotificationService(EmailDeliveryService emailDelivery, IConfiguration configuration)
    {
        _emailDelivery = emailDelivery;
        _configuration = configuration;
    }

    public async Task<Notification?> CreateAsync(
        AppDbContext db,
        Guid userId,
        Guid? monitorId,
        Guid? incidentId,
        NotificationType type,
        string title,
        string message,
        string dedupKey,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken)
    {
        if (db.Notifications.Local.Any(notification => notification.DedupKey == dedupKey) ||
            await db.Notifications.AnyAsync(notification => notification.DedupKey == dedupKey, cancellationToken))
        {
            return null;
        }

        var notification = new Notification
        {
            UserId = userId,
            MonitorId = monitorId,
            IncidentId = incidentId,
            Type = type,
            Title = title,
            Message = message,
            DedupKey = dedupKey,
            CreatedAt = createdAt
        };

        db.Notifications.Add(notification);

        var user = await db.Users
            .Where(user => user.Id == userId)
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null || string.IsNullOrWhiteSpace(user.Email))
        {
            notification.EmailStatus = NotificationEmailStatus.Skipped;
            notification.EmailErrorMessage = "User email is unavailable.";
            return notification;
        }

        if (!user.EmailAlertsEnabled)
        {
            notification.EmailStatus = NotificationEmailStatus.Skipped;
            notification.EmailErrorMessage = "Email alerts are disabled.";
            return notification;
        }

        var monitor = await FindMonitorAsync(db, monitorId, cancellationToken);
        var incident = await FindIncidentAsync(db, incidentId, cancellationToken);
        var emailContent = NotificationEmailTemplate.Build(
            notification,
            user,
            monitor,
            incident,
            ResolveFrontendBaseUrl());
        var result = await _emailDelivery.SendAsync(
            user.Email,
            emailContent.Subject,
            emailContent.TextBody,
            emailContent.HtmlBody,
            cancellationToken);
        notification.EmailStatus = result.Status;
        notification.EmailErrorMessage = result.ErrorMessage;
        return notification;
    }

    private async Task<MonitorEntity?> FindMonitorAsync(AppDbContext db, Guid? monitorId, CancellationToken cancellationToken)
    {
        if (monitorId is null)
        {
            return null;
        }

        return db.Monitors.Local.FirstOrDefault(monitor => monitor.Id == monitorId.Value) ??
            await db.Monitors.FirstOrDefaultAsync(monitor => monitor.Id == monitorId.Value, cancellationToken);
    }

    private async Task<Incident?> FindIncidentAsync(AppDbContext db, Guid? incidentId, CancellationToken cancellationToken)
    {
        if (incidentId is null)
        {
            return null;
        }

        return db.Incidents.Local.FirstOrDefault(incident => incident.Id == incidentId.Value) ??
            await db.Incidents.FirstOrDefaultAsync(incident => incident.Id == incidentId.Value, cancellationToken);
    }

    private string ResolveFrontendBaseUrl()
    {
        var explicitUrl = _configuration["PulseCheck:FrontendBaseUrl"];
        if (!string.IsNullOrWhiteSpace(explicitUrl))
        {
            return explicitUrl;
        }

        return (_configuration["PulseCheck:AllowedOrigins"] ?? "http://localhost:5173")
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault() ?? "http://localhost:5173";
    }
}
