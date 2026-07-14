using System.ComponentModel.DataAnnotations;

namespace AlonProject.Application.DTOs;

/// <summary>
/// Body for POST /api/auth/refresh and /api/auth/logout — carries the raw
/// refresh token the client received at login.
/// </summary>
public class RefreshTokenRequestDto
{
    [Required(ErrorMessage = "Refresh token is required")]
    public string RefreshToken { get; set; } = null!;
}
