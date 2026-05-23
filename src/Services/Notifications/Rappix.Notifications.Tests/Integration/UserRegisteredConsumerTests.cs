using FluentAssertions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Rappix.Contracts.Identity;
using Rappix.Notifications.Domain.UserContacts;
using Rappix.Notifications.Infrastructure.Messaging;
using Rappix.Notifications.Tests.Integration._Shared;

namespace Rappix.Notifications.Tests.Integration;

/// <summary>
/// Pruebas integration del <see cref="UserRegisteredConsumer"/>: verifica que publicar el evento via
/// el harness crea (o actualiza) la proyeccion <see cref="UserContact"/> con los campos correctos.
/// </summary>
public sealed class UserRegisteredConsumerTests : NotificationsConsumerTestBase
{
    protected override void ConfigureHarness(IBusRegistrationConfigurator configurator) =>
        configurator.AddConsumer<UserRegisteredConsumer>();

    [Fact]
    public async Task Consume_NewUser_CreatesUserContact_WithMerchantUserType()
    {
        var message = NewUserRegisteredEvent(userType: "Merchant", emailConfirmed: false);

        await Harness.Bus.Publish(message);
        (await Harness.GetConsumerHarness<UserRegisteredConsumer>()
            .Consumed.Any<UserRegisteredIntegrationEvent>(filter => filter.Context.Message.UserId == message.UserId))
            .Should().BeTrue();

        (Microsoft.Extensions.DependencyInjection.IServiceScope scope, var db) = CreateDbScope();
        using (scope)
        {
            UserContact? contact = await db.UserContacts.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == message.UserId);

            contact.Should().NotBeNull();
            contact!.Email.Should().Be(message.Email);
            contact.FirstName.Should().Be(message.FirstName);
            contact.LastName.Should().Be(message.LastName);
            contact.UserType.Should().Be(UserType.Merchant);
            contact.EmailConfirmed.Should().BeFalse();
        }
    }

    [Fact]
    public async Task Consume_ExistingUser_UpdatesAllFields()
    {
        Guid userId = Guid.CreateVersion7();
        var first = NewUserRegisteredEvent(userType: "Customer", emailConfirmed: false) with
        {
            UserId = userId,
            Email = "old@test.local",
            FirstName = "Old",
            LastName = "Name",
        };
        var second = NewUserRegisteredEvent(userType: "Courier", emailConfirmed: true) with
        {
            UserId = userId,
            Email = "new@test.local",
            FirstName = "Nuevo",
            LastName = "Apellido",
        };

        await Harness.Bus.Publish(first);
        await Harness.GetConsumerHarness<UserRegisteredConsumer>()
            .Consumed.Any<UserRegisteredIntegrationEvent>(filter => filter.Context.Message.UserId == userId && filter.Context.Message.Email == "old@test.local");

        await Harness.Bus.Publish(second);
        await Harness.GetConsumerHarness<UserRegisteredConsumer>()
            .Consumed.Any<UserRegisteredIntegrationEvent>(filter => filter.Context.Message.UserId == userId && filter.Context.Message.Email == "new@test.local");

        (Microsoft.Extensions.DependencyInjection.IServiceScope scope, var db) = CreateDbScope();
        using (scope)
        {
            UserContact? contact = await db.UserContacts.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == userId);

            contact.Should().NotBeNull();
            contact!.Email.Should().Be("new@test.local");
            contact.FirstName.Should().Be("Nuevo");
            contact.LastName.Should().Be("Apellido");
            contact.UserType.Should().Be(UserType.Courier);
            contact.EmailConfirmed.Should().BeTrue();

            int count = await db.UserContacts.AsNoTracking().CountAsync(c => c.Id == userId);
            count.Should().Be(1);
        }
    }

    [Fact]
    public async Task Consume_WithUnknownUserType_FallsBackToCustomer()
    {
        var message = NewUserRegisteredEvent(userType: "SuperAdminGodMode", emailConfirmed: false);

        await Harness.Bus.Publish(message);
        await Harness.GetConsumerHarness<UserRegisteredConsumer>()
            .Consumed.Any<UserRegisteredIntegrationEvent>(filter => filter.Context.Message.UserId == message.UserId);

        (Microsoft.Extensions.DependencyInjection.IServiceScope scope, var db) = CreateDbScope();
        using (scope)
        {
            UserContact? contact = await db.UserContacts.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == message.UserId);

            contact.Should().NotBeNull();
            // Fallback documentado en el consumer: valor desconocido -> Customer + Log Warning.
            contact!.UserType.Should().Be(UserType.Customer);
        }
    }

    private static UserRegisteredIntegrationEvent NewUserRegisteredEvent(string userType, bool emailConfirmed) => new()
    {
        UserId = Guid.CreateVersion7(),
        Email = $"user-{Guid.NewGuid():N}@test.local",
        FirstName = "Juan",
        LastName = "Perez",
        UserType = userType,
        EmailConfirmed = emailConfirmed,
    };
}
