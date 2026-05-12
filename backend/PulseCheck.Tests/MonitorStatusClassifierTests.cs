using PulseCheck.Api.Models;
using PulseCheck.Api.Services;
using Xunit;
using MonitorEntity = PulseCheck.Api.Models.Monitor;

namespace PulseCheck.Tests;

public sealed class MonitorStatusClassifierTests
{
    private readonly MonitorStatusClassifier _classifier = new();

    [Fact]
    public void Classify_ReturnsUp_WhenResponseMatchesAndIsFast()
    {
        var monitor = CreateMonitor();
        var result = _classifier.Classify(monitor, new HealthProbeResult(true, 200, 120, "ok", null));

        Assert.Equal(MonitorStatus.Up, result.Status);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void Classify_ReturnsDegraded_WhenResponseIsTooSlow()
    {
        var monitor = CreateMonitor(degradedThresholdMs: 300);
        var result = _classifier.Classify(monitor, new HealthProbeResult(true, 200, 350, "ok", null));

        Assert.Equal(MonitorStatus.Degraded, result.Status);
    }

    [Fact]
    public void Classify_ReturnsError_WhenStatusCodeDoesNotMatch()
    {
        var monitor = CreateMonitor(expectedStatusCode: 201);
        var result = _classifier.Classify(monitor, new HealthProbeResult(true, 500, 120, "ok", null));

        Assert.Equal(MonitorStatus.Error, result.Status);
        Assert.Contains("Expected HTTP 201", result.ErrorMessage);
    }

    [Fact]
    public void Classify_ReturnsError_WhenExpectedKeywordIsMissing()
    {
        var monitor = CreateMonitor(expectedKeyword: "healthy");
        var result = _classifier.Classify(monitor, new HealthProbeResult(true, 200, 120, "not ready", null));

        Assert.Equal(MonitorStatus.Error, result.Status);
        Assert.Contains("healthy", result.ErrorMessage);
    }

    [Theory]
    [InlineData("Request timed out.")]
    [InlineData("Name or service not known.")]
    [InlineData("SSL connection could not be established.")]
    public void Classify_ReturnsDown_WhenResponseWasNotReceived(string error)
    {
        var monitor = CreateMonitor();
        var result = _classifier.Classify(monitor, new HealthProbeResult(false, null, 1000, null, error));

        Assert.Equal(MonitorStatus.Down, result.Status);
        Assert.Equal(error, result.ErrorMessage);
    }

    private static MonitorEntity CreateMonitor(
        int expectedStatusCode = 200,
        int degradedThresholdMs = 500,
        string? expectedKeyword = null)
    {
        return new MonitorEntity
        {
            Name = "API",
            Url = "https://example.com",
            ExpectedStatusCode = expectedStatusCode,
            DegradedThresholdMs = degradedThresholdMs,
            ExpectedKeyword = expectedKeyword
        };
    }
}
