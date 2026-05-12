using Microsoft.EntityFrameworkCore;
using PulseCheck.Api.Data;
using PulseCheck.Api.Models;

namespace PulseCheck.Api.Services;

public sealed class UptimeCalculator
{
    public async Task<double> CalculateAsync(
        AppDbContext db,
        Guid monitorId,
        DateTimeOffset since,
        CancellationToken cancellationToken)
    {
        var checks = await db.MonitorChecks
            .AsNoTracking()
            .Where(check => check.MonitorId == monitorId && check.CheckedAt >= since)
            .Select(check => check.Status)
            .ToListAsync(cancellationToken);

        return Calculate(checks);
    }

    public static double Calculate(IReadOnlyCollection<MonitorStatus> statuses)
    {
        if (statuses.Count == 0)
        {
            return 100;
        }

        var successful = statuses.Count(IncidentService.IsHealthy);
        return Math.Round(successful * 100d / statuses.Count, 2);
    }
}
