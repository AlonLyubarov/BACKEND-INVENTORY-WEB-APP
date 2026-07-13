namespace AlonProject.Domain.Entities;

/// <summary>
/// A personal to-do item. Each task belongs to exactly one user and is private
/// to that account — tasks are never shared between users.
/// </summary>
public class PersonalTask
{
    public int Id { get; set; }
    public int UserId { get; set; }

    public string Title { get; set; } = null!;
    public string? Notes { get; set; }

    /// <summary>Whether the task has been checked off.</summary>
    public bool IsCompleted { get; set; }

    /// <summary>Optional target date. Null = no deadline.</summary>
    public DateTime? DueDate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Set when the task is marked complete; cleared when re-opened.</summary>
    public DateTime? CompletedAt { get; set; }

    // Navigation property
    public User User { get; set; } = null!;
}
