using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Rappix.BuildingBlocks.Core.Domain;
using Rappix.Dispatch.Application.Abstractions;
using Rappix.Dispatch.Domain.Abstractions;
using Rappix.Dispatch.Domain.Couriers;
using Rappix.Dispatch.Infrastructure.Persistence.Converters;

namespace Rappix.Dispatch.Infrastructure.Persistence;

/// <summary>
/// Contexto de EF Core del servicio Dispatch. Actua como unidad de trabajo y, en cada guardado,
/// despacha los eventos de dominio (que la capa de aplicacion traduce a eventos de integracion
/// publicados a traves del outbox de MassTransit, dentro de la misma transaccion).
/// </summary>
public sealed class DispatchDbContext(DbContextOptions<DispatchDbContext> options, IPublisher publisher)
    : DbContext(options), IUnitOfWork
{
    /// <summary>Couriers (aggregate).</summary>
    public DbSet<CourierProfile> CourierProfiles => Set<CourierProfile>();

    /// <summary>Asignaciones historicas courier->pedido.</summary>
    public DbSet<CourierAssignment> CourierAssignments => Set<CourierAssignment>();

    /// <inheritdoc />
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Publicar antes del guardado garantiza que las filas del outbox entren en la misma transaccion.
        await DispatchDomainEventsAsync(cancellationToken);

        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException(
                "Otra operacion concurrente modifico el registro. Reintente.", ex);
        }
    }

    /// <inheritdoc />
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Convierte el identificador fuertemente tipado en todas las entidades (clave y claves foraneas).
        configurationBuilder.Properties<CourierId>().HaveConversion<CourierIdConverter>();
        // decimal(19,4) NO aplica aqui (CapacityKg ya usa numeric(6,2) en su mapping).
        base.ConfigureConventions(configurationBuilder);
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("dispatch");

        // Tablas del outbox transaccional de MassTransit.
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DispatchDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }

    private async Task DispatchDomainEventsAsync(CancellationToken cancellationToken)
    {
        IHasDomainEvents[] aggregates =
        [
            .. ChangeTracker.Entries<IHasDomainEvents>()
                .Where(entry => entry.Entity.DomainEvents.Count > 0)
                .Select(entry => entry.Entity)
        ];

        IDomainEvent[] domainEvents = [.. aggregates.SelectMany(aggregate => aggregate.DomainEvents)];

        foreach (IHasDomainEvents aggregate in aggregates)
        {
            aggregate.ClearDomainEvents();
        }

        foreach (IDomainEvent domainEvent in domainEvents)
        {
            Type notificationType = typeof(DomainEventNotification<>).MakeGenericType(domainEvent.GetType());
            object notification = Activator.CreateInstance(notificationType, domainEvent)!;
            await publisher.Publish(notification, cancellationToken);
        }
    }
}
