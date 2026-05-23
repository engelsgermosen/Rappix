using FluentAssertions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Rappix.Contracts.Merchants;
using Rappix.Notifications.Domain.MerchantContacts;
using Rappix.Notifications.Infrastructure.Messaging;
using Rappix.Notifications.Tests.Integration._Shared;

namespace Rappix.Notifications.Tests.Integration;

/// <summary>
/// Pruebas integration del <see cref="MerchantLifecycleConsumer"/>: verifica que cada uno de los 4
/// integration events de Merchants crea (o actualiza) la proyeccion <see cref="MerchantContact"/>.
/// La proyeccion es lo que <c>OrderSubmittedConsumer</c> y los otros consumers de pedido usaran en
/// commits 7-8 para resolver el email del merchant via UserContact.
/// </summary>
public sealed class MerchantLifecycleConsumerTests : NotificationsConsumerTestBase
{
    protected override void ConfigureHarness(IBusRegistrationConfigurator configurator) =>
        configurator.AddConsumer<MerchantLifecycleConsumer>();

    [Fact]
    public async Task Consume_MerchantApproved_CreatesMerchantContact()
    {
        Guid merchantId = Guid.CreateVersion7();
        Guid ownerUserId = Guid.CreateVersion7();
        var message = new MerchantApprovedIntegrationEvent
        {
            MerchantId = merchantId,
            OwnerUserId = ownerUserId,
            Name = "Pizzeria Test",
            Slug = "pizzeria-test",
            VerticalType = "Restaurant",
        };

        await Harness.Bus.Publish(message);
        (await Harness.GetConsumerHarness<MerchantLifecycleConsumer>()
            .Consumed.Any<MerchantApprovedIntegrationEvent>(filter => filter.Context.Message.MerchantId == merchantId))
            .Should().BeTrue();

        (Microsoft.Extensions.DependencyInjection.IServiceScope scope, var db) = CreateDbScope();
        using (scope)
        {
            MerchantContact? contact = await db.MerchantContacts.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == merchantId);

            contact.Should().NotBeNull();
            contact!.OwnerUserId.Should().Be(ownerUserId);
        }
    }

    [Fact]
    public async Task Consume_AllFourLifecycleEvents_UpsertSameMerchantContact_NoDuplicates()
    {
        Guid merchantId = Guid.CreateVersion7();
        Guid ownerUserId = Guid.CreateVersion7();

        // 1. Approved -> insert.
        await Harness.Bus.Publish(new MerchantApprovedIntegrationEvent
        {
            MerchantId = merchantId,
            OwnerUserId = ownerUserId,
            Name = "M",
            Slug = "m",
            VerticalType = "Restaurant",
        });
        await Harness.GetConsumerHarness<MerchantLifecycleConsumer>()
            .Consumed.Any<MerchantApprovedIntegrationEvent>(filter => filter.Context.Message.MerchantId == merchantId);

        // 2. Suspended -> update mismo owner (no-op idempotente, solo bump si owner cambia).
        await Harness.Bus.Publish(new MerchantSuspendedIntegrationEvent
        {
            MerchantId = merchantId,
            OwnerUserId = ownerUserId,
            Reason = "Test",
        });
        await Harness.GetConsumerHarness<MerchantLifecycleConsumer>()
            .Consumed.Any<MerchantSuspendedIntegrationEvent>(filter => filter.Context.Message.MerchantId == merchantId);

        // 3. Activated.
        await Harness.Bus.Publish(new MerchantActivatedIntegrationEvent
        {
            MerchantId = merchantId,
            OwnerUserId = ownerUserId,
        });
        await Harness.GetConsumerHarness<MerchantLifecycleConsumer>()
            .Consumed.Any<MerchantActivatedIntegrationEvent>(filter => filter.Context.Message.MerchantId == merchantId);

        // 4. Rejected (caso teorico — un merchant Active no se rechaza, pero el consumer lo procesa igual).
        await Harness.Bus.Publish(new MerchantRejectedIntegrationEvent
        {
            MerchantId = merchantId,
            OwnerUserId = ownerUserId,
            Reason = "Test",
        });
        await Harness.GetConsumerHarness<MerchantLifecycleConsumer>()
            .Consumed.Any<MerchantRejectedIntegrationEvent>(filter => filter.Context.Message.MerchantId == merchantId);

        (Microsoft.Extensions.DependencyInjection.IServiceScope scope, var db) = CreateDbScope();
        using (scope)
        {
            // Una sola fila por merchant — los 4 eventos hicieron upsert sobre la misma.
            int count = await db.MerchantContacts.AsNoTracking().CountAsync(c => c.Id == merchantId);
            count.Should().Be(1);

            MerchantContact contact = (await db.MerchantContacts.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == merchantId))!;
            contact.OwnerUserId.Should().Be(ownerUserId);
        }
    }
}
