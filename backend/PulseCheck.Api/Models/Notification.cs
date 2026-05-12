namespace PulseCheck.Api.Models;

public sealed class Notification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public ApplicationUser? User { get; set; }

    public Guid? MonitorId { get; set; }
    public Monitor? Monitor { get; set; }

    public Guid? IncidentId { get; set; }
    public Incident? Incident { get; set; }

    public NotificationType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string DedupKey { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public NotificationEmailStatus EmailStatus { get; set; } = NotificationEmailStatus.NotConfigured;
    public string? EmailErrorMessage { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReadAt { get; set; }
}
