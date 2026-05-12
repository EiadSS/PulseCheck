namespace PulseCheck.Api.Models;

public sealed class Incident
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MonitorId { get; set; }
    public Monitor? Monitor { get; set; }

    public IncidentStatus Status { get; set; } = IncidentStatus.Open;
    public MonitorStatus StartedStatus { get; set; }
    public MonitorStatus? ResolvedStatus { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ResolvedAt { get; set; }

    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}
