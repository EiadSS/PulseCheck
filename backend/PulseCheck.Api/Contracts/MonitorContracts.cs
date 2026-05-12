using System.ComponentModel.DataAnnotations;
using PulseCheck.Api.Models;

namespace PulseCheck.Api.Contracts;

public sealed record MonitorRequest(
    [property: Required, MaxLength(120)] string Name,
    [property: Required, Url, MaxLength(2048)] string Url,
    MonitorType Type,
    [property: Range(30, 86400)] int CheckIntervalSeconds,
    [property: Range(1, 60)] int TimeoutSeconds,
    [property: Range(1, 120000)] int DegradedThresholdMs,
    [property: Range(100, 599)] int ExpectedStatusCode,
    [property: MaxLength(200)] string? ExpectedKeyword,
    bool IsPublic);

public sealed record MonitorSummaryDto(
    Guid Id,
    string Name,
    string Url,
    MonitorType Type,
    MonitorStatus CurrentStatus,
    bool IsPaused,
    bool IsPublic,
    DateTimeOffset? LastCheckedAt,
    int? LastStatusCode,
    int? LastResponseTimeMs,
    string? LastErrorMessage,
    SslCertificateStatus SslCertificateStatus,
    DateTimeOffset? SslCertificateExpiresAt,
    int? SslCertificateDaysRemaining,
    string? LastSslErrorMessage,
    double UptimePercentage,
    int OpenIncidentCount);

public sealed record MonitorDetailDto(
    Guid Id,
    string Name,
    string Url,
    MonitorType Type,
    MonitorStatus CurrentStatus,
    bool IsPaused,
    bool IsPublic,
    int CheckIntervalSeconds,
    int TimeoutSeconds,
    int DegradedThresholdMs,
    int ExpectedStatusCode,
    string? ExpectedKeyword,
    DateTimeOffset? LastCheckedAt,
    int? LastStatusCode,
    int? LastResponseTimeMs,
    string? LastErrorMessage,
    SslCertificateStatus SslCertificateStatus,
    DateTimeOffset? SslCertificateExpiresAt,
    int? SslCertificateDaysRemaining,
    string? LastSslErrorMessage,
    double Uptime24Hours,
    double Uptime7Days,
    double Uptime30Days);

public sealed record MonitorCheckDto(
    Guid Id,
    MonitorStatus Status,
    int? StatusCode,
    int? ResponseTimeMs,
    string? ErrorMessage,
    DateTimeOffset CheckedAt);

public sealed record IncidentDto(
    Guid Id,
    IncidentStatus Status,
    MonitorStatus StartedStatus,
    MonitorStatus? ResolvedStatus,
    string Title,
    string? Summary,
    DateTimeOffset StartedAt,
    DateTimeOffset? ResolvedAt);
