using System.ComponentModel.DataAnnotations;

namespace PulseCheck.Api.Contracts;

public sealed record RegisterRequest(
    [property: Required(ErrorMessage = "Email is required."), EmailAddress(ErrorMessage = "Enter a valid email address.")] string Email,
    [property: Required(ErrorMessage = "Password is required."), MinLength(8, ErrorMessage = "Use at least 8 characters.")] string Password,
    [property: Required(ErrorMessage = "Workspace name is required."), MaxLength(120)] string WorkspaceName);

public sealed record LoginRequest(
    [property: Required(ErrorMessage = "Email is required."), EmailAddress(ErrorMessage = "Enter a valid email address.")] string Email,
    [property: Required(ErrorMessage = "Password is required.")] string Password);

public sealed record AuthResponse(string Token, UserDto User);

public sealed record UserDto(Guid Id, string Email, string WorkspaceName, string PublicStatusSlug, bool EmailAlertsEnabled, bool IsAdmin);
