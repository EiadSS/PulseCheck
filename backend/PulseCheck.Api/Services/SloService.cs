using Microsoft.EntityFrameworkCore;
using PulseCheck.Api.Contracts;
using PulseCheck.Api.Data;
using PulseCheck.Api.Models;

namespace PulseCheck.Api.Services;

public sealed class SloService
{
    public const double DefaultTargetPercentage = 99.9;

    public async Task<SloSummaryDto> BuildSummaryAsync(
        AppDbContext db,
        Guid userId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var monitors = await db.Monitors
            .AsNoTracking()
            .Where(monitor => monitor.UserId == userId)
            .OrderBy(monitor => monitor.Name)
            .ToListAsync(cancellationToken);

        var monitorDtos = new List<SloMonitorDto>();
        foreach (var monitor in monitors)
        {
            var uptime24Hours = await CalculateAvailabilityAsync(db, monitor.Id, monitor.CurrentStatus, now.AddHours(-24), now, cancellationToken);
            var uptime7Days = await CalculateAvailabilityAsync(db, monitor.Id, monitor.CurrentStatus, now.AddDays(-7), now, cancellationToken);
            var uptime30Days = await CalculateAvailabilityAsync(db, monitor.Id, monitor.CurrentStatus, now.AddDays(-30), now, cancellationToken);

            monitorDtos.Add(new SloMonitorDto(
                monitor.Id,
                monitor.Name,
                monitor.CurrentStatus,
                uptime24Hours,
                uptime7Days,
                uptime30Days,
                ErrorBudgetUsed(uptime30Days),
                uptime30Days >= DefaultTargetPercentage));
        }

        var windows = new[]
        {
            BuildWindow("24h", monitorDtos.Select(monitor => monitor.Uptime24Hours)),
            BuildWindow("7d", monitorDtos.Select(monitor => monitor.Uptime7Days)),
            BuildWindow("30d", monitorDtos.Select(monitor => monitor.Uptime30Days))
        };

        return new SloSummaryDto(
            DefaultTargetPercentage,
            windows,
            monitorDtos
                .OrderByDescending(monitor => monitor.ErrorBudgetUsed30Days)
                .ThenBy(monitor => monitor.Name)
                .ToList());
    }

    public async Task<double> CalculateAvailabilityAsync(
        AppDbContext db,
        Guid monitorId,
        MonitorStatus currentStatus,
        DateTimeOffset windowStart,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (now <= windowStart)
        {
            return 100;
        }

        var checks = await db.MonitorChecks
            .AsNoTracking()
            .Where(check => check.MonitorId == monitorId && check.CheckedAt >= windowStart && check.CheckedAt <= now)
            .OrderBy(check => check.CheckedAt)
            .Select(check => new CheckPoint(check.CheckedAt, check.Status))
            .ToListAsync(cancellationToken);

        var previousStatus = await db.MonitorChecks
            .AsNoTracking()
            .Where(check => check.MonitorId == monitorId && check.CheckedAt < windowStart)
            .OrderByDescending(check => check.CheckedAt)
            .Select(check => (MonitorStatus?)check.Status)
            .FirstOrDefaultAsync(cancellationToken);

        if (checks.Count == 0 && previousStatus is null)
        {
            return 100;
        }

        var status = previousStatus ?? checks.FirstOrDefault()?.Status ?? currentStatus;
        var cursor = windowStart;
        var availableTicks = 0L;

        foreach (var check in checks)
        {
            if (check.CheckedAt > cursor && IsAvailable(status))
            {
                availableTicks += (check.CheckedAt - cursor).Ticks;
            }

            status = check.Status;
            cursor = check.CheckedAt;
        }

        if (now > cursor && IsAvailable(status))
        {
            availableTicks += (now - cursor).Ticks;
        }

        return Math.Round(availableTicks * 100d / (now - windowStart).Ticks, 2);
    }

    private static SloWindowDto BuildWindow(string range, IEnumerable<double> uptimes)
    {
        var values = uptimes.ToList();
        var uptime = values.Count == 0 ? 100 : Math.Round(values.Average(), 2);

        return new SloWindowDto(
            range,
            uptime,
            ErrorBudgetUsed(uptime),
            ErrorBudgetRemaining(uptime),
            uptime >= DefaultTargetPercentage);
    }

    private static bool IsAvailable(MonitorStatus status) => status is MonitorStatus.Up or MonitorStatus.Degraded;

    private static double ErrorBudgetUsed(double uptime)
    {
        var budget = 100 - DefaultTargetPercentage;
        var downtime = Math.Max(0, 100 - uptime);
        return Math.Round(Math.Min(100, downtime * 100 / budget), 2);
    }

    private static double ErrorBudgetRemaining(double uptime)
    {
        return Math.Round(Math.Max(0, 100 - ErrorBudgetUsed(uptime)), 2);
    }

    private sealed record CheckPoint(DateTimeOffset CheckedAt, MonitorStatus Status);
}
