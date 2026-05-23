using FluentAssertions;
using Rappix.Notifications.Domain.MerchantContacts;
using Rappix.Notifications.Domain.NotificationOrders;
using Rappix.Notifications.Domain.UserContacts;

namespace Rappix.Notifications.Tests.Unit;

/// <summary>
/// Pruebas de los entities de proyeccion (read-models simples sin maquina de estados rica). Cubre
/// Create + Update upsert (UserContact, MerchantContact) e idempotencia de SetCourier
/// (NotificationOrder). La logica compleja vive en los consumers (testeados en integration).
/// </summary>
public sealed class ProjectionEntitiesTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Later = Now.AddMinutes(10);

    // ----------------- UserContact -----------------

    [Fact]
    public void UserContact_Create_PopulatesAllFields_AndDerivesFullName()
    {
        Guid userId = Guid.CreateVersion7();

        UserContact contact = UserContact.Create(
            userId: userId,
            email: "juan.perez@test.local",
            firstName: "Juan",
            lastName: "Perez",
            userType: UserType.Customer,
            emailConfirmed: false,
            utcNow: Now);

        contact.Id.Should().Be(userId);
        contact.Email.Should().Be("juan.perez@test.local");
        contact.FirstName.Should().Be("Juan");
        contact.LastName.Should().Be("Perez");
        contact.UserType.Should().Be(UserType.Customer);
        contact.EmailConfirmed.Should().BeFalse();
        contact.UpdatedAtUtc.Should().Be(Now);
        contact.FullName.Should().Be("Juan Perez");
    }

    [Fact]
    public void UserContact_Update_OverwritesAllFields_AndBumpsTimestamp()
    {
        UserContact contact = UserContact.Create(
            userId: Guid.CreateVersion7(),
            email: "old@test.local",
            firstName: "Old",
            lastName: "Name",
            userType: UserType.Customer,
            emailConfirmed: false,
            utcNow: Now);

        contact.Update(
            email: "new@test.local",
            firstName: "Nuevo",
            lastName: "Apellido",
            userType: UserType.Merchant,
            emailConfirmed: true,
            utcNow: Later);

        contact.Email.Should().Be("new@test.local");
        contact.FirstName.Should().Be("Nuevo");
        contact.LastName.Should().Be("Apellido");
        contact.UserType.Should().Be(UserType.Merchant);
        contact.EmailConfirmed.Should().BeTrue();
        contact.UpdatedAtUtc.Should().Be(Later);
    }

    [Fact]
    public void UserContact_ConfirmEmail_FromFalse_SetsTrue_AndBumpsTimestamp()
    {
        UserContact contact = UserContact.Create(
            userId: Guid.CreateVersion7(),
            email: "x@test.local",
            firstName: "X",
            lastName: "Y",
            userType: UserType.Customer,
            emailConfirmed: false,
            utcNow: Now);

        contact.ConfirmEmail(Later);

        contact.EmailConfirmed.Should().BeTrue();
        contact.UpdatedAtUtc.Should().Be(Later);
    }

    [Fact]
    public void UserContact_ConfirmEmail_FromTrue_IsIdempotentNoOp()
    {
        UserContact contact = UserContact.Create(
            userId: Guid.CreateVersion7(),
            email: "x@test.local",
            firstName: "X",
            lastName: "Y",
            userType: UserType.Customer,
            emailConfirmed: true,
            utcNow: Now);

        contact.ConfirmEmail(Later);

        contact.EmailConfirmed.Should().BeTrue();
        // Idempotente: no se reescribe el timestamp si ya estaba confirmado.
        contact.UpdatedAtUtc.Should().Be(Now);
    }

    // ----------------- MerchantContact -----------------

    [Fact]
    public void MerchantContact_Create_SetsOwnerAndTimestamp()
    {
        Guid merchantId = Guid.CreateVersion7();
        Guid ownerUserId = Guid.CreateVersion7();

        MerchantContact contact = MerchantContact.Create(merchantId, ownerUserId, Now);

        contact.Id.Should().Be(merchantId);
        contact.OwnerUserId.Should().Be(ownerUserId);
        contact.UpdatedAtUtc.Should().Be(Now);
    }

    [Fact]
    public void MerchantContact_Update_SameOwner_IsIdempotentNoOp()
    {
        Guid owner = Guid.CreateVersion7();
        MerchantContact contact = MerchantContact.Create(Guid.CreateVersion7(), owner, Now);

        contact.Update(owner, Later);

        contact.OwnerUserId.Should().Be(owner);
        // Idempotente: no bump si el owner no cambia.
        contact.UpdatedAtUtc.Should().Be(Now);
    }

    [Fact]
    public void MerchantContact_Update_DifferentOwner_SwitchesAndBumps()
    {
        Guid originalOwner = Guid.CreateVersion7();
        Guid newOwner = Guid.CreateVersion7();
        MerchantContact contact = MerchantContact.Create(Guid.CreateVersion7(), originalOwner, Now);

        contact.Update(newOwner, Later);

        contact.OwnerUserId.Should().Be(newOwner);
        contact.UpdatedAtUtc.Should().Be(Later);
    }

    // ----------------- NotificationOrder -----------------

    [Fact]
    public void NotificationOrder_Create_PopulatesAllFields_CourierIsNull()
    {
        Guid orderId = Guid.CreateVersion7();
        Guid customer = Guid.CreateVersion7();
        Guid merchant = Guid.CreateVersion7();

        NotificationOrder order = NotificationOrder.Create(orderId, customer, merchant, Now);

        order.Id.Should().Be(orderId);
        order.CustomerUserId.Should().Be(customer);
        order.MerchantId.Should().Be(merchant);
        order.CourierUserId.Should().BeNull();
        order.CreatedAtUtc.Should().Be(Now);
        order.UpdatedAtUtc.Should().Be(Now);
    }

    [Fact]
    public void NotificationOrder_SetCourier_FromNull_SetsAndBumps()
    {
        Guid courier = Guid.CreateVersion7();
        NotificationOrder order = NotificationOrder.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Now);

        order.SetCourier(courier, Later);

        order.CourierUserId.Should().Be(courier);
        order.UpdatedAtUtc.Should().Be(Later);
    }

    [Fact]
    public void NotificationOrder_SetCourier_SameCourier_IsIdempotentNoOp()
    {
        Guid courier = Guid.CreateVersion7();
        NotificationOrder order = NotificationOrder.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Now);
        order.SetCourier(courier, Now);

        order.SetCourier(courier, Later);

        order.CourierUserId.Should().Be(courier);
        order.UpdatedAtUtc.Should().Be(Now);
    }

    [Fact]
    public void NotificationOrder_SetCourier_DifferentCourier_Overwrites()
    {
        Guid first = Guid.CreateVersion7();
        Guid second = Guid.CreateVersion7();
        NotificationOrder order = NotificationOrder.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Now);
        order.SetCourier(first, Now);

        order.SetCourier(second, Later);

        order.CourierUserId.Should().Be(second);
        order.UpdatedAtUtc.Should().Be(Later);
    }
}
