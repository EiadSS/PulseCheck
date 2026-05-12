using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using PulseCheck.Api.Contracts;
using PulseCheck.Api.Models;

namespace PulseCheck.Api.Services;

public sealed class JwtTokenService
{
    private readonly IConfiguration _configuration;
    private readonly AdminAccessService _adminAccess;

    public JwtTokenService(IConfiguration configuration, AdminAccessService adminAccess)
    {
        _configuration = configuration;
        _adminAccess = adminAccess;
    }

    public AuthResponse CreateToken(ApplicationUser user)
    {
        var issuer = _configuration["Jwt:Issuer"] ?? "PulseCheck";
        var audience = _configuration["Jwt:Audience"] ?? "PulseCheck";
        var secret = _configuration["Jwt:Secret"] ?? throw new InvalidOperationException("JWT secret is not configured.");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new Claim("workspace", user.WorkspaceName)
        };

        var token = new JwtSecurityToken(
            issuer,
            audience,
            claims,
            expires: DateTime.UtcNow.AddHours(12),
            signingCredentials: credentials);

        return new AuthResponse(
            new JwtSecurityTokenHandler().WriteToken(token),
            new UserDto(
                user.Id,
                user.Email ?? string.Empty,
                user.WorkspaceName,
                user.PublicStatusSlug,
                user.EmailAlertsEnabled,
                _adminAccess.IsAdmin(user)));
    }
}
