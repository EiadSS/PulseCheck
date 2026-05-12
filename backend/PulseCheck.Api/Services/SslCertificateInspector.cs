using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using PulseCheck.Api.Models;

namespace PulseCheck.Api.Services;

public sealed record SslCertificateResult(
    SslCertificateStatus Status,
    DateTimeOffset? ExpiresAt,
    int? DaysRemaining,
    string? ErrorMessage);

public sealed class SslCertificateInspector
{
    private const int WarningDays = 14;
    private const int CriticalDays = 7;

    public async Task<SslCertificateResult> InspectAsync(Uri uri, int timeoutSeconds, CancellationToken cancellationToken)
    {
        if (uri.Scheme != Uri.UriSchemeHttps)
        {
            return new SslCertificateResult(SslCertificateStatus.NotApplicable, null, null, null);
        }

        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
            using var client = new TcpClient();
            await client.ConnectAsync(uri.Host, uri.Port > 0 ? uri.Port : 443, linked.Token);

            await using var stream = new SslStream(
                client.GetStream(),
                false,
                (_, _, _, _) => true);

            await stream.AuthenticateAsClientAsync(uri.Host);

            if (stream.RemoteCertificate is null)
            {
                return new SslCertificateResult(SslCertificateStatus.Unavailable, null, null, "Server did not provide a certificate.");
            }

            using var certificate = new X509Certificate2(stream.RemoteCertificate);
            return Classify(certificate.NotAfter.ToUniversalTime(), DateTimeOffset.UtcNow);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new SslCertificateResult(SslCertificateStatus.Unavailable, null, null, ex.Message);
        }
    }

    public static SslCertificateResult Classify(DateTimeOffset expiresAt, DateTimeOffset now)
    {
        var daysRemaining = (int)Math.Floor((expiresAt - now).TotalDays);

        if (expiresAt <= now)
        {
            return new SslCertificateResult(SslCertificateStatus.Expired, expiresAt, daysRemaining, "SSL certificate has expired.");
        }

        if (daysRemaining <= CriticalDays)
        {
            return new SslCertificateResult(SslCertificateStatus.Critical, expiresAt, daysRemaining, "SSL certificate expires soon.");
        }

        if (daysRemaining <= WarningDays)
        {
            return new SslCertificateResult(SslCertificateStatus.ExpiringSoon, expiresAt, daysRemaining, "SSL certificate is nearing expiry.");
        }

        return new SslCertificateResult(SslCertificateStatus.Valid, expiresAt, daysRemaining, null);
    }
}
