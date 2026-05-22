using Rappix.Pricing.Domain.Coupons;
using Rappix.Pricing.Domain.ItemPrices;
using Rappix.Pricing.Domain.Quotes;
using Rappix.Pricing.Domain.Surge;

namespace Rappix.Pricing.Application.Abstractions;

/// <summary>Acceso a la persistencia del agregado <see cref="Quote"/>.</summary>
public interface IQuoteRepository
{
    /// <summary>Marca una nueva cotizacion para insercion.</summary>
    void Add(Quote quote);

    /// <summary>Obtiene una cotizacion por id, incluyendo sus lineas (o null si no existe).</summary>
    Task<Quote?> GetByIdAsync(QuoteId id, CancellationToken cancellationToken);
}

/// <summary>Acceso a la persistencia del agregado <see cref="Coupon"/> y sus redenciones.</summary>
public interface ICouponRepository
{
    /// <summary>Marca un nuevo cupon para insercion.</summary>
    void Add(Coupon coupon);

    /// <summary>Obtiene un cupon por id (respeta el filtro de borrado logico).</summary>
    Task<Coupon?> GetByIdAsync(CouponId id, CancellationToken cancellationToken);

    /// <summary>Obtiene un cupon por id incluyendo los borrados (para redimir al consumir).</summary>
    Task<Coupon?> GetByIdIncludingDeletedAsync(CouponId id, CancellationToken cancellationToken);

    /// <summary>Obtiene un cupon activo por codigo (para cotizar).</summary>
    Task<Coupon?> GetActiveByCodeAsync(string code, CancellationToken cancellationToken);

    /// <summary>Indica si ya existe un cupon (no borrado) con el codigo dado.</summary>
    Task<bool> ExistsByCodeAsync(string code, CancellationToken cancellationToken);

    /// <summary>Lista los cupones no borrados.</summary>
    Task<IReadOnlyList<Coupon>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Cuenta cuantas veces un cliente redimio un cupon (sin contar redenciones revertidas), para el limite por usuario.</summary>
    Task<int> CountRedemptionsAsync(CouponId couponId, Guid customerUserId, CancellationToken cancellationToken);

    /// <summary>Obtiene la redencion activa (no revertida) de un cupon en una cotizacion, para revertirla al compensar.</summary>
    Task<CouponRedemption?> GetActiveRedemptionAsync(CouponId couponId, QuoteId quoteId, CancellationToken cancellationToken);

    /// <summary>Marca una nueva redencion para insercion.</summary>
    void AddRedemption(CouponRedemption redemption);
}

/// <summary>Acceso a la persistencia del agregado <see cref="SurgeRule"/>.</summary>
public interface ISurgeRuleRepository
{
    /// <summary>Marca una nueva regla para insercion.</summary>
    void Add(SurgeRule rule);

    /// <summary>Obtiene una regla por id (respeta el filtro de borrado logico).</summary>
    Task<SurgeRule?> GetByIdAsync(SurgeRuleId id, CancellationToken cancellationToken);

    /// <summary>Lista las reglas activas (no borradas) para el calculo de surge.</summary>
    Task<IReadOnlyList<SurgeRule>> GetActiveAsync(CancellationToken cancellationToken);

    /// <summary>Lista todas las reglas no borradas (para administracion).</summary>
    Task<IReadOnlyList<SurgeRule>> ListAsync(CancellationToken cancellationToken);
}

/// <summary>Acceso a la persistencia del cache local de precios de item (<see cref="ItemPriceCache"/>).</summary>
public interface IItemPriceCacheRepository
{
    /// <summary>Obtiene el precio cacheado de un item (o null si no esta cacheado).</summary>
    Task<ItemPriceCache?> GetByItemIdAsync(Guid itemId, CancellationToken cancellationToken);

    /// <summary>Marca una nueva entrada de cache para insercion.</summary>
    void Add(ItemPriceCache cache);
}
