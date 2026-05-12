using Microsoft.AspNetCore.Identity;

namespace PulseCheck.Api.Models;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string WorkspaceName { get; set; } = string.Empty;
    public string PublicStatusSlug { get; set; } = string.Empty;
    public bool EmailAlertsEnabled { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<Monitor> Monitors { get; set; } = new List<Monitor>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    public StatusPage? StatusPage { get; set; }
}
