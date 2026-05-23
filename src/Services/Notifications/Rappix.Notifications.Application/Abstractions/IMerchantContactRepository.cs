using Rappix.Notifications.Domain.MerchantContacts;

namespace Rappix.Notifications.Application.Abstractions;

/// <summary>
/// Repositorio de la proyeccion <see cref="MerchantContact"/>. El consumer de
/// <c>MerchantApprovedIntegrationEvent</c> (y los otros 3 lifecycle) hace lookup + upsert. Los
/// consumers de eventos de pedido hacen solo <see cref="GetByIdAsync"/> para resolver
/// <c>MerchantId -&gt; OwnerUserId</c> y de ahi a <c>UserContact</c> para el email.
/// </summary>
public interface IMerchantContactRepository
{
    /// <summary>Recupera el contacto del merchant por <c>MerchantId</c>. Null si no existe (cold-start gap).</summary>
    Task<MerchantContact?> GetByIdAsync(Guid merchantId, CancellationToken cancellationToken);

    /// <summary>Lo marca para insercion al proximo SaveChanges.</summary>
    void Add(MerchantContact merchantContact);
}
