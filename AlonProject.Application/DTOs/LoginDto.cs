using System.ComponentModel.DataAnnotations;

namespace AlonProject.Application.DTOs;

/// <summary>
/// DTO for user login requests.
/// Frontend submits username and password to authenticate.
/// </summary>
public class LoginDto
{
    /// <summary>
    /// Username to authenticate.
    /// </summary>
    [Required(ErrorMessage = "Username is required")]
    public string Username { get; set; } = null!;

    /// <summary>
    /// User's password. Transmitted over HTTPS only.
    /// Backend will verify against stored BCrypt hash.
    /// </summary>
    [Required(ErrorMessage = "Password is required")]
    public string Password { get; set; } = null!;
}
