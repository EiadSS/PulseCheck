namespace PulseCheck.Api.Models;

public sealed class Monitor
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public ApplicationUser? User { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public MonitorType Type { get; set; } = MonitorType.Website;
    public int CheckIntervalSeconds { get; set; } = 60;
    public int TimeoutSeconds { get; set; } = 10;
    public int DegradedThresholdMs { get; set; } = 800;
    public int ExpectedStatusCode { get; set; } = 200;
    public string? ExpectedKeyword { get; set; }
    public bool IsPaused { get; set; }
    public bool IsPublic { get; set; }

    public MonitorStatus CurrentStatus { get; set; } = MonitorStatus.Up;
    public DateTimeOffset? LastCheckedAt { get; set; }
    public int? LastStatusCode { get; set; }
    public int? LastResponseTimeMs { get; set; }
    public string? LastErrorMessage { get; set; }
    public SslCertificateStatus SslCertificateStatus { get; set; } = SslCertificateStatus.NotApplicable;
    public DateTimeOffset? SslCertificateExpiresAt { get; set; }
    public int? SslCertificateDaysRemaining { get; set; }
    public string? LastSslErrorMessage { get; set; }
    public DateTimeOffset NextCheckAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<MonitorCheck> Checks { get; set; } = new List<MonitorCheck>();
    public ICollection<Incident> Incidents { get; set; } = new List<Incident>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}
