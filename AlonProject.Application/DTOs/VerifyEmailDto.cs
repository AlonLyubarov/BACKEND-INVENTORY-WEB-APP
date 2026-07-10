using System.ComponentModel.DataAnnotations;

namespace AlonProject.Application.DTOs;

/// <summary>
/// DTO for confirming an email address with the one-time token from the link.
/// </summary>
public class VerifyEmailDto
{
    [Required(ErrorMessage = "Token is required")]
    public string Token { get; set; } = null!;
}

/// <summary>
/// DTO for requesting a fresh verification email.
/// </summary>
public class ResendVerificationDto
{
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Must be a valid email address")]
    public string Email { get; set; } = null!;
}
