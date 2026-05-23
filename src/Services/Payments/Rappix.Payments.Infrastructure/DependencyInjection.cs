using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.BuildingBlocks.Messaging.Extensions;
using Rappix.Payments.Application.Abstractions;
using Rappix.Payments.Infrastructure.Gateways;
using Rappix.Payments.Infrastructure.Messaging;
using Rappix.Payments.Infrastructure.Persistence;

namespace Rappix.Payments.Infrastructure;

/// <summary>Registro de servicios de la capa de infraestructura.</summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registra el DbContext, el repositorio, el reloj, el <see cref="IPaymentGateway"/> conmutable
    /// (Fake/Stripe) y MassTransit + outbox + el callback <c>UseEntityFrameworkOutbox</c> obligatorio
    /// en cada endpoint (leccion de Orders Fase 5 — sin esto los publish bufferizados se pierden).
    /// </summary>
    /// <remarks>
    /// El switch a <see cref="Gateways.FakePaymentGateway"/> es default. Stripe se cableara en el
    /// commit 9 de Fase 8 (al introducir <c>StripePaymentGateway</c>); por ahora cualquier valor
    /// <c>Payments:Gateway=Stripe</c> lanza <see cref="InvalidOperationException"/> explicito.
    /// </remarks>
    public static IServiceCollection AddPaymentsInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString("PaymentsDb")
            ?? throw new InvalidOperationException("Falta la cadena de conexion 'PaymentsDb'.");

        services.AddDbContext<PaymentsDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<PaymentsDbContext>());
        services.AddScoped<IPaymentRepository, PaymentRepository>();

        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

        RegisterPaymentGateway(services, configuration);

        AddMessaging(services, configuration);

        return services;
    }

    private static void RegisterPaymentGateway(IServiceCollection services, IConfiguration configuration)
    {
        // Solo leemos el string suelto (no requiere Microsoft.Extensions.Configuration.Binder en
        // Infrastructure; el bind de PaymentsOptions completo lo hace Api en Program.cs).
        string gateway = configuration["Payments:Gateway"] ?? "Fake";

        if (string.Equals(gateway, "Stripe", StringComparison.OrdinalIgnoreCase))
        {
            // El adaptador StripePaymentGateway llega en el commit 9 de Fase 8. Antes de eso, fallar
            // ruidosamente al arrancar es mejor que cargar Fake en silencio cuando el operador pidio Stripe.
            throw new InvalidOperationException(
                "Payments:Gateway=Stripe aun no esta cableado (sera anadido en el commit 9 de Fase 8). " +
                "Use Payments:Gateway=Fake o no defina la clave.");
        }

        // FakePaymentGateway como Singleton: el ConcurrentDictionary interno acumula respuestas por
        // idempotency-key durante la vida del proceso (analogo a la cache de 24h de Stripe). Ver
        // vigilance #2 en el comentario de la clase.
        services.AddSingleton<IPaymentGateway, FakePaymentGateway>();
    }

    private static void AddMessaging(IServiceCollection services, IConfiguration configuration) =>
        services.AddRappixMessaging(configuration, serviceName: "payments", configure: bus =>
        {
            // Consumer del flujo principal: PaymentRequested (saga AwaitingPayment) -> autoriza hold y
            // publica PaymentSucceeded/PaymentFailed.
            bus.AddConsumer<PaymentRequestedConsumer>();

            // OrderDeliveredCaptureConsumer (commit 6) y OrderTerminalCompensationConsumer (commit 7)
            // llegan despues.

            bus.AddEntityFrameworkOutbox<PaymentsDbContext>(outbox =>
            {
                outbox.UsePostgres();
                outbox.UseBusOutbox();
            });

            // CRITICO (leccion de Orders Fase 5): aplica el filtro de outbox EF a CADA endpoint —
            // envuelve cada consume en transaccion + SaveChanges + dedup de inbox. Sin esto, los
            // publish bufferizados nunca se vacian y los mensajes se pierden silenciosamente.
            bus.AddConfigureEndpointsCallback((context, _, cfg) => cfg.UseEntityFrameworkOutbox<PaymentsDbContext>(context));
        });
}
