namespace PulseCheck.Api.Models;

public enum MonitorStatus
{
    Up = 0,
    Degraded = 1,
    Error = 2,
    Down = 3,
    Paused = 4
}
