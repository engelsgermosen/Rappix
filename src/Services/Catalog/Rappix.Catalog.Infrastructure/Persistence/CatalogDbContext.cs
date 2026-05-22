using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Rappix.BuildingBlocks.Core.Domain;
using Rappix.Catalog.Application.Abstractions;
using Rappix.Catalog.Domain.Abstractions;
using Rappix.Catalog.Domain.Catalogs;
using Rappix.Catalog.Domain.Items;
using Rappix.Catalog.Infrastructure.Persistence.Converters;

namespace Rappix.Catalog.Infrastructure.Persistence;

/// <summary>
/// Contexto de EF Core del servicio Catalog. Actua como unidad de trabajo y, en cada guardado,
/// despacha los eventos de dominio (que la capa de aplicacion traduce a eventos de integracion
/// publicados a traves del outbox de MassTransit, en la misma transaccion). Traduce los conflictos
/// de concurrencia optimista (xmin del stock) a <see cref="ConcurrencyConflictException"/>.
/// </summary>
public sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options, IPublisher publisher)
    : DbContext(options), IUnitOfWork
{
    /// <summary>Catalogos (uno por merchant).</summary>
    public DbSet<MerchantCatalog> Catalogs => Set<MerchantCatalog>();

    /// <summary>Items.</summary>
    public DbSet<Item> Items => Set<Item>();

    /// <summary>Niveles de stock.</summary>
    public DbSet<StockLevel> StockLevels => Set<StockLevel>();

    /// <summary>Reservas (holds) de stock tomadas por pedidos durante su saga.</summary>
    public DbSet<StockReservation> StockReservations => Set<StockReservation>();

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
            // El token xmin detecto una escritura concurrente; los manejadores lo traducen a Conflict.
            throw new ConcurrencyConflictException("Conflicto de concurrencia al guardar el stock.", ex);
        }
    }

    /// <inheritdoc />
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Convierte los identificadores fuertemente tipados en todas las entidades (clave y claves foraneas).
        configurationBuilder.Properties<CatalogId>().HaveConversion<CatalogIdConverter>();
        configurationBuilder.Properties<ItemId>().HaveConversion<ItemIdConverter>();
        base.ConfigureConventions(configurationBuilder);
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("catalog");

        // Tablas del outbox transaccional de MassTransit.
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CatalogDbContext).Assembly);

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
