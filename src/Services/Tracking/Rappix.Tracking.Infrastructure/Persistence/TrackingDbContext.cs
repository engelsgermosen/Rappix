using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Rappix.BuildingBlocks.Core.Domain;
using Rappix.Tracking.Application.Abstractions;
using Rappix.Tracking.Domain.Abstractions;
using Rappix.Tracking.Domain.CourierActiveOrders;
using Rappix.Tracking.Domain.OrderTrackings;

namespace Rappix.Tracking.Infrastructure.Persistence;

/// <summary>
/// Contexto de EF Core del servicio Tracking. Actua como unidad de trabajo. Ningun aggregate hoy
/// emite domain events (read model puro), por lo que <c>DispatchDomainEventsAsync</c> es
/// efectivamente no-op; se mantiene el wiring por consistencia con el resto del repo y por si en
/// el futuro algun read model eleva eventos internos.
/// </summary>
public sealed class TrackingDbContext(DbContextOptions<TrackingDbContext> options, IPublisher publisher)
    : DbContext(options), IUnitOfWork
{
    /// <summary>Read model por pedido.</summary>
    public DbSet<OrderTracking> OrderTrackings => Set<OrderTracking>();

    /// <summary>Mapping CourierId -> OrderId (1↔1 mientras el courier esta asignado).</summary>
    public DbSet<CourierActiveOrder> CourierActiveOrders => Set<CourierActiveOrder>();

    /// <inheritdoc />
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Publicar antes del guardado garantiza que si en el futuro algun aggregate emite eventos,
        // las filas del outbox EF entren en la misma transaccion (patron Dispatch).
        await DispatchDomainEventsAsync(cancellationToken);

        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException(
                "Otra operacion concurrente modifico el registro de tracking. Reintente.", ex);
        }
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("tracking");

        // Tablas del outbox transaccional de MassTransit. Tracking solo CONSUME (no publica nunca al
        // bus en Fase 7); las tablas del outbox quedan registradas porque el filtro
        // UseEntityFrameworkOutbox del callback aplica el inbox (dedupe por MessageId) a cada
        // consumer aunque no haya publicacion saliente.
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TrackingDbContext).Assembly);

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
