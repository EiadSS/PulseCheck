namespace PulseCheck.Api.Models;

public sealed class AnalyticsEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? UserId { get; set; }
    public ApplicationUser? User { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string? VisitorId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
