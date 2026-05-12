using PulseCheck.Api.Models;

namespace PulseCheck.Api.Contracts;

public sealed record PublicStatusPageDto(
    string Slug,
    string Title,
    MonitorStatus OverallStatus,
    double Uptime24Hours,
    double Uptime7Days,
    double Uptime30Days,
    IReadOnlyCollection<PublicMonitorDto> Monitors,
    IReadOnlyCollection<PublicIncidentDto> RecentIncidents);

public sealed record PublicMonitorDto(
    Guid Id,
    string Name,
    MonitorType Type,
    MonitorStatus CurrentStatus,
    DateTimeOffset? LastCheckedAt,
    int? LastResponseTimeMs,
    double Uptime24Hours,
    double Uptime7Days,
    double Uptime30Days);

public sealed record PublicIncidentDto(
    Guid Id,
    string MonitorName,
    IncidentStatus Status,
    string Title,
    DateTimeOffset StartedAt,
    DateTimeOffset? ResolvedAt);
