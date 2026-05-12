namespace PulseCheck.Api.Models;

public enum NotificationType
{
    MonitorFailed = 0,
    MonitorRecovered = 1,
    SslCertificateWarning = 2,
    SslCertificateCritical = 3
}
