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
}
