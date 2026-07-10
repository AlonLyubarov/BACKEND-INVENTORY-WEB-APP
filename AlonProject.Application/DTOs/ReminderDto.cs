namespace AlonProject.Application.DTOs;

/// <summary>
/// DTO representing a calendar reminder in API responses.
/// </summary>
public class ReminderDto
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public string Title { get; set; } = null!;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}
