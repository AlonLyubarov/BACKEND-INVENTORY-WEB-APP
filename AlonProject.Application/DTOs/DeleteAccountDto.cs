using System.ComponentModel.DataAnnotations;

namespace AlonProject.Application.DTOs;

/// <summary>
/// Self-service account deletion request. The caller's identity comes from
/// the JWT; the password re-confirmation proves account ownership.
/// </summary>
public class DeleteAccountDto
{
    [Required(ErrorMessage = "Password is required")]
    public string Password { get; set; } = null!;
}
