using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Rappix.BuildingBlocks.Core.Domain;
using Rappix.Payments.Application.Abstractions;
using Rappix.Payments.Domain.Abstractions;
using Rappix.Payments.Domain.Payments;

namespace Rappix.Payments.Infrastructure.Persistence;

/// <summary>
/// Contexto de EF Core del servicio Payments. Actua como unidad de trabajo. Ningun aggregate hoy
/// emite domain events (Payments publica integration events directamente desde los consumers, ver
/// ADR-0009 pendiente), por lo que <c>DispatchDomainEventsAsync</c> es efectivamente no-op; se
/// mantiene el wiring por consistencia con el resto del repo y por si en el futuro algun aggregate
/// eleva eventos internos.
/// </summary>
public sealed class PaymentsDbContext(DbContextOptions<PaymentsDbContext> options, IPublisher publisher)
    : DbContext(options), IUnitOfWork
{
    /// <summary>Pagos (un row por OrderId; ver Payment.cs y ADR-0009 #4).</summary>
    public DbSet<Payment> Payments => Set<Payment>();

    /// <inheritdoc />
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Publicar antes del guardado garantiza que si en el futuro algun aggregate emite eventos,
        // las filas del outbox EF entren en la misma transaccion (patron Tracking/Dispatch).
        await DispatchDomainEventsAsync(cancellationToken);

        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException(
                "Otra operacion concurrente modifico el registro de pago. Reintente.", ex);
        }
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("payments");

        // Tablas del outbox transaccional de MassTransit. Payments PUBLICA (PaymentSucceeded,
        // PaymentFailed, RefundCompleted) Y CONSUME (PaymentRequested, OrderDelivered, OrderCancelled,
        // OrderFailed, RefundRequested); ambas requieren las tablas. El filtro UseEntityFrameworkOutbox
        // del callback aplica el inbox (dedupe por MessageId) + envuelve el publish en la transaccion
        // del SaveChanges del consumer — leccion de Orders Fase 5 (sin esto, los publish se pierden).
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PaymentsDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }

    /// <inheritdoc />
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Convencion global del repo (patron Pricing): todos los decimals son precision(19, 4).
        // Money.Amount lo hereda; cualquier propiedad decimal futura la respetara sin configuracion.
        configurationBuilder.Properties<decimal>().HavePrecision(19, 4);
        base.ConfigureConventions(configurationBuilder);
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
