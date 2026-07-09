using AlonProject.Domain.Enums;

namespace AlonProject.Application.DTOs;

public class TransactionDto
{
    public int Id { get; set; }
    public int ItemId { get; set; }
    public TransactionType Type { get; set; }
    public int Quantity { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Product identification of the transacted item, resolved even when the
    /// item was soft-deleted — the audit trail must say WHAT was moved/removed.
    /// </summary>
    public string? ProductSku { get; set; }
    public string? ProductName { get; set; }
}
