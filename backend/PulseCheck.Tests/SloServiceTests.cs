using Microsoft.EntityFrameworkCore;
using PulseCheck.Api.Data;
using PulseCheck.Api.Models;
using PulseCheck.Api.Services;
using Xunit;
using MonitorEntity = PulseCheck.Api.Models.Monitor;

namespace PulseCheck.Tests;

public sealed class SloServiceTests
{
    [Fact]
    public async Task CalculateAvailabilityAsync_UsesDurationWeightedStatusHistory()
    {
        await using var db = CreateDbContext();
        var now = DateTimeOffset.UtcNow;
        var monitor = new MonitorEntity
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Name = "API",
            Url = "https://example.com",
            CurrentStatus = MonitorStatus.Up
        };
        db.Monitors.Add(monitor);
        db.MonitorChecks.AddRange(
            new MonitorCheck { MonitorId = monitor.Id, Status = MonitorStatus.Up, CheckedAt = now.AddHours(-24) },
            new MonitorCheck { MonitorId = monitor.Id, Status = MonitorStatus.Down, CheckedAt = now.AddHours(-12) },
            new MonitorCheck { MonitorId = monitor.Id, Status = MonitorStatus.Up, CheckedAt = now.AddHours(-6) });
        await db.SaveChangesAsync();

        var uptime = await new SloService().CalculateAvailabilityAsync(
            db,
            monitor.Id,
            monitor.CurrentStatus,
            now.AddHours(-24),
            now,
            CancellationToken.None);

        Assert.Equal(75, uptime);
    }

    [Fact]
    public async Task BuildSummaryAsync_UsesDefaultTargetAndSortsBudgetBurn()
    {
        await using var db = CreateDbContext();
        var now = DateTimeOffset.UtcNow;
        var userId = Guid.NewGuid();
        var monitor = new MonitorEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = "API",
            Url = "https://example.com",
            CurrentStatus = MonitorStatus.Down
        };
        db.Monitors.Add(monitor);
        db.MonitorChecks.Add(new MonitorCheck { MonitorId = monitor.Id, Status = MonitorStatus.Down, CheckedAt = now.AddDays(-30) });
        await db.SaveChangesAsync();

        var summary = await new SloService().BuildSummaryAsync(db, userId, now, CancellationToken.None);

        Assert.Equal(99.9, summary.TargetPercentage);
        Assert.Single(summary.Monitors);
        Assert.Equal(100, summary.Monitors.Single().ErrorBudgetUsed30Days);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}
