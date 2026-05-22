using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Rappix.BuildingBlocks.Core.Domain;
using Rappix.Pricing.Application.Abstractions;
using Rappix.Pricing.Domain.Abstractions;
using Rappix.Pricing.Domain.Coupons;
using Rappix.Pricing.Domain.ItemPrices;
using Rappix.Pricing.Domain.Quotes;
using Rappix.Pricing.Domain.Surge;
using Rappix.Pricing.Infrastructure.Persistence.Converters;

namespace Rappix.Pricing.Infrastructure.Persistence;

/// <summary>
/// Contexto de EF Core del servicio Pricing. Actua como unidad de trabajo y, en cada guardado, despacha
/// los eventos de dominio (que la capa de aplicacion traduce a eventos de integracion publicados por el
/// outbox de MassTransit, en la misma transaccion). Traduce los conflictos de concurrencia optimista
/// (xmin del cupon) a <see cref="ConcurrencyConflictException"/>. Todo decimal mapea a numeric(19,4).
/// </summary>
public sealed class PricingDbContext(DbContextOptions<PricingDbContext> options, IPublisher publisher)
    : DbContext(options), IUnitOfWork
{
    /// <summary>Cotizaciones.</summary>
    public DbSet<Quote> Quotes => Set<Quote>();

    /// <summary>Cupones.</summary>
    public DbSet<Coupon> Coupons => Set<Coupon>();

    /// <summary>Redenciones de cupon (para el limite por usuario).</summary>
    public DbSet<CouponRedemption> CouponRedemptions => Set<CouponRedemption>();

    /// <summary>Reglas de surge.</summary>
    public DbSet<SurgeRule> SurgeRules => Set<SurgeRule>();

    /// <summary>Cache local de precios de item.</summary>
    public DbSet<ItemPriceCache> ItemPrices => Set<ItemPriceCache>();

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
            // El token xmin detecto una escritura concurrente (p. ej. dos consumos del mismo cupon).
            throw new ConcurrencyConflictException("Conflicto de concurrencia al guardar.", ex);
        }
    }

    /// <inheritdoc />
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Todo el dinero (y demas decimales) en numeric(19,4): precision suficiente sin error de acumulacion.
        configurationBuilder.Properties<decimal>().HavePrecision(19, 4);

        // Identificadores fuertemente tipados de las raices de agregado.
        configurationBuilder.Properties<QuoteId>().HaveConversion<QuoteIdConverter>();
        configurationBuilder.Properties<CouponId>().HaveConversion<CouponIdConverter>();
        configurationBuilder.Properties<SurgeRuleId>().HaveConversion<SurgeRuleIdConverter>();

        base.ConfigureConventions(configurationBuilder);
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("pricing");

        // Tablas del outbox transaccional de MassTransit.
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PricingDbContext).Assembly);

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
