namespace PulseCheck.Api.Models;

public enum SslCertificateStatus
{
    NotApplicable = 0,
    Valid = 1,
    ExpiringSoon = 2,
    Critical = 3,
    Expired = 4,
    Unavailable = 5
}
