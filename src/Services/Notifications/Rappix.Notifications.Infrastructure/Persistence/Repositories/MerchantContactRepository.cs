using Rappix.Notifications.Application.Abstractions;
using Rappix.Notifications.Domain.MerchantContacts;

namespace Rappix.Notifications.Infrastructure.Persistence.Repositories;

/// <summary>Repositorio de la proyeccion <see cref="MerchantContact"/>.</summary>
internal sealed class MerchantContactRepository(NotificationsDbContext db) : IMerchantContactRepository
{
    public async Task<MerchantContact?> GetByIdAsync(Guid merchantId, CancellationToken cancellationToken) =>
        await db.MerchantContacts.FindAsync([merchantId], cancellationToken).ConfigureAwait(false);

    public void Add(MerchantContact merchantContact) => db.MerchantContacts.Add(merchantContact);
}
