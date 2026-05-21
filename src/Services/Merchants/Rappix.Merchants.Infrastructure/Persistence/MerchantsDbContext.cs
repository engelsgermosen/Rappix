using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Rappix.BuildingBlocks.Core.Domain;
using Rappix.Merchants.Application.Abstractions;
using Rappix.Merchants.Domain.Abstractions;
using Rappix.Merchants.Domain.Merchants;
using Rappix.Merchants.Infrastructure.Persistence.Converters;

namespace Rappix.Merchants.Infrastructure.Persistence;

/// <summary>
/// Contexto de EF Core del servicio Merchants. Actua como unidad de trabajo y, en cada guardado,
/// despacha los eventos de dominio (que la capa de aplicacion traduce a eventos de integracion
/// publicados a traves del outbox de MassTransit, dentro de la misma transaccion).
/// </summary>
public sealed class MerchantsDbContext(DbContextOptions<MerchantsDbContext> options, IPublisher publisher)
    : DbContext(options), IUnitOfWork
{
    /// <summary>Comercios.</summary>
    public DbSet<Merchant> Merchants => Set<Merchant>();

    /// <inheritdoc />
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Publicar antes del guardado garantiza que las filas del outbox entren en la misma transaccion.
        await DispatchDomainEventsAsync(cancellationToken);
        return await base.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Convierte el identificador fuertemente tipado en todas las entidades (clave y claves foraneas).
        configurationBuilder.Properties<MerchantId>().HaveConversion<MerchantIdConverter>();
        base.ConfigureConventions(configurationBuilder);
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("merchants");

        // Tablas del outbox transaccional de MassTransit.
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MerchantsDbContext).Assembly);

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
