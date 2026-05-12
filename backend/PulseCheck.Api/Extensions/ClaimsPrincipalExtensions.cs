using System.Security.Claims;

namespace PulseCheck.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var rawId = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue("sub");

        return Guid.TryParse(rawId, out var userId)
            ? userId
            : throw new InvalidOperationException("Authenticated user id is missing.");
    }
}
