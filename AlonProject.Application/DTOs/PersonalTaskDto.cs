namespace AlonProject.Application.DTOs;

/// <summary>
/// DTO representing a personal task in API responses.
/// </summary>
public class PersonalTaskDto
{
    public int Id { get; set; }
    public string Title { get; set; } = null!;
    public string? Notes { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
