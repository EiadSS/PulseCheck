using System.ComponentModel.DataAnnotations;
using PulseCheck.Api.Models;

namespace PulseCheck.Api.Contracts;

public sealed record AnalyticsEventRequest(
    [property: Required, MaxLength(80)] string EventType,
    [property: Required, MaxLength(300)] string Path,
    [property: MaxLength(80)] string? VisitorId = null);

public sealed record AnalyticsSummaryDto(
    string Range,
    DateTimeOffset Since,
    DateTimeOffset GeneratedAt,
    int TotalUsers,
    int NewUsers,
    int ActiveUsers,
    int UniqueVisitors,
    int AnonymousVisitors,
    int TotalMonitors,
    int MonitorsCreated,
    double AverageMonitorsPerUser,
    int PageViews,
    int PublicStatusPageViews,
    int MonitorChecks,
    double? AverageResponseTimeMs,
    int IncidentsOpened,
    int IncidentsResolved,
    int NotificationsCreated,
    IReadOnlyCollection<AnalyticsTopPageDto> TopPages,
    IReadOnlyCollection<AnalyticsMonitorStatusCountDto> CheckStatusCounts,
    IReadOnlyCollection<AnalyticsEmailStatusCountDto> EmailStatusCounts,
    IReadOnlyCollection<AnalyticsMonitorActivityDto> MonitorActivity,
    IReadOnlyCollection<AnalyticsSeriesPointDto> NewUsersOverTime,
    IReadOnlyCollection<AnalyticsRecentSignupDto> RecentSignups);

public sealed record AnalyticsTopPageDto(string Path, int Views);

public sealed record AnalyticsMonitorStatusCountDto(MonitorStatus Status, int Count);

public sealed record AnalyticsEmailStatusCountDto(NotificationEmailStatus Status, int Count);

public sealed record AnalyticsMonitorActivityDto(
    Guid Id,
    string Name,
    string Url,
    MonitorStatus CurrentStatus,
    int CheckCount,
    DateTimeOffset? LastCheckedAt);

public sealed record AnalyticsSeriesPointDto(DateTimeOffset PeriodStart, int Count);

public sealed record AnalyticsRecentSignupDto(Guid Id, string Email, string WorkspaceName, DateTimeOffset CreatedAt);
