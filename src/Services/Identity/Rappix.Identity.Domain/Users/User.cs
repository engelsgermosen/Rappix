using Rappix.BuildingBlocks.Core.Domain;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Identity.Domain.Abstractions;
using Rappix.Identity.Domain.Users.Events;

namespace Rappix.Identity.Domain.Users;

/// <summary>
/// Agregado raiz que representa a un usuario de la plataforma y gestiona sus
/// refresh tokens (con rotacion y deteccion de robo), logins externos y tokens
/// de confirmacion de email.
/// </summary>
public sealed class User : AggregateRoot<UserId>, IHasDomainEvents
{
    /// <summary>Nombre del proveedor de login con Google.</summary>
    public const string GoogleProvider = "Google";

    private readonly List<RefreshToken> _refreshTokens = [];
    private readonly List<ExternalLogin> _externalLogins = [];
    private readonly List<EmailConfirmationToken> _emailConfirmationTokens = [];

    private User()
    {
    }

    private User(
        UserId id,
        Email email,
        PhoneNumber? phoneNumber,
        string? passwordHash,
        string firstName,
        string lastName,
        UserType userType,
        bool emailConfirmed,
        DateTime utcNow)
        : base(id)
    {
        Email = email;
        PhoneNumber = phoneNumber;
        PasswordHash = passwordHash;
        FirstName = firstName;
        LastName = lastName;
        UserType = userType;
        EmailConfirmed = emailConfirmed;
        PhoneConfirmed = false;
        IsActive = true;
        CreatedAtUtc = utcNow;
    }

    /// <summary>Email del usuario (unico).</summary>
    public Email Email { get; private set; } = null!;

    /// <summary>Telefono del usuario (unico si esta presente).</summary>
    public PhoneNumber? PhoneNumber { get; private set; }

    /// <summary>Hash BCrypt de la contrasena; null en cuentas creadas solo via Google.</summary>
    public string? PasswordHash { get; private set; }

    /// <summary>Nombre.</summary>
    public string FirstName { get; private set; } = null!;

    /// <summary>Apellido.</summary>
    public string LastName { get; private set; } = null!;

    /// <summary>Rol del usuario.</summary>
    public UserType UserType { get; private set; }

    /// <summary>Indica si el email fue confirmado.</summary>
    public bool EmailConfirmed { get; private set; }

    /// <summary>Indica si el telefono fue confirmado.</summary>
    public bool PhoneConfirmed { get; private set; }

    /// <summary>Indica si la cuenta esta activa.</summary>
    public bool IsActive { get; private set; }

    /// <summary>Momento de creacion (UTC).</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Momento de la ultima actualizacion (UTC).</summary>
    public DateTime? UpdatedAtUtc { get; private set; }

    /// <summary>Refresh tokens emitidos para el usuario.</summary>
    public IReadOnlyCollection<RefreshToken> RefreshTokens => _refreshTokens.AsReadOnly();

    /// <summary>Logins externos vinculados.</summary>
    public IReadOnlyCollection<ExternalLogin> ExternalLogins => _externalLogins.AsReadOnly();

    /// <summary>Tokens de confirmacion de email emitidos.</summary>
    public IReadOnlyCollection<EmailConfirmationToken> EmailConfirmationTokens => _emailConfirmationTokens.AsReadOnly();

    /// <summary>Indica si la cuenta tiene contrasena local configurada.</summary>
    public bool HasPassword => PasswordHash is not null;

    /// <summary>Crea un usuario por registro local. El email queda sin confirmar.</summary>
    public static User Register(
        Email email,
        PhoneNumber? phoneNumber,
        string passwordHash,
        string firstName,
        string lastName,
        UserType userType,
        DateTime utcNow)
    {
        var user = new User(UserId.New(), email, phoneNumber, passwordHash, firstName.Trim(), lastName.Trim(), userType, emailConfirmed: false, utcNow);
        user.RaiseDomainEvent(new UserRegisteredDomainEvent(user.Id, email.Value, user.FirstName, user.LastName, userType, EmailConfirmed: false));
        return user;
    }

    /// <summary>Crea un usuario a partir de un login con Google. El email queda confirmado.</summary>
    public static User RegisterWithGoogle(
        Email email,
        string firstName,
        string lastName,
        string providerKey,
        UserType userType,
        DateTime utcNow)
    {
        var user = new User(UserId.New(), email, phoneNumber: null, passwordHash: null, firstName.Trim(), lastName.Trim(), userType, emailConfirmed: true, utcNow);
        user.LinkExternalLogin(GoogleProvider, providerKey, email.Value, utcNow);
        user.RaiseDomainEvent(new UserRegisteredDomainEvent(user.Id, email.Value, user.FirstName, user.LastName, userType, EmailConfirmed: true));
        return user;
    }

    /// <summary>Agrega un token de confirmacion de email y lo devuelve.</summary>
    public EmailConfirmationToken AddEmailConfirmationToken(string tokenHash, DateTime utcNow, TimeSpan lifetime)
    {
        var token = new EmailConfirmationToken(Id, tokenHash, utcNow, utcNow.Add(lifetime));
        _emailConfirmationTokens.Add(token);
        return token;
    }

