namespace PulseCheck.Api.Models;

public sealed class MonitorCheck
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MonitorId { get; set; }
    public Monitor? Monitor { get; set; }

    public MonitorStatus Status { get; set; }
    public int? StatusCode { get; set; }
    public int? ResponseTimeMs { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTimeOffset CheckedAt { get; set; } = DateTimeOffset.UtcNow;
}
