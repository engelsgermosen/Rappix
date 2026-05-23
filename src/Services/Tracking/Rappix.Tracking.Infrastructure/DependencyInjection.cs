using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Rappix.Tracking.Infrastructure;

/// <summary>Registro de servicios de la capa de infraestructura.</summary>
public static class DependencyInjection
{
    /// <summary>
    /// Esqueleto del registro de infraestructura. En el commit 3 se anade el DbContext + repositorios; en
    /// el commit 6 se anade MassTransit + outbox + AddConfigureEndpointsCallback. Tracking NO usa Redis
    /// (sin idempotency, sin Geo, sin SignalR backplane en Fase 7).
    /// </summary>
    public static IServiceCollection AddTrackingInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        _ = configuration; // los registros vienen en commits posteriores.
        return services;
    }
}
