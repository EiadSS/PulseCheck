using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PulseCheck.Api.Models;
using MonitorEntity = PulseCheck.Api.Models.Monitor;

namespace PulseCheck.Api.Data;

public sealed class AppDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<MonitorEntity> Monitors => Set<MonitorEntity>();
    public DbSet<MonitorCheck> MonitorChecks => Set<MonitorCheck>();
    public DbSet<Incident> Incidents => Set<Incident>();
    public DbSet<StatusPage> StatusPages => Set<StatusPage>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AnalyticsEvent> AnalyticsEvents => Set<AnalyticsEvent>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(user => user.WorkspaceName).HasMaxLength(120).IsRequired();
            entity.Property(user => user.PublicStatusSlug).HasMaxLength(80).IsRequired();
            entity.Property(user => user.EmailAlertsEnabled).HasDefaultValue(true);
            entity.HasIndex(user => user.PublicStatusSlug).IsUnique();
        });

        builder.Entity<MonitorEntity>(entity =>
        {
            entity.Property(monitor => monitor.Name).HasMaxLength(120).IsRequired();
            entity.Property(monitor => monitor.Url).HasMaxLength(2048).IsRequired();
            entity.Property(monitor => monitor.ExpectedKeyword).HasMaxLength(200);
            entity.Property(monitor => monitor.LastErrorMessage).HasMaxLength(500);
            entity.Property(monitor => monitor.LastSslErrorMessage).HasMaxLength(500);
            entity.HasIndex(monitor => new { monitor.UserId, monitor.Name });
            entity.HasIndex(monitor => new { monitor.IsPaused, monitor.NextCheckAt });
            entity.HasMany(monitor => monitor.Checks)
                .WithOne(check => check.Monitor)
                .HasForeignKey(check => check.MonitorId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(monitor => monitor.Incidents)
                .WithOne(incident => incident.Monitor)
                .HasForeignKey(incident => incident.MonitorId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(monitor => monitor.Notifications)
                .WithOne(notification => notification.Monitor)
                .HasForeignKey(notification => notification.MonitorId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<MonitorCheck>(entity =>
        {
            entity.Property(check => check.ErrorMessage).HasMaxLength(500);
            entity.HasIndex(check => new { check.MonitorId, check.CheckedAt });
        });

        builder.Entity<Incident>(entity =>
        {
            entity.Property(incident => incident.Title).HasMaxLength(160).IsRequired();
            entity.Property(incident => incident.Summary).HasMaxLength(500);
            entity.HasIndex(incident => new { incident.MonitorId, incident.Status });
            entity.HasMany(incident => incident.Notifications)
                .WithOne(notification => notification.Incident)
                .HasForeignKey(notification => notification.IncidentId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<StatusPage>(entity =>
        {
            entity.Property(page => page.Slug).HasMaxLength(80).IsRequired();
            entity.Property(page => page.Title).HasMaxLength(140).IsRequired();
            entity.HasIndex(page => page.Slug).IsUnique();
            entity.HasOne(page => page.User)
                .WithOne(user => user.StatusPage)
                .HasForeignKey<StatusPage>(page => page.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Notification>(entity =>
        {
            entity.Property(notification => notification.Title).HasMaxLength(160).IsRequired();
            entity.Property(notification => notification.Message).HasMaxLength(800).IsRequired();
            entity.Property(notification => notification.DedupKey).HasMaxLength(160).IsRequired();
            entity.Property(notification => notification.EmailErrorMessage).HasMaxLength(500);
            entity.HasIndex(notification => notification.DedupKey).IsUnique();
            entity.HasIndex(notification => new { notification.UserId, notification.IsRead, notification.CreatedAt });
            entity.HasOne(notification => notification.User)
                .WithMany(user => user.Notifications)
                .HasForeignKey(notification => notification.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<AnalyticsEvent>(entity =>
        {
            entity.Property(analyticsEvent => analyticsEvent.EventType).HasMaxLength(80).IsRequired();
            entity.Property(analyticsEvent => analyticsEvent.Path).HasMaxLength(300).IsRequired();
            entity.HasIndex(analyticsEvent => new { analyticsEvent.EventType, analyticsEvent.CreatedAt });
            entity.HasIndex(analyticsEvent => new { analyticsEvent.Path, analyticsEvent.CreatedAt });
            entity.HasIndex(analyticsEvent => new { analyticsEvent.UserId, analyticsEvent.CreatedAt });
            entity.HasOne(analyticsEvent => analyticsEvent.User)
                .WithMany()
                .HasForeignKey(analyticsEvent => analyticsEvent.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
