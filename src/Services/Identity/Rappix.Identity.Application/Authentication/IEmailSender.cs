using Rappix.BuildingBlocks.Core.Results;

namespace Rappix.Identity.Application.Authentication;

/// <summary>Envia correos transaccionales (confirmacion de email).</summary>
public interface IEmailSender
{
    /// <summary>
    /// Envia el correo de confirmacion de email. Devuelve un Result de fallo en lugar de lanzar,
    /// para que el caso de uso pueda registrarlo sin abortar el registro del usuario.
    /// </summary>
    Task<Result> SendEmailConfirmationAsync(string toEmail, string toName, string confirmationUrl, CancellationToken cancellationToken);
}
