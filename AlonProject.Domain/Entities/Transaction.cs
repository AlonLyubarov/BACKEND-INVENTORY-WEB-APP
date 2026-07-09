using AlonProject.Domain.Enums;

namespace AlonProject.Domain.Entities;

public class Transaction
{
    public int Id { get; set; }
    public int ItemId { get; set; }
    public TransactionType Type { get; set; }
    public int Quantity { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public Item Item { get; set; } = null!;
}
