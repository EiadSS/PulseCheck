using PulseCheck.Api.Models;
using MonitorEntity = PulseCheck.Api.Models.Monitor;

namespace PulseCheck.Api.Services;

public sealed record HealthProbeResult(
    bool ReceivedResponse,
    int? StatusCode,
    int? ResponseTimeMs,
    string? Body,
    string? ErrorMessage);

public sealed record CheckClassification(MonitorStatus Status, string? ErrorMessage);

public sealed class MonitorStatusClassifier
{
    public CheckClassification Classify(MonitorEntity monitor, HealthProbeResult result)
    {
        if (!result.ReceivedResponse)
        {
            return new CheckClassification(MonitorStatus.Down, result.ErrorMessage ?? "Host did not respond.");
        }

        if (result.StatusCode != monitor.ExpectedStatusCode)
        {
            return new CheckClassification(
                MonitorStatus.Error,
                $"Expected HTTP {monitor.ExpectedStatusCode}, received HTTP {result.StatusCode}.");
        }

        if (!string.IsNullOrWhiteSpace(monitor.ExpectedKeyword) &&
            (result.Body is null || !result.Body.Contains(monitor.ExpectedKeyword, StringComparison.OrdinalIgnoreCase)))
        {
            return new CheckClassification(
                MonitorStatus.Error,
                $"Expected keyword \"{monitor.ExpectedKeyword}\" was not found.");
        }

        if (result.ResponseTimeMs >= monitor.DegradedThresholdMs)
        {
            return new CheckClassification(MonitorStatus.Degraded, "Response time exceeded degraded threshold.");
        }

        return new CheckClassification(MonitorStatus.Up, null);
    }
}
