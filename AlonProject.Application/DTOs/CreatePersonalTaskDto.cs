using System.ComponentModel.DataAnnotations;

namespace AlonProject.Application.DTOs;

/// <summary>
/// DTO for creating a personal task. The owning user is taken from the caller's
/// JWT — never from the request body.
/// </summary>
public class CreatePersonalTaskDto
{
    [Required(ErrorMessage = "Title is required")]
    [MaxLength(200, ErrorMessage = "Title must not exceed 200 characters")]
    public string Title { get; set; } = null!;

    [MaxLength(1000, ErrorMessage = "Notes cannot exceed 1000 characters")]
    public string? Notes { get; set; }

    /// <summary>Optional deadline. Omit for a task with no due date.</summary>
    public DateTime? DueDate { get; set; }
}
