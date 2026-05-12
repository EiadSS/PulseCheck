using PulseCheck.Api.Models;

namespace PulseCheck.Api.Contracts;

public sealed record SloSummaryDto(
    double TargetPercentage,
    IReadOnlyCollection<SloWindowDto> Windows,
    IReadOnlyCollection<SloMonitorDto> Monitors);

public sealed record SloWindowDto(
    string Range,
    double UptimePercentage,
    double ErrorBudgetUsedPercentage,
    double ErrorBudgetRemainingPercentage,
    bool IsCompliant);

public sealed record SloMonitorDto(
    Guid Id,
    string Name,
    MonitorStatus CurrentStatus,
    double Uptime24Hours,
    double Uptime7Days,
    double Uptime30Days,
    double ErrorBudgetUsed30Days,
    bool IsCompliant30Days);
