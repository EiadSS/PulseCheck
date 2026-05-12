using PulseCheck.Api.Models;
using PulseCheck.Api.Services;
using Xunit;

namespace PulseCheck.Tests;

public sealed class SslCertificateInspectorTests
{
    [Fact]
    public void Classify_ReturnsExpiringSoon_WhenCertificateExpiresWithinFourteenDays()
    {
        var now = DateTimeOffset.UtcNow;
        var result = SslCertificateInspector.Classify(now.AddDays(10), now);

        Assert.Equal(SslCertificateStatus.ExpiringSoon, result.Status);
        Assert.Equal(10, result.DaysRemaining);
    }

    [Fact]
    public void Classify_ReturnsCritical_WhenCertificateExpiresWithinSevenDays()
    {
        var now = DateTimeOffset.UtcNow;
        var result = SslCertificateInspector.Classify(now.AddDays(4), now);

        Assert.Equal(SslCertificateStatus.Critical, result.Status);
    }

    [Fact]
    public void Classify_ReturnsExpired_WhenCertificateIsPastExpiry()
    {
        var now = DateTimeOffset.UtcNow;
        var result = SslCertificateInspector.Classify(now.AddDays(-1), now);

        Assert.Equal(SslCertificateStatus.Expired, result.Status);
    }
}
