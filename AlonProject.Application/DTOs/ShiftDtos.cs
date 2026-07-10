using System.ComponentModel.DataAnnotations;

namespace AlonProject.Application.DTOs;

/// <summary>A work-schedule shift in API responses.</summary>
public class ShiftDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Username { get; set; } = null!;
    public int WarehouseId { get; set; }
    public string WarehouseName { get; set; } = null!;
    public DateTime Date { get; set; }
    /// <summary>"HH:mm"</summary>
    public string StartTime { get; set; } = null!;
    /// <summary>"HH:mm"</summary>
    public string EndTime { get; set; } = null!;
    public string? Notes { get; set; }
}

/// <summary>POST /api/shift — assign a shift to a warehouse team member.</summary>
public class CreateShiftDto
{
    public int UserId { get; set; }
    public int WarehouseId { get; set; }

    [Required(ErrorMessage = "Date is required")]
    public DateTime Date { get; set; }

    [Required(ErrorMessage = "Start time is required")]
    [RegularExpression(@"^([01]\d|2[0-3]):[0-5]\d$", ErrorMessage = "Start time must be HH:mm")]
    public string StartTime { get; set; } = null!;

    [Required(ErrorMessage = "End time is required")]
    [RegularExpression(@"^([01]\d|2[0-3]):[0-5]\d$", ErrorMessage = "End time must be HH:mm")]
    public string EndTime { get; set; } = null!;

    [MaxLength(300, ErrorMessage = "Notes cannot exceed 300 characters")]
    public string? Notes { get; set; }
}
