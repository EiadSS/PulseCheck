using Microsoft.EntityFrameworkCore;
using PulseCheck.Api.Data;
using PulseCheck.Api.Models;
using PulseCheck.Api.Services;
using Xunit;
using MonitorEntity = PulseCheck.Api.Models.Monitor;

namespace PulseCheck.Tests;

public sealed class IncidentServiceTests
{
    [Theory]
    [InlineData(MonitorStatus.Up, MonitorStatus.Down)]
    [InlineData(MonitorStatus.Degraded, MonitorStatus.Error)]
    public async Task ApplyTransitionAsync_CreatesIncident_WhenHealthyMonitorFails(
        MonitorStatus previousStatus,
        MonitorStatus newStatus)
    {
        await using var db = CreateDbContext();
        var monitor = CreateMonitor(previousStatus);
        db.Monitors.Add(monitor);
        await db.SaveChangesAsync();

        await new IncidentService().ApplyTransitionAsync(
            db,
            monitor,
            previousStatus,
            newStatus,
            DateTimeOffset.UtcNow,
            "failure",
            CancellationToken.None);
        await db.SaveChangesAsync();

        var incident = await db.Incidents.SingleAsync();
        Assert.Equal(IncidentStatus.Open, incident.Status);
        Assert.Equal(newStatus, incident.StartedStatus);
    }

    [Fact]
    public async Task ApplyTransitionAsync_ResolvesOpenIncident_WhenMonitorRecovers()
    {
        await using var db = CreateDbContext();
        var monitor = CreateMonitor(MonitorStatus.Down);
        db.Monitors.Add(monitor);
        db.Incidents.Add(new Incident
        {
            MonitorId = monitor.Id,
            Status = IncidentStatus.Open,
            StartedStatus = MonitorStatus.Down,
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
            Title = "API is Down"
        });
        await db.SaveChangesAsync();

        await new IncidentService().ApplyTransitionAsync(
            db,
            monitor,
            MonitorStatus.Down,
            MonitorStatus.Up,
            DateTimeOffset.UtcNow,
            null,
            CancellationToken.None);
        await db.SaveChangesAsync();

        var incident = await db.Incidents.SingleAsync();
        Assert.Equal(IncidentStatus.Resolved, incident.Status);
        Assert.Equal(MonitorStatus.Up, incident.ResolvedStatus);
        Assert.NotNull(incident.ResolvedAt);
    }

    [Fact]
    public async Task ApplyTransitionAsync_DoesNotCreateDuplicateIncident_WhenOneIsAlreadyOpen()
    {
        await using var db = CreateDbContext();
        var monitor = CreateMonitor(MonitorStatus.Error);
        db.Monitors.Add(monitor);
        db.Incidents.Add(new Incident
        {
            MonitorId = monitor.Id,
            Status = IncidentStatus.Open,
            StartedStatus = MonitorStatus.Error,
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
            Title = "API is Error"
        });
        await db.SaveChangesAsync();

        await new IncidentService().ApplyTransitionAsync(
            db,
            monitor,
            MonitorStatus.Error,
            MonitorStatus.Down,
            DateTimeOffset.UtcNow,
            "still failing",
            CancellationToken.None);
        await db.SaveChangesAsync();

        Assert.Equal(1, await db.Incidents.CountAsync());
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private static MonitorEntity CreateMonitor(MonitorStatus status)
    {
        return new MonitorEntity
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Name = "API",
            Url = "https://example.com",
            CurrentStatus = status
        };
    }
}
