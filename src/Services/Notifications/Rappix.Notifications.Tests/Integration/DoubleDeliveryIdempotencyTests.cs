using FluentAssertions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Rappix.Contracts.Dispatch;
using Rappix.Contracts.Orders;
using Rappix.Notifications.Application.Abstractions;
using Rappix.Notifications.Application.Notifications;
using Rappix.Notifications.Domain.MerchantContacts;
using Rappix.Notifications.Domain.NotificationOrders;
using Rappix.Notifications.Domain.Notifications;
using Rappix.Notifications.Domain.UserContacts;
using Rappix.Notifications.Infrastructure.Messaging;
using Rappix.Notifications.Tests.Integration._Shared;

namespace Rappix.Notifications.Tests.Integration;

/// <summary>
/// CHECKPOINT CRITICO DE FASE 9 — espejo del doble-cobro de Payments. Verifica los DOS escenarios
/// de idempotencia que evitan emails duplicados:
/// <list type="bullet">
/// <item><b>Escenario A — broker redelivery puro:</b> mismo evento publicado 2 veces (re-entregas
///   de RabbitMQ) -&gt; el lookup de Nivel 1 en NotifyHandler atrapa el segundo y devuelve no-op
///   sin llamar al canal. Aplicado a un evento single-recipient (OrderAccepted): 1 fila, 1 call.</item>
/// <item><b>Escenario B — dos eventos distintos misma notificacion de negocio:</b>
///   <c>OrderDeliveredIntegrationEvent</c> (Dispatch) + <c>OrderCompletedIntegrationEvent</c>
///   (Orders saga) tienen MessageIds DIFERENTES, asi que el inbox EF NO los dedupe — pero AMBOS
///   mapean a <see cref="NotificationType.OrderDelivered"/>; el lookup por clave de negocio
///   <c>(RelatedOrderId, RecipientUserId, NotificationType)</c> encuentra el primero y descarta el
///   segundo. 2 filas (cust + merch) y 2 canal calls — NO 4. Este es el escenario que el dedup por
///   MessageId habria dejado pasar y el unique partial index a nivel BD blinda.</item>
/// </list>
/// Cualquier regression aqui = email duplicado al usuario en produccion. Test obligatorio antes de
/// shippear cualquier cambio a NotifyHandler, el unique index o el mapping de eventos terminales.
/// </summary>
public sealed class DoubleDeliveryIdempotencyTests : NotificationsConsumerTestBase
{
    protected override void ConfigureHarness(IBusRegistrationConfigurator configurator)
    {
        // Ambos consumers en el mismo harness — uno para cada escenario.
        configurator.AddConsumer<OrderAcceptedConsumer>();
        configurator.AddConsumer<OrderTerminalEventsConsumer>();
    }

    // ----------------- Escenario A: redelivery del mismo evento ----------------------------------

    [Fact]
    public async Task ScenarioA_SameEventPublishedTwice_ProducesExactlyOneNotificationAndOneChannelCall()
    {
        Guid orderId = Guid.CreateVersion7();
        Guid customerUserId = Guid.CreateVersion7();
        Guid merchantId = Guid.CreateVersion7();

        await SeedNotificationOrderAndCustomerAsync(orderId, customerUserId, merchantId);

        var message = new OrderAcceptedIntegrationEvent
        {
            OrderId = orderId,
            MerchantId = merchantId,
            AcceptedAtUtc = DateTime.UtcNow,
        };

        // 1er publish.
        await Harness.Bus.Publish(message);
        await Harness.GetConsumerHarness<OrderAcceptedConsumer>()
            .Consumed.Any<OrderAcceptedIntegrationEvent>(filter => filter.Context.Message.OrderId == orderId);

        // 2do publish del MISMO mensaje. El harness in-memory no aplica inbox EF (deliberadamente —
        // el Nivel 1 lookup-then-insert del NotifyHandler debe atajar este caso por si solo).
        await Harness.Bus.Publish(message);
        await Task.Delay(500); // Pequeno wait para que el segundo consume ejecute.

        (IServiceScope scope, var db) = CreateDbScope();
        using (scope)
        {
            int count = await db.Notifications.AsNoTracking()
                .CountAsync(n => n.RelatedOrderId == orderId && n.NotificationType == NotificationType.OrderAccepted);
            count.Should().Be(1, because: "el lookup por clave de negocio debe atajar el segundo publish ANTES de insertar");
        }

        // Canal llamado EXACTAMENTE 1 vez — NO 2.
        await Channel.Received(1).SendAsync(Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>());
    }

