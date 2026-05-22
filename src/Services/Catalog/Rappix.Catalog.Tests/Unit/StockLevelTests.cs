using FluentAssertions;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Catalog.Domain.Items;
using Rappix.Catalog.Domain.Items.Events;

namespace Rappix.Catalog.Tests.Unit;

/// <summary>Pruebas unitarias del agregado StockLevel (decrementos, agotamiento, reposicion).</summary>
public sealed class StockLevelTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_WithNegativeQuantity_Fails()
    {
        Result<StockLevel> result = StockLevel.Create(ItemId.New(), Guid.CreateVersion7(), -1, Now);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Decrement_ReducesQuantity()
    {
        StockLevel stock = NewStock(10);

        Result result = stock.Decrement(3, Now);

        result.IsSuccess.Should().BeTrue();
        stock.Quantity.Should().Be(7);
    }

    [Fact]
    public void Decrement_MoreThanAvailable_Fails()
    {
        StockLevel stock = NewStock(2);

        Result result = stock.Decrement(3, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        stock.Quantity.Should().Be(2);
    }

    [Fact]
    public void Decrement_NonPositive_Fails()
    {
        StockLevel stock = NewStock(5);

        Result result = stock.Decrement(0, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public void Decrement_ToZero_RaisesStockDepleted()
    {
        StockLevel stock = NewStock(1);

        stock.Decrement(1, Now);

        stock.Quantity.Should().Be(0);
        stock.IsDepleted.Should().BeTrue();
        stock.DomainEvents.Should().ContainSingle(domainEvent => domainEvent is StockDepletedDomainEvent);
    }

    [Fact]
    public void Restock_IncreasesQuantity()
    {
        StockLevel stock = NewStock(1);

        stock.Restock(4, Now);

        stock.Quantity.Should().Be(5);
    }

    [Fact]
    public void SetQuantity_ToZeroFromPositive_RaisesStockDepleted()
    {
        StockLevel stock = NewStock(5);

        stock.SetQuantity(0, Now);

        stock.IsDepleted.Should().BeTrue();
        stock.DomainEvents.Should().ContainSingle(domainEvent => domainEvent is StockDepletedDomainEvent);
    }

    private static StockLevel NewStock(int quantity) =>
        StockLevel.Create(ItemId.New(), Guid.CreateVersion7(), quantity, Now).Value;
}
