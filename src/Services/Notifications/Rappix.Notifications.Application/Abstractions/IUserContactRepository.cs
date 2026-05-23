using Rappix.Notifications.Domain.UserContacts;

namespace Rappix.Notifications.Application.Abstractions;

/// <summary>
/// Repositorio de la proyeccion <see cref="UserContact"/>. El consumer de
/// <c>UserRegisteredIntegrationEvent</c> hace lookup + upsert. Los consumers de eventos de pedido
/// hacen solo <see cref="GetByIdAsync"/> para resolver email + FullName.
/// </summary>
public interface IUserContactRepository
{
    /// <summary>Recupera el contacto por <c>UserId</c>. Null si no existe (cold-start gap).</summary>
    Task<UserContact?> GetByIdAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Lo marca para insercion al proximo SaveChanges.</summary>
    void Add(UserContact userContact);
}