    /// <summary>Confirma el email usando el hash del token recibido.</summary>
    public Result ConfirmEmail(string tokenHash, DateTime utcNow)
    {
        if (EmailConfirmed)
        {
            return Result.Failure(UserErrors.EmailAlreadyConfirmed);
        }

        var token = _emailConfirmationTokens.FirstOrDefault(t => t.TokenHash == tokenHash);
        if (token is null || !token.IsValidAt(utcNow))
        {
            return Result.Failure(TokenErrors.InvalidConfirmation);
        }

        token.MarkUsed(utcNow);
        EmailConfirmed = true;
        UpdatedAtUtc = utcNow;
        RaiseDomainEvent(new EmailConfirmedDomainEvent(Id, Email.Value));
        return Result.Success();
    }

    /// <summary>Establece una nueva contrasena y revoca todas las sesiones activas.</summary>
    public void ChangePassword(string newPasswordHash, DateTime utcNow)
    {
        PasswordHash = newPasswordHash;
        UpdatedAtUtc = utcNow;
        RevokeAllActiveRefreshTokens(utcNow);
        RaiseDomainEvent(new PasswordChangedDomainEvent(Id, Email.Value));
    }

    /// <summary>Actualiza nombre, apellido y telefono.</summary>
    public void UpdateProfile(string firstName, string lastName, PhoneNumber? phoneNumber, DateTime utcNow)
    {
        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        if (!Equals(PhoneNumber, phoneNumber))
        {
            PhoneNumber = phoneNumber;
            PhoneConfirmed = false;
        }

        UpdatedAtUtc = utcNow;
    }

    /// <summary>Emite un nuevo refresh token (se almacena solo su hash) y lo devuelve.</summary>
    public RefreshToken IssueRefreshToken(string tokenHash, DateTime utcNow, TimeSpan lifetime, string? createdByIp)
    {
        var token = new RefreshToken(Id, tokenHash, utcNow, utcNow.Add(lifetime), createdByIp);
        _refreshTokens.Add(token);
        return token;
    }

    /// <summary>
    /// Rota un refresh token: revoca el presentado y emite uno nuevo. Si el token
    /// presentado ya estaba revocado, se interpreta como reuso (posible robo) y se
    /// revocan TODOS los refresh tokens activos del usuario.
    /// </summary>
    public Result RotateRefreshToken(
        string presentedTokenHash,
        string newTokenHash,
        DateTime utcNow,
        TimeSpan lifetime,
        string? createdByIp)
    {
        var existing = _refreshTokens.FirstOrDefault(t => t.TokenHash == presentedTokenHash);
        if (existing is null)
        {
            return Result.Failure(TokenErrors.InvalidRefresh);
        }

        if (existing.IsRevoked)
        {
            RevokeAllActiveRefreshTokens(utcNow);
            return Result.Failure(TokenErrors.ReuseDetected);
        }

        if (!existing.IsActiveAt(utcNow))
        {
            return Result.Failure(TokenErrors.ExpiredRefresh);
        }

        var replacement = new RefreshToken(Id, newTokenHash, utcNow, utcNow.Add(lifetime), createdByIp);
        _refreshTokens.Add(replacement);
        existing.Replace(replacement.Id, utcNow);
        return Result.Success();
    }

    /// <summary>Revoca el refresh token presentado (logout).</summary>
    public Result RevokeRefreshToken(string presentedTokenHash, DateTime utcNow)
    {
        var existing = _refreshTokens.FirstOrDefault(t => t.TokenHash == presentedTokenHash);
        if (existing is null)
        {
            return Result.Failure(TokenErrors.InvalidRefresh);
        }

        existing.Revoke(utcNow);
        return Result.Success();
    }

    /// <summary>Vincula un login externo si aun no existe y eleva el evento correspondiente.</summary>
    public void LinkExternalLogin(string provider, string providerKey, string email, DateTime utcNow)
    {
        if (HasExternalLogin(provider, providerKey))
        {
            return;
        }

        _externalLogins.Add(new ExternalLogin(Id, provider, providerKey, email, utcNow));
        RaiseDomainEvent(new ExternalLoginLinkedDomainEvent(Id, provider, providerKey, email));
    }

    /// <summary>Indica si ya existe un login externo para el proveedor y clave dados.</summary>
    public bool HasExternalLogin(string provider, string providerKey) =>
        _externalLogins.Any(e => e.Provider == provider && e.ProviderKey == providerKey);

    /// <summary>Registra un inicio de sesion satisfactorio.</summary>
    public void RecordLogin() => RaiseDomainEvent(new UserLoggedInDomainEvent(Id));

    private void RevokeAllActiveRefreshTokens(DateTime utcNow)
    {
        foreach (var token in _refreshTokens.Where(t => t.IsActiveAt(utcNow)))
        {
            token.Revoke(utcNow);
        }
    }
}