    // ----------------- Escenario B: dos eventos distintos misma notificacion ---------------------

    [Fact]
    public async Task ScenarioB_OrderDeliveredThenOrderCompleted_ProduceExactlyTwoNotifications_NotFour()
    {
        (Guid orderId, Guid customerUserId, Guid merchantId, Guid ownerUserId) = await SeedFullSetupAsync();

        // Primer terminal: OrderDelivered de Dispatch -> NotifyTerminal(type=OrderDelivered) -> notifica
        // a CUSTOMER y a MERCHANT. Resultado tras este publish: 2 filas, 2 channel calls.
        await Harness.Bus.Publish(new OrderDeliveredIntegrationEvent
        {
            OrderId = orderId,
            DeliveredAtUtc = DateTime.UtcNow,
        });
        await Harness.GetConsumerHarness<OrderTerminalEventsConsumer>()
            .Consumed.Any<OrderDeliveredIntegrationEvent>(filter => filter.Context.Message.OrderId == orderId);

        // Verificacion intermedia: 2 filas + 2 channel calls.
        (IServiceScope mid, var midDb) = CreateDbScope();
        using (mid)
        {
            int count = await midDb.Notifications.AsNoTracking()
                .CountAsync(n => n.RelatedOrderId == orderId);
            count.Should().Be(2, because: "OrderDelivered debe haber generado 2 filas (cust + merch)");
        }
        await Channel.Received(2).SendAsync(Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>());

        // Segundo terminal: OrderCompleted de Orders saga. MessageId DIFERENTE (cada Bus.Publish
        // genera uno nuevo); el inbox EF NO podria dedupe esto en produccion. Pero AMBOS mapean al
        // mismo NotificationType.OrderDelivered — el lookup por clave de negocio
        // (RelatedOrderId, RecipientUserId, NotificationType) encuentra las 2 filas previas y devuelve
        // no-op.
        await Harness.Bus.Publish(new OrderCompletedIntegrationEvent
        {
            OrderId = orderId,
            CustomerUserId = customerUserId,
            MerchantId = merchantId,
            CompletedAtUtc = DateTime.UtcNow,
        });
        await Harness.GetConsumerHarness<OrderTerminalEventsConsumer>()
            .Consumed.Any<OrderCompletedIntegrationEvent>(filter => filter.Context.Message.OrderId == orderId);

        // ASSERT CRITICO: siguen siendo 2 filas (no 4). El canal NO se llamo de nuevo (sigue en 2).
        (IServiceScope final, var finalDb) = CreateDbScope();
        using (final)
        {
            var notifications = await finalDb.Notifications.AsNoTracking()
                .Where(n => n.RelatedOrderId == orderId)
                .ToListAsync();

            notifications.Should().HaveCount(2,
                because: "OrderCompleted debe ser no-op (mismo NotificationType.OrderDelivered que el OrderDelivered previo); SIN esto el cliente recibiria 2 emails 'fue entregado'");
            notifications.Should().AllSatisfy(n => n.NotificationType.Should().Be(NotificationType.OrderDelivered));
            notifications.Should().Contain(n => n.RecipientRole == RecipientRole.Customer);
            notifications.Should().Contain(n => n.RecipientRole == RecipientRole.Merchant);
        }

        // Canal llamado TOTAL 2 veces (no 4) — el segundo evento no se propago al canal.
        await Channel.Received(2).SendAsync(Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>());
    }

