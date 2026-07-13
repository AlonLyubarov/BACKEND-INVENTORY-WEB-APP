using AlonProject.Application.DTOs;
using AlonProject.Application.Services;
using AlonProject.Domain.Entities;
using AlonProject.Domain.Enums;
using AlonProject.Domain.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace AlonProject.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="TransactionService"/>. The backend treats the
/// transaction Quantity as a SIGNED delta applied to the item's stock (the
/// frontend negates outbound types before sending). These tests lock that
/// contract plus the stock-availability and existence guards.
/// </summary>
public class TransactionServiceTests
{
    private readonly ITransactionRepository _txRepo = Substitute.For<ITransactionRepository>();
    private readonly IItemRepository _itemRepo = Substitute.For<IItemRepository>();
    private readonly IWarehouseRepository _whRepo = Substitute.For<IWarehouseRepository>();

    private TransactionService NewService() =>
        new(_txRepo, _itemRepo, _whRepo, NullLogger<TransactionService>.Instance);

    private void ArrangeItem(int id, int quantity)
    {
        _itemRepo.GetByIdAsync(id).Returns(new Item { Id = id, Quantity = quantity, WarehouseId = 1 });
        _txRepo.CreateAsync(Arg.Any<Transaction>()).Returns(ci =>
        {
            var t = ci.Arg<Transaction>()!;
            t.Id = 999;
            return t;
        });
    }

    [Fact]
    public async Task CreateAsync_PositiveQuantity_IncreasesStock()
    {
        ArrangeItem(id: 5, quantity: 10);

        await NewService().CreateAsync(new CreateTransactionDto
        {
            ItemId = 5, Type = TransactionType.StockIn, Quantity = 7
        });

        // 10 + 7 = 17
        await _itemRepo.Received(1).UpdateAsync(Arg.Is<Item>(i => i!.Quantity == 17));
    }

    [Fact]
    public async Task CreateAsync_NegativeQuantity_DecreasesStock()
    {
        ArrangeItem(id: 5, quantity: 10);

        // Outbound movement arrives already negated (StockOut of 4 → -4)
        await NewService().CreateAsync(new CreateTransactionDto
        {
            ItemId = 5, Type = TransactionType.StockOut, Quantity = -4
        });

        // 10 + (-4) = 6
        await _itemRepo.Received(1).UpdateAsync(Arg.Is<Item>(i => i!.Quantity == 6));
    }

    [Fact]
    public async Task CreateAsync_PersistsTransactionRecord_WithGivenValues()
    {
        ArrangeItem(id: 5, quantity: 10);

        var dto = await NewService().CreateAsync(new CreateTransactionDto
        {
            ItemId = 5, Type = TransactionType.Sale, Quantity = -2, Notes = "sold 2"
        });

        await _txRepo.Received(1).CreateAsync(Arg.Is<Transaction>(t =>
            t!.ItemId == 5 && t.Type == TransactionType.Sale && t.Quantity == -2 && t.Notes == "sold 2"));
        Assert.Equal(999, dto.Id);
    }

    [Fact]
    public async Task CreateAsync_ItemNotFound_ThrowsKeyNotFound()
    {
        _itemRepo.GetByIdAsync(404).Returns((Item?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            NewService().CreateAsync(new CreateTransactionDto
            {
                ItemId = 404, Type = TransactionType.StockIn, Quantity = 1
            }));

        await _txRepo.DidNotReceive().CreateAsync(Arg.Any<Transaction>());
    }

    [Fact]
    public async Task CreateAsync_InsufficientStock_ThrowsAndDoesNotPersist()
    {
        ArrangeItem(id: 5, quantity: 3);

        // Would drive stock to -7 → rejected
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            NewService().CreateAsync(new CreateTransactionDto
            {
                ItemId = 5, Type = TransactionType.StockOut, Quantity = -10
            }));

        Assert.Contains("Insufficient stock", ex.Message);
        await _txRepo.DidNotReceive().CreateAsync(Arg.Any<Transaction>());
        await _itemRepo.DidNotReceive().UpdateAsync(Arg.Any<Item>());
    }

    [Fact]
    public async Task CreateAsync_ExactlyToZero_IsAllowed()
    {
        ArrangeItem(id: 5, quantity: 4);

        await NewService().CreateAsync(new CreateTransactionDto
        {
            ItemId = 5, Type = TransactionType.StockOut, Quantity = -4
        });

        await _itemRepo.Received(1).UpdateAsync(Arg.Is<Item>(i => i!.Quantity == 0));
    }

    [Fact]
    public async Task GetByIdAsync_MapsProductNameAndSku_FromCatalog()
    {
        _txRepo.GetByIdAsync(1).Returns(new Transaction
        {
            Id = 1,
            ItemId = 5,
            Type = TransactionType.StockIn,
            Quantity = 3,
            Item = new Item
            {
                Id = 5,
                ProductCatalog = new ProductCatalog { Sku = "SKU-001", Name = "Blue Widget" }
            }
        });

        var dto = await NewService().GetByIdAsync(1);

        Assert.NotNull(dto);
        Assert.Equal("SKU-001", dto!.ProductSku);
        Assert.Equal("Blue Widget", dto.ProductName);
    }

    [Fact]
    public async Task GetByIdAsync_NotFound_ReturnsNull()
    {
        _txRepo.GetByIdAsync(404).Returns((Transaction?)null);
        Assert.Null(await NewService().GetByIdAsync(404));
    }
}
