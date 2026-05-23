using Rappix.BuildingBlocks.Core.Domain;

namespace Rappix.Notifications.Domain.UserContacts;

/// <summary>
/// Proyeccion local de un usuario de Identity para resolver <c>UserId -> Email</c> sin tener que
/// llamar a Identity por gRPC ni acoplar Notifications a su BD. Se mantiene desde
/// <c>UserRegisteredIntegrationEvent</c> (insert/upsert) y <c>UserEmailConfirmedIntegrationEvent</c>
/// (marca <see cref="EmailConfirmed"/>=true). El <see cref="Entity{TId}.Id"/> ES el <c>UserId</c>
/// de Identity (relacion 1-1 por diseno).
/// </summary>
/// <remarks>
/// Decisiones de diseno:
/// <list type="bullet">
/// <item>Hereda de <see cref="Entity{TId}"/>: read model puro, sin eventos de dominio.</item>
/// <item><c>Email</c> es <see cref="string"/> plano (no value object) — la validacion vive en Identity;
///   aqui solo proyectamos lo que el evento ya trae validado.</item>
/// <item><see cref="Update"/> sobrescribe TODOS los campos sin checks de transicion: el evento mas
///   reciente gana (semantica de upsert de proyeccion). Si en el futuro se necesita "versionado" por
///   timestamp del evento, anadir un <c>EventOccurredAtUtc</c> y descartar updates viejos.</item>
/// </list>
/// </remarks>
public sealed class UserContact : Entity<Guid>
{
    // EF necesita ctor sin parametros para materializar.
    private UserContact() { }

    private UserContact(
        Guid userId,
        string email,
        string firstName,
        string lastName,
        UserType userType,
        bool emailConfirmed,
        DateTime utcNow)
        : base(userId)
    {
        Email = email;
        FirstName = firstName;
        LastName = lastName;
        UserType = userType;
        EmailConfirmed = emailConfirmed;
        UpdatedAtUtc = utcNow;
    }

    /// <summary>Email del usuario (snapshot mas reciente de Identity).</summary>
    public string Email { get; private set; } = null!;

    /// <summary>Nombre — usado en el cuerpo del email para personalizar ("Hola Juan,...").</summary>
    public string FirstName { get; private set; } = null!;

    /// <summary>Apellido — usado junto con FirstName en <see cref="FullName"/>.</summary>
    public string LastName { get; private set; } = null!;

    /// <summary>Rol del usuario en Identity (Customer/Merchant/Courier/Admin).</summary>
    public UserType UserType { get; private set; }

    /// <summary>True cuando el usuario confirmo su email (informacional — Fase 9 envia igualmente).</summary>
    public bool EmailConfirmed { get; private set; }

    /// <summary>Ultimo upsert (UTC).</summary>
    public DateTime UpdatedAtUtc { get; private set; }

    /// <summary>Nombre completo trim-eado, para usar como <c>ToName</c> del email.</summary>
    public string FullName => $"{FirstName} {LastName}".Trim();

    /// <summary>Crea un nuevo contacto desde un <c>UserRegisteredIntegrationEvent</c>.</summary>
    public static UserContact Create(
        Guid userId,
        string email,
        string firstName,
        string lastName,
        UserType userType,
        bool emailConfirmed,
        DateTime utcNow) =>
            new(userId, email, firstName, lastName, userType, emailConfirmed, utcNow);

    /// <summary>
    /// Upsert: sobrescribe todos los campos. Llamado por el consumer cuando llega un evento mas
    /// reciente (e.g. UserRegistered re-entregado o un futuro UserProfileUpdated).
    /// </summary>
    public void Update(
        string email,
        string firstName,
        string lastName,
        UserType userType,
        bool emailConfirmed,
        DateTime utcNow)
    {
        Email = email;
        FirstName = firstName;
        LastName = lastName;
        UserType = userType;
        EmailConfirmed = emailConfirmed;
        UpdatedAtUtc = utcNow;
    }

    /// <summary>Marca el email como confirmado (disparado por <c>UserEmailConfirmedIntegrationEvent</c>). Idempotente.</summary>
    public void ConfirmEmail(DateTime utcNow)
    {
        if (EmailConfirmed)
        {
            return;
        }

        EmailConfirmed = true;
        UpdatedAtUtc = utcNow;
    }
}
