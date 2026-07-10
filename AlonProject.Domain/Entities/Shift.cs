namespace AlonProject.Domain.Entities;

/// <summary>
/// A work-schedule shift: one user working at one warehouse on a given day
/// between StartTime and EndTime. Assigned by the warehouse owner; visible
/// to the assigned user in their own schedule.
/// </summary>
public class Shift
{
    public int Id { get; set; }

    /// <summary>The assigned team member (Employee/ShiftManager of the warehouse).</summary>
    public int UserId { get; set; }

    /// <summary>The main warehouse the shift belongs to.</summary>
    public int WarehouseId { get; set; }

    /// <summary>The calendar day of the shift (time component unused).</summary>
    public DateTime Date { get; set; }

    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }

    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public User User { get; set; } = null!;
    public Warehouse Warehouse { get; set; } = null!;
}