    // ----------------- Bonus: race level 2 (unique constraint a nivel BD) ------------------------

    [Fact]
    public async Task ScenarioB_OrderCompletedFirstThenOrderDelivered_StillExactlyTwoNotifications()
    {
        // Orden invertido para confirmar que el dedup es simetrico — no depende de cual evento llega
        // primero, depende del NotificationType.
        (Guid orderId, Guid customerUserId, Guid merchantId, _) = await SeedFullSetupAsync();

        await Harness.Bus.Publish(new OrderCompletedIntegrationEvent
        {
            OrderId = orderId,
            CustomerUserId = customerUserId,
            MerchantId = merchantId,
            CompletedAtUtc = DateTime.UtcNow,
        });
        await Harness.GetConsumerHarness<OrderTerminalEventsConsumer>()
            .Consumed.Any<OrderCompletedIntegrationEvent>(filter => filter.Context.Message.OrderId == orderId);

        await Harness.Bus.Publish(new OrderDeliveredIntegrationEvent
        {
            OrderId = orderId,
            DeliveredAtUtc = DateTime.UtcNow,
        });
        await Harness.GetConsumerHarness<OrderTerminalEventsConsumer>()
            .Consumed.Any<OrderDeliveredIntegrationEvent>(filter => filter.Context.Message.OrderId == orderId);

        (IServiceScope scope, var db) = CreateDbScope();
        using (scope)
        {
            int count = await db.Notifications.AsNoTracking()
                .CountAsync(n => n.RelatedOrderId == orderId);
            count.Should().Be(2, because: "el orden de los eventos terminales no debe alterar el resultado del dedup");
        }
        await Channel.Received(2).SendAsync(Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>());
    }

    // ----------------- Helpers de seed -----------------------------------------------------------

    private async Task SeedNotificationOrderAndCustomerAsync(Guid orderId, Guid customerUserId, Guid merchantId)
    {
        (IServiceScope scope, var db) = CreateDbScope();
        using (scope)
        {
            db.NotificationOrders.Add(NotificationOrder.Create(orderId, customerUserId, merchantId, DateTime.UtcNow));
            db.UserContacts.Add(UserContact.Create(
                userId: customerUserId,
                email: "cliente@test.local",
                firstName: "Juan",
                lastName: "Perez",
                userType: UserType.Customer,
                emailConfirmed: true,
                utcNow: DateTime.UtcNow));
            await db.SaveChangesAsync();
        }
    }

    private async Task<(Guid OrderId, Guid CustomerUserId, Guid MerchantId, Guid OwnerUserId)> SeedFullSetupAsync()
    {
        Guid orderId = Guid.CreateVersion7();
        Guid customerUserId = Guid.CreateVersion7();
        Guid merchantId = Guid.CreateVersion7();
        Guid ownerUserId = Guid.CreateVersion7();

        (IServiceScope scope, var db) = CreateDbScope();
        using (scope)
        {
            db.NotificationOrders.Add(NotificationOrder.Create(orderId, customerUserId, merchantId, DateTime.UtcNow));
            db.MerchantContacts.Add(MerchantContact.Create(merchantId, ownerUserId, DateTime.UtcNow));
            db.UserContacts.Add(UserContact.Create(
                userId: customerUserId,
                email: "cliente@test.local",
                firstName: "Juan",
                lastName: "Perez",
                userType: UserType.Customer,
                emailConfirmed: true,
                utcNow: DateTime.UtcNow));
            db.UserContacts.Add(UserContact.Create(
                userId: ownerUserId,
                email: "owner@merchant.local",
                firstName: "Maria",
                lastName: "Garcia",
                userType: UserType.Merchant,
                emailConfirmed: true,
                utcNow: DateTime.UtcNow));
            await db.SaveChangesAsync();
        }

        return (orderId, customerUserId, merchantId, ownerUserId);
    }
}
