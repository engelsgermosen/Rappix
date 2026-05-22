using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Rappix.BuildingBlocks.Core.Domain;
using Rappix.Orders.Application.Abstractions;
using Rappix.Orders.Domain.Abstractions;
using Rappix.Orders.Domain.Orders;
using Rappix.Orders.Infrastructure.Persistence.Converters;
using Rappix.Orders.Application.Sagas;

namespace Rappix.Orders.Infrastructure.Persistence;

/// <summary>
/// Contexto de EF Core del servicio Orders. Aloja el agregado Order y, en el MISMO contexto, el estado de la
/// saga (OrderState) y las tablas inbox/outbox de MassTransit, para que el estado de la saga y los mensajes
/// publicados se confirmen en una sola transaccion. En cada guardado despacha los eventos de dominio del
/// agregado (no de la saga) y traduce los conflictos de concurrencia (xmin) a ConcurrencyConflictException.
/// </summary>
public sealed class OrdersDbContext(DbContextOptions<OrdersDbContext> options, IPublisher publisher)
    : DbContext(options), IUnitOfWork
{
    /// <summary>Pedidos (agregado de dominio).</summary>
    public DbSet<Order> Orders => Set<Order>();

    /// <summary>Estado de la saga del pedido (instancias persistidas por el repositorio EF de MassTransit).</summary>
    public DbSet<OrderState> OrderStates => Set<OrderState>();

    /// <inheritdoc />
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await DispatchDomainEventsAsync(cancellationToken);

        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException("Conflicto de concurrencia al guardar el pedido o la saga.", ex);
        }
    }

    /// <inheritdoc />
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<OrderId>().HaveConversion<OrderIdConverter>();
        configurationBuilder.Properties<decimal>().HavePrecision(19, 4);
        base.ConfigureConventions(configurationBuilder);
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("orders");

        // Tablas del outbox transaccional de MassTransit (en el mismo contexto que la saga).
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrdersDbContext).Assembly);

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
