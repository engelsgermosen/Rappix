using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Rappix.BuildingBlocks.Messaging.Extensions;

public static class MessagingExtensions
{
    /// <summary>
    /// Registra MassTransit con RabbitMQ. Cada servicio llama esto y opcionalmente
    /// configura sus consumers vía la acción <paramref name="configure"/>.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configuration">Configuración del host (lee la sección "RabbitMq").</param>
    /// <param name="serviceName">Identificador del servicio, usado como prefijo de colas.</param>
    /// <param name="configure">Configuración adicional del bus (consumers, sagas).</param>
    /// <param name="configureBus">
    /// Configuración adicional de la fábrica del bus RabbitMQ (p. ej. el scheduler de mensajes de la saga),
    /// aplicada antes de ConfigureEndpoints. Opcional: los servicios que no la necesitan pasan null.
    /// </param>
    public static IServiceCollection AddRappixMessaging(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName,
        Action<IBusRegistrationConfigurator>? configure = null,
        Action<IBusRegistrationContext, IRabbitMqBusFactoryConfigurator>? configureBus = null)
    {
        services.AddMassTransit(busConfigurator =>
        {
            busConfigurator.SetKebabCaseEndpointNameFormatter();

            configure?.Invoke(busConfigurator);

            busConfigurator.UsingRabbitMq((context, cfg) =>
            {
                var host = configuration["RabbitMq:Host"] ?? "localhost";
                var port = configuration.GetValue<int>("RabbitMq:Port", 5672);
                var user = configuration["RabbitMq:Username"] ?? "guest";
                var pass = configuration["RabbitMq:Password"] ?? "guest";

                cfg.Host(host, (ushort)port, "/", h =>
                {
                    h.Username(user);
                    h.Password(pass);
                });

                // Reintentos exponenciales para fallos transitorios
                cfg.UseMessageRetry(r => r.Exponential(
                    retryLimit: 5,
                    minInterval: TimeSpan.FromSeconds(1),
                    maxInterval: TimeSpan.FromSeconds(30),
                    intervalDelta: TimeSpan.FromSeconds(2)));

                configureBus?.Invoke(context, cfg);

                cfg.ConfigureEndpoints(context, new KebabCaseEndpointNameFormatter(prefix: serviceName, includeNamespace: false));
            });
        });

        return services;
    }
}
