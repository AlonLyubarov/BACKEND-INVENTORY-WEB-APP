using System.ComponentModel.DataAnnotations;

namespace AlonProject.Application.DTOs;

/// <summary>
/// DTO for editing a personal task — used both to change its fields and to
/// check/uncheck it. All fields are sent; the server replaces the task's state.
/// </summary>
public class UpdatePersonalTaskDto
{
    [Required(ErrorMessage = "Title is required")]
    [MaxLength(200, ErrorMessage = "Title must not exceed 200 characters")]
    public string Title { get; set; } = null!;

    [MaxLength(1000, ErrorMessage = "Notes cannot exceed 1000 characters")]
    public string? Notes { get; set; }

    public bool IsCompleted { get; set; }

    public DateTime? DueDate { get; set; }
}
