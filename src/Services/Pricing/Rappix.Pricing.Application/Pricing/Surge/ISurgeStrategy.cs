using Rappix.Pricing.Domain.Common;

namespace Rappix.Pricing.Application.Pricing.Surge;

/// <summary>Estrategia que resuelve el multiplicador de surge de una cotizacion.</summary>
public interface ISurgeStrategy
{
    /// <summary>Resuelve el multiplicador de surge (siempre entre 1.0 y el cap configurado).</summary>
    Task<decimal> ResolveMultiplierAsync(SurgeContext context, CancellationToken cancellationToken);
}

/// <summary>Contexto para resolver el surge.</summary>
/// <param name="ZoneId">Zona geografica del pedido (mapeada por el caller; null = sin zona).</param>
/// <param name="Vertical">Vertical del pedido.</param>
/// <param name="UtcNow">Momento actual (UTC); se convierte a hora local para evaluar las franjas.</param>
public sealed record SurgeContext(string? ZoneId, VerticalType Vertical, DateTime UtcNow);
