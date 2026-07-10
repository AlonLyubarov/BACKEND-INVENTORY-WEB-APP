namespace AlonProject.Domain.Entities;

/// <summary>
/// A personal calendar reminder. Each reminder belongs to exactly one user
/// and is pinned to a calendar date.
/// </summary>
public class Reminder
{
    public int Id { get; set; }
    public int UserId { get; set; }

    /// <summary>The calendar day the reminder is for (time component unused).</summary>
    public DateTime Date { get; set; }

    public string Title { get; set; } = null!;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public User User { get; set; } = null!;
}
