using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.BuildingBlocks.Messaging.Extensions;
using Rappix.Notifications.Application.Abstractions;
using Rappix.Notifications.Infrastructure.Channels;
using Rappix.Notifications.Infrastructure.Messaging;
using Rappix.Notifications.Infrastructure.Persistence;
using Rappix.Notifications.Infrastructure.Persistence.Repositories;

namespace Rappix.Notifications.Infrastructure;

/// <summary>Registro de servicios de la capa de infraestructura.</summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registra el DbContext, los 4 repositorios, el reloj, el <see cref="INotificationChannel"/>
    /// conmutable (Fake/SendGrid) y (en commit 6) MassTransit con outbox callback. El switch del
    /// canal sigue el patron de <c>IPaymentGateway</c> en Payments: Fake por defecto;
    /// <c>Notifications:Channel=SendGrid</c> opt-in en commit 9.
    /// </summary>
    public static IServiceCollection AddNotificationsInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString("NotificationsDb")
            ?? throw new InvalidOperationException("Falta la cadena de conexion 'NotificationsDb'.");

        services.AddDbContext<NotificationsDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<NotificationsDbContext>());

        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IUserContactRepository, UserContactRepository>();
        services.AddScoped<IMerchantContactRepository, MerchantContactRepository>();
        services.AddScoped<INotificationOrderRepository, NotificationOrderRepository>();

        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

        RegisterNotificationChannel(services, configuration);

        AddMessaging(services, configuration);

        return services;
    }

    private static void AddMessaging(IServiceCollection services, IConfiguration configuration) =>
        services.AddRappixMessaging(configuration, serviceName: "notifications", configure: bus =>
        {
            // Consumers de Identity: pueblan UserContact (la proyeccion userId -> email + rol).
            bus.AddConsumer<UserRegisteredConsumer>();
            bus.AddConsumer<UserEmailConfirmedConsumer>();

            // Consumer multi-IConsumer de Merchants: puebla MerchantContact (merchantId -> ownerUserId)
            // desde los 4 events de lifecycle. El primero (Approved) es el que ocurre antes que
            // OrderSubmitted, asi que en condiciones normales el contact ya existe cuando llega el
            // primer pedido. Cold-start gap documentado en ADR-0010.
            bus.AddConsumer<MerchantLifecycleConsumer>();

            // Consumers de pedido: proyectan NotificationOrder y/o notifican a destinatarios via
            // NotifyHandler. Cada uno hace su mapping evento->destinatario+plantilla.
            bus.AddConsumer<OrderSubmittedConsumer>();
            bus.AddConsumer<OrderAcceptedConsumer>();
            bus.AddConsumer<CourierAssignedConsumer>();

            // Multi-IConsumer sobre los 4 eventos terminales del pedido (OrderDelivered de Dispatch,
            // OrderCompleted/Cancelled/Failed de la saga de Orders). CRITICO: OrderDelivered y
            // OrderCompleted mapean al MISMO NotificationType.OrderDelivered — el unique partial
            // index UX_Notification_BusinessKey dedupe ambos por (RelatedOrderId, RecipientUserId,
            // NotificationType). Sin esto el cliente recibiria dos emails "fue entregado".
            bus.AddConsumer<OrderTerminalEventsConsumer>();

            bus.AddEntityFrameworkOutbox<NotificationsDbContext>(outbox =>
            {
                outbox.UsePostgres();
                outbox.UseBusOutbox();
            });

            // CRITICO (leccion de Orders Fase 5 aplicada desde el primer consumer): aplica el filtro
            // de outbox EF a CADA endpoint — envuelve cada consume en transaccion + SaveChanges +
            // INBOX DEDUP por MessageId. Notifications no publica al bus en Fase 9 (solo consume +
            // envia emails), pero el INBOX SI es necesario — es el Nivel 1 de idempotencia que ataja
            // las redelivers del broker antes incluso de llegar al consumer (ADR-0010 D4).
            bus.AddConfigureEndpointsCallback((context, _, cfg) => cfg.UseEntityFrameworkOutbox<NotificationsDbContext>(context));
        });

    private static void RegisterNotificationChannel(IServiceCollection services, IConfiguration configuration)
    {
        // Solo leemos el string suelto (no requiere Microsoft.Extensions.Configuration.Binder en
        // Infrastructure; el bind de NotificationsOptions completo lo hace Api en Program.cs).
        string channel = configuration["Notifications:Channel"] ?? "Fake";

        if (string.Equals(channel, "SendGrid", StringComparison.OrdinalIgnoreCase))
        {
            // SendGridNotificationChannel se cablea en el commit 9 de Fase 9. Por ahora fail-fast
            // explicito si alguien fuerza el switch en config — evita arrancar con un canal "fantasma"
            // (sin la dependencia SendGrid en csproj el resolve fallaria mas tarde con un mensaje
            // menos claro).
            throw new InvalidOperationException(
                "Notifications:Channel=SendGrid no esta cableado hasta el commit 9. Use Notifications:Channel=Fake.");
        }

        // FakeNotificationChannel como Singleton: no mantiene estado (a diferencia de
        // FakePaymentGateway que cachea idempotency keys); solo loguea. Singleton ahorra
        // el resolve por scope sin coste de aislamiento.
        services.AddSingleton<INotificationChannel, FakeNotificationChannel>();
    }
}
