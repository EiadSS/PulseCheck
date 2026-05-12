using Microsoft.EntityFrameworkCore;
using PulseCheck.Api.Data;
using PulseCheck.Api.Models;
using MonitorEntity = PulseCheck.Api.Models.Monitor;

namespace PulseCheck.Api.Services;

public sealed class IncidentService
{
    private readonly NotificationService? _notifications;

    public IncidentService(NotificationService? notifications = null)
    {
        _notifications = notifications;
    }

    public static bool IsHealthy(MonitorStatus status) => status is MonitorStatus.Up or MonitorStatus.Degraded;
    public static bool IsIncidentStatus(MonitorStatus status) => status is MonitorStatus.Down or MonitorStatus.Error;

    public async Task ApplyTransitionAsync(
        AppDbContext db,
        MonitorEntity monitor,
        MonitorStatus previousStatus,
        MonitorStatus newStatus,
        DateTimeOffset checkedAt,
        string? message,
        CancellationToken cancellationToken)
    {
        var openIncident = await db.Incidents
            .Where(incident => incident.MonitorId == monitor.Id && incident.Status == IncidentStatus.Open)
            .OrderByDescending(incident => incident.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (IsHealthy(previousStatus) && IsIncidentStatus(newStatus) && openIncident is null)
        {
            var incident = new Incident
            {
                MonitorId = monitor.Id,
                Status = IncidentStatus.Open,
                StartedStatus = newStatus,
                StartedAt = checkedAt,
                Title = $"{monitor.Name} is {newStatus}",
                Summary = message
            };

            db.Incidents.Add(incident);

            if (_notifications is not null)
            {
                await _notifications.CreateAsync(
                    db,
                    monitor.UserId,
                    monitor.Id,
                    incident.Id,
                    NotificationType.MonitorFailed,
                    $"{monitor.Name} is {newStatus}",
                    message ?? $"{monitor.Name} entered {newStatus} at {checkedAt.LocalDateTime}.",
                    $"incident-opened:{incident.Id}",
                    checkedAt,
                    cancellationToken);
            }

            return;
        }

        if (openIncident is not null && IsHealthy(newStatus))
        {
            openIncident.Status = IncidentStatus.Resolved;
            openIncident.ResolvedStatus = newStatus;
            openIncident.ResolvedAt = checkedAt;
            openIncident.Summary = string.IsNullOrWhiteSpace(openIncident.Summary)
                ? $"Recovered with status {newStatus}."
                : $"{openIncident.Summary} Recovered with status {newStatus}.";

            if (_notifications is not null)
            {
                await _notifications.CreateAsync(
                    db,
                    monitor.UserId,
                    monitor.Id,
                    openIncident.Id,
                    NotificationType.MonitorRecovered,
                    $"{monitor.Name} recovered",
                    $"{monitor.Name} recovered with status {newStatus} at {checkedAt.LocalDateTime}.",
                    $"incident-resolved:{openIncident.Id}",
                    checkedAt,
                    cancellationToken);
            }
        }
    }
}
