namespace PulseCheck.Api.Contracts;

public sealed record DashboardSummaryDto(
    int TotalMonitors,
    int Up,
    int Degraded,
    int Error,
    int Down,
    int Paused,
    int OpenIncidents,
    double AverageUptime24Hours);
