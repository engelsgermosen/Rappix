using Rappix.Notifications.Application.Abstractions;
using Rappix.Notifications.Domain.UserContacts;

namespace Rappix.Notifications.Infrastructure.Persistence.Repositories;

/// <summary>Repositorio de la proyeccion <see cref="UserContact"/>.</summary>
internal sealed class UserContactRepository(NotificationsDbContext db) : IUserContactRepository
{
    public async Task<UserContact?> GetByIdAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.UserContacts.FindAsync([userId], cancellationToken).ConfigureAwait(false);

    public void Add(UserContact userContact) => db.UserContacts.Add(userContact);
}
