using PulseCheck.Api.Models;

namespace PulseCheck.Api.Services;

public sealed class AdminAccessService
{
    private readonly HashSet<string> _adminEmails;

    public AdminAccessService(IConfiguration configuration)
    {
        _adminEmails = (configuration["PulseCheck:AdminEmails"] ?? string.Empty)
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public bool IsAdmin(ApplicationUser user)
    {
        return IsAdminEmail(user.Email);
    }

    public bool IsAdminEmail(string? email)
    {
        return !string.IsNullOrWhiteSpace(email) && _adminEmails.Contains(email.Trim());
    }
}
