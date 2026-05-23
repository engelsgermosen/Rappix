using Rappix.BuildingBlocks.Core.Domain;

namespace Rappix.Notifications.Domain.MerchantContacts;

/// <summary>
/// Proyeccion local <c>MerchantId -> OwnerUserId</c> para que Notifications pueda resolver al
/// "merchant" como un usuario notificable sin llamar a Merchants ni mantener un join. Se mantiene
/// desde los 4 integration events de Merchants que llevan ambos IDs:
/// <c>MerchantApprovedIntegrationEvent</c>, <c>MerchantActivatedIntegrationEvent</c>,
/// <c>MerchantRejectedIntegrationEvent</c>, <c>MerchantSuspendedIntegrationEvent</c>.
/// El <see cref="Entity{TId}.Id"/> ES el <c>MerchantId</c> (relacion 1-1).
/// </summary>
/// <remarks>
/// Cold-start risk: si Notifications arranca DESPUES de que el merchant fue aprobado, esta
/// proyeccion no tendra la fila y los emails al merchant se persisten como <c>Failed</c> con
/// <c>errorReason="MerchantContact missing for {merchantId}"</c> (no crashea el consumer). Mitigacion
/// local-dev: <c>docker compose down -v</c> + seed. Mitigacion produccion: backfill script
/// (follow-up de ADR-0010).
/// </remarks>
public sealed class MerchantContact : Entity<Guid>
{
    // EF necesita ctor sin parametros para materializar.
    private MerchantContact() { }

    private MerchantContact(Guid merchantId, Guid ownerUserId, DateTime utcNow)
        : base(merchantId)
    {
        OwnerUserId = ownerUserId;
        UpdatedAtUtc = utcNow;
    }

    /// <summary>UserId del dueno del merchant — el <c>sub</c> del JWT que la app del merchant usa para autenticarse.</summary>
    public Guid OwnerUserId { get; private set; }

    /// <summary>Ultimo upsert (UTC).</summary>
    public DateTime UpdatedAtUtc { get; private set; }

    /// <summary>Crea un nuevo contacto desde uno de los 4 eventos de lifecycle de Merchants.</summary>
    public static MerchantContact Create(Guid merchantId, Guid ownerUserId, DateTime utcNow) =>
        new(merchantId, ownerUserId, utcNow);

    /// <summary>
    /// Upsert: actualiza el <see cref="OwnerUserId"/> si cambio (defensivo — en el modelo actual el
    /// owner no rota, pero el contrato no lo prohibe). Idempotente si el owner es el mismo.
    /// </summary>
    public void Update(Guid ownerUserId, DateTime utcNow)
    {
        if (OwnerUserId == ownerUserId)
        {
            // Idempotente: nada que actualizar.
            return;
        }

        OwnerUserId = ownerUserId;
        UpdatedAtUtc = utcNow;
    }
}
