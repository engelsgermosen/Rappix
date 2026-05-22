using FluentAssertions;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Catalog.Domain;
using Rappix.Catalog.Domain.Items;
using Rappix.Catalog.Domain.Items.Events;

namespace Rappix.Catalog.Tests.Unit;

/// <summary>
/// Pruebas unitarias del patron de reserva (hold) en el agregado StockLevel y del agregado
/// StockReservation: el hold aparta del disponible sin tocar el fisico; el commit descuenta del
/// fisico; el release devuelve al disponible.
/// </summary>
public sealed class StockReservationTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Reserve_ReducesAvailable_NotPhysicalQuantity()
    {
        StockLevel stock = NewStock(10);

        Result result = stock.Reserve(3, Now);

        result.IsSuccess.Should().BeTrue();
        stock.Quantity.Should().Be(10);
        stock.ReservedQuantity.Should().Be(3);
        stock.Available.Should().Be(7);
        stock.IsDepleted.Should().BeFalse();
    }

    [Fact]
    public void Reserve_MoreThanAvailable_Fails()
    {
        StockLevel stock = NewStock(5);
        stock.Reserve(4, Now);

        Result result = stock.Reserve(2, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        stock.ReservedQuantity.Should().Be(4);
        stock.Available.Should().Be(1);
    }

    [Fact]
    public void Reserve_NonPositive_Fails()
    {
        StockLevel stock = NewStock(5);

        stock.Reserve(0, Now).Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public void ReleaseReservation_ReturnsUnitsToAvailable()
    {
        StockLevel stock = NewStock(10);
        stock.Reserve(4, Now);

        Result result = stock.ReleaseReservation(4, Now);

        result.IsSuccess.Should().BeTrue();
        stock.ReservedQuantity.Should().Be(0);
        stock.Available.Should().Be(10);
        stock.Quantity.Should().Be(10);
    }

    [Fact]
    public void ReleaseReservation_MoreThanReserved_Fails()
    {
        StockLevel stock = NewStock(10);
        stock.Reserve(2, Now);

        Result result = stock.ReleaseReservation(3, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(StockErrors.InvalidReservation.Code);
    }

    [Fact]
    public void CommitReservation_DecrementsPhysicalAndReserved()
    {
        StockLevel stock = NewStock(10);
        stock.Reserve(4, Now);

        Result result = stock.CommitReservation(4, Now);

        result.IsSuccess.Should().BeTrue();
        stock.Quantity.Should().Be(6);
        stock.ReservedQuantity.Should().Be(0);
        stock.Available.Should().Be(6);
    }

    [Fact]
    public void CommitReservation_ToZeroPhysical_RaisesStockDepleted()
    {
        StockLevel stock = NewStock(2);
        stock.Reserve(2, Now);

        stock.CommitReservation(2, Now);

        stock.Quantity.Should().Be(0);
        stock.IsDepleted.Should().BeTrue();
        stock.DomainEvents.Should().ContainSingle(domainEvent => domainEvent is StockDepletedDomainEvent);
    }

    [Fact]
    public void CommitReservation_MoreThanReserved_Fails()
    {
        StockLevel stock = NewStock(10);
        stock.Reserve(2, Now);

        stock.CommitReservation(3, Now).Error.Code.Should().Be(StockErrors.InvalidReservation.Code);
    }

    [Fact]
    public void Reservation_Create_IsHeld_AndExpiresAfterTtl()
    {
        StockReservation reservation = StockReservation.Create(Guid.CreateVersion7(), ItemId.New(), Guid.CreateVersion7(), 2, Now.AddMinutes(30), Now);

        reservation.Status.Should().Be(StockReservationStatus.Held);
        reservation.IsHeld.Should().BeTrue();
        reservation.IsExpired(Now.AddMinutes(10)).Should().BeFalse();
        reservation.IsExpired(Now.AddMinutes(31)).Should().BeTrue();
    }

    [Fact]
    public void Reservation_Commit_IsIdempotent_AndBlocksLaterRelease()
    {
        StockReservation reservation = StockReservation.Create(Guid.CreateVersion7(), ItemId.New(), Guid.CreateVersion7(), 2, Now.AddMinutes(30), Now);

        reservation.Commit(Now);
        reservation.Commit(Now); // idempotente
        reservation.Release(Now); // no revierte un commit

        reservation.Status.Should().Be(StockReservationStatus.Committed);
        reservation.IsExpired(Now.AddMinutes(31)).Should().BeFalse();
    }

    [Fact]
    public void Reservation_Release_IsIdempotent()
    {
        StockReservation reservation = StockReservation.Create(Guid.CreateVersion7(), ItemId.New(), Guid.CreateVersion7(), 2, Now.AddMinutes(30), Now);

        reservation.Release(Now);
        reservation.Release(Now);

        reservation.Status.Should().Be(StockReservationStatus.Released);
    }

    private static StockLevel NewStock(int quantity) =>
        StockLevel.Create(ItemId.New(), Guid.CreateVersion7(), quantity, Now).Value;
}
