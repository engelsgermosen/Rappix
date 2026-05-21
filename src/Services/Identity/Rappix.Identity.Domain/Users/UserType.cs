namespace Rappix.Identity.Domain.Users;

/// <summary>Rol principal de un usuario dentro de la plataforma.</summary>
public enum UserType
{
    /// <summary>Cliente que realiza pedidos.</summary>
    Customer = 0,

    /// <summary>Comercio que vende productos.</summary>
    Merchant = 1,

    /// <summary>Repartidor que entrega pedidos.</summary>
    Courier = 2,

    /// <summary>Administrador de la plataforma.</summary>
    Admin = 3,
}
