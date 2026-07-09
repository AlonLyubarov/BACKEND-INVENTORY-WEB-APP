using System.ComponentModel.DataAnnotations;
using AlonProject.Domain.Enums;

namespace AlonProject.Application.DTOs;

public class CreateTransactionDto
{
    /// <summary>
    /// Item being transacted.
    /// </summary>
    [Required(ErrorMessage = "ItemId is required")]
    public int ItemId { get; set; }

    /// <summary>
    /// Type of transaction (StockIn, StockOut, Adjustment).
    /// </summary>
    public TransactionType Type { get; set; }

    /// <summary>
    /// Quantity change - can be negative for StockOut (withdrawal).
    /// Positive: StockIn (receipt), Negative: StockOut (withdrawal).
    /// No strict range validation as semantics depend on transaction type.
    /// Validation logic should be applied at service layer based on current inventory.
    /// </summary>
    public int Quantity { get; set; }

    /// <summary>
    /// Optional notes/reason for the transaction.
    /// </summary>
    [MaxLength(500, ErrorMessage = "Notes cannot exceed 500 characters")]
    public string? Notes { get; set; }
}
