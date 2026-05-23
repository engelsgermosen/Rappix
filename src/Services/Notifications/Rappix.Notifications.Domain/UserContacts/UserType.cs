namespace Rappix.Notifications.Domain.UserContacts;

/// <summary>
/// Mirror del <c>UserType</c> de Identity. Se persiste en <see cref="UserContact"/> como smallint
/// para correlacionar con el rol del destinatario en las notificaciones (un Merchant que pide algo
/// como cliente sigue siendo <see cref="Merchant"/> en su contacto, pero recibe como
/// <c>RecipientRole.Customer</c> en esa notificacion especifica). Los valores numericos COINCIDEN
/// con los de Identity para no romper si se decide hacer JOIN cross-service en una vista futura.
/// </summary>
public enum UserType
{
    /// <summary>Usuario final que ordena pedidos.</summary>
    Customer = 0,

    /// <summary>Dueno o staff autorizado de un comercio.</summary>
    Merchant = 1,

    /// <summary>Repartidor.</summary>
    Courier = 2,

    /// <summary>Administrador de plataforma.</summary>
    Admin = 3,
}
