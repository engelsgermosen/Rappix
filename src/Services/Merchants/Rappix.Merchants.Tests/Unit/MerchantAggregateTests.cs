using FluentAssertions;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Merchants.Application.Geo;
using Rappix.Merchants.Domain.Merchants;
using Rappix.Merchants.Domain.Merchants.Events;

namespace Rappix.Merchants.Tests.Unit;

/// <summary>Pruebas unitarias de la maquina de estados del agregado Merchant.</summary>
public sealed class MerchantAggregateTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void CreateDraft_StartsInDraft_WithoutDomainEvents()
    {
        Merchant merchant = CreateDraft();

        merchant.Status.Should().Be(MerchantStatus.Draft);
        merchant.IsActive.Should().BeFalse();
        merchant.CommissionPercentage.Value.Should().Be(15.00m);
        merchant.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void SubmitForApproval_WhenIncomplete_Fails()
    {
        Merchant merchant = CreateDraft();

        Result result = merchant.SubmitForApproval(Now);

        result.IsFailure.Should().BeTrue();
        merchant.Status.Should().Be(MerchantStatus.Draft);
    }

    [Fact]
    public void SubmitForApproval_WhenComplete_MovesToPending()
    {
        Merchant merchant = CreateComplete();

        Result result = merchant.SubmitForApproval(Now);

        result.IsSuccess.Should().BeTrue();
        merchant.Status.Should().Be(MerchantStatus.Pending);
    }

    [Fact]
    public void SubmitForApproval_WithoutPickupLocation_Fails()
    {
        // CreateComplete sin SetPickupLocation: aunque tenga Rnc + ServiceArea + horarios, debe fallar.
        Merchant merchant = CreateDraft();
        merchant.UpdateProfile("Tienda Lulu", Slug.FromTrusted("tienda-lulu"), Rnc.Create("131246803").Value, "Descripcion", VerticalType.Food, Now);
        merchant.AddCircleServiceArea(GeoFactory.CreatePoint(18.4861, -69.9312), 2000, Now);
        merchant.ReplaceOperatingHours([new OperatingHoursRange(DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(18, 0))], Now);
        // Falta SetPickupLocation a proposito.

        Result result = merchant.SubmitForApproval(Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Merchants.Merchant.IncompleteForSubmission");
        merchant.Status.Should().Be(MerchantStatus.Draft);
    }

    [Fact]
    public void SetPickupLocation_WithValidPoint_Persists()
    {
        Merchant merchant = CreateDraft();

        Result result = merchant.SetPickupLocation(GeoFactory.CreatePoint(18.4861, -69.9312), Now);

        result.IsSuccess.Should().BeTrue();
        merchant.PickupLocation.Should().NotBeNull();
        merchant.PickupLocation!.Y.Should().BeApproximately(18.4861, 0.0001);  // lat = Y
        merchant.PickupLocation!.X.Should().BeApproximately(-69.9312, 0.0001); // lng = X
    }

    [Fact]
    public void SetPickupLocation_WhenSuspended_Fails()
    {
        Merchant merchant = Approved();
        merchant.Suspend("Abuso", Now);

        Result result = merchant.SetPickupLocation(GeoFactory.CreatePoint(18.4861, -69.9312), Now);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Approve_FromPending_ActivatesAndRaisesEvent()
    {
        Merchant merchant = CreateComplete();
        merchant.SubmitForApproval(Now);

        Result result = merchant.Approve(Now);

        result.IsSuccess.Should().BeTrue();
        merchant.Status.Should().Be(MerchantStatus.Active);
        merchant.IsActive.Should().BeTrue();
        merchant.DomainEvents.Should().ContainSingle(domainEvent => domainEvent is MerchantApprovedDomainEvent);
    }

    [Fact]
    public void Approve_FromDraft_Fails()
    {
        Merchant merchant = CreateComplete();

        Result result = merchant.Approve(Now);

        result.IsFailure.Should().BeTrue();
        merchant.Status.Should().Be(MerchantStatus.Draft);
    }

    [Fact]
    public void Reject_FromPending_RejectsAndRaisesEvent()
    {
        Merchant merchant = CreateComplete();
        merchant.SubmitForApproval(Now);

        Result result = merchant.Reject("Documentacion invalida", Now);

        result.IsSuccess.Should().BeTrue();
        merchant.Status.Should().Be(MerchantStatus.Rejected);
        merchant.RejectionReason.Should().Be("Documentacion invalida");
        merchant.DomainEvents.Should().ContainSingle(domainEvent => domainEvent is MerchantRejectedDomainEvent);
    }

    [Fact]
    public void Suspend_FromActive_SuspendsAndRaisesEvent()
    {
        Merchant merchant = Approved();

        Result result = merchant.Suspend("Abuso", Now);

        result.IsSuccess.Should().BeTrue();
        merchant.Status.Should().Be(MerchantStatus.Suspended);
        merchant.DomainEvents.Should().ContainSingle(domainEvent => domainEvent is MerchantSuspendedDomainEvent);
    }

    [Fact]
    public void Unsuspend_FromSuspended_ReactivatesAndRaisesEvent()
    {
        Merchant merchant = Approved();
        merchant.Suspend("Abuso", Now);

        Result result = merchant.Unsuspend(Now);

        result.IsSuccess.Should().BeTrue();
        merchant.Status.Should().Be(MerchantStatus.Active);
        merchant.SuspensionReason.Should().BeNull();
        merchant.DomainEvents.Should().ContainSingle(domainEvent => domainEvent is MerchantActivatedDomainEvent);
    }

    [Fact]
    public void UpdateProfile_WhenSuspended_Fails()
    {
        Merchant merchant = Approved();
        merchant.Suspend("Abuso", Now);

        Result result = merchant.UpdateProfile("Nuevo", merchant.Slug, merchant.Rnc, null, VerticalType.Food, Now);

        result.IsFailure.Should().BeTrue();
    }

    private static Merchant CreateDraft() => Merchant.CreateDraft(
        Guid.CreateVersion7(),
        "Mi negocio",
        Slug.FromTrusted("mi-negocio-prueba"),
        VerticalType.Food,
        CommissionPercentage.Default,
        Now);

    private static Merchant CreateComplete()
    {
        Merchant merchant = CreateDraft();
        merchant.UpdateProfile("Tienda Lulu", Slug.FromTrusted("tienda-lulu"), Rnc.Create("131246803").Value, "Descripcion", VerticalType.Food, Now);
        merchant.AddCircleServiceArea(GeoFactory.CreatePoint(18.4861, -69.9312), 2000, Now);
        merchant.ReplaceOperatingHours([new OperatingHoursRange(DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(18, 0))], Now);
        merchant.SetPickupLocation(GeoFactory.CreatePoint(18.4861, -69.9312), Now);
        return merchant;
    }

    private static Merchant Approved()
    {
        Merchant merchant = CreateComplete();
        merchant.SubmitForApproval(Now);
        merchant.Approve(Now);
        merchant.ClearDomainEvents();
        return merchant;
    }
}
