using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Identity.Application.Authentication;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace Rappix.Identity.Infrastructure.Mail;

/// <summary>Envia el correo de confirmacion de email via SendGrid con una plantilla HTML inline.</summary>
internal sealed partial class SendGridEmailSender(IOptions<SendGridOptions> options, ILogger<SendGridEmailSender> logger)
    : IEmailSender
{
    private readonly SendGridOptions _options = options.Value;

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Un fallo al enviar el email no debe abortar el registro; se registra y se devuelve un Result de fallo.")]
    public async Task<Result> SendEmailConfirmationAsync(string toEmail, string toName, string confirmationUrl, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            LogApiKeyMissing(logger);
            return Result.Failure(Error.Failure("Identity.Email.NotConfigured", "El servicio de email no esta configurado."));
        }

        var client = new SendGridClient(_options.ApiKey);
        SendGridMessage message = MailHelper.CreateSingleEmail(
            new EmailAddress(_options.FromEmail, _options.FromName),
            new EmailAddress(toEmail, toName),
            "Confirma tu cuenta en Rappix",
            $"Confirma tu cuenta en Rappix abriendo este enlace: {confirmationUrl}",
            BuildHtmlBody(toName, confirmationUrl));

        try
        {
            Response response = await client.SendEmailAsync(message, cancellationToken);
            if ((int)response.StatusCode >= 400)
            {
                LogSendFailed(logger, (int)response.StatusCode);
                return Result.Failure(Error.Failure("Identity.Email.SendFailed", "No se pudo enviar el email de confirmacion."));
            }

            return Result.Success();
        }
        catch (Exception exception)
        {
            LogSendException(logger, exception);
            return Result.Failure(Error.Failure("Identity.Email.SendError", "Error al enviar el email de confirmacion."));
        }
    }

    private static string BuildHtmlBody(string name, string confirmationUrl) =>
        $"""
        <!DOCTYPE html>
        <html lang="es">
        <body style="margin:0;padding:0;background:#f4f4f5;font-family:Arial,Helvetica,sans-serif;">
        <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:#f4f4f5;padding:24px 0;">
        <tr><td align="center">
        <table role="presentation" width="480" cellpadding="0" cellspacing="0" style="background:#ffffff;border-radius:12px;overflow:hidden;">
        <tr><td style="background:#ff5a1f;padding:20px 32px;color:#ffffff;font-size:22px;font-weight:bold;">Rappix</td></tr>
        <tr><td style="padding:32px;color:#18181b;font-size:15px;line-height:1.6;">
        <p style="margin:0 0 16px;">Hola {name},</p>
        <p style="margin:0 0 24px;">Gracias por registrarte en Rappix. Confirma tu direccion de email para activar tu cuenta.</p>
        <p style="text-align:center;margin:0 0 24px;"><a href="{confirmationUrl}" style="background:#ff5a1f;color:#ffffff;text-decoration:none;padding:12px 28px;border-radius:8px;font-weight:bold;display:inline-block;">Confirmar mi email</a></p>
        <p style="margin:0 0 8px;color:#52525b;font-size:13px;">Si el boton no funciona, copia y pega este enlace en tu navegador:</p>
        <p style="margin:0;word-break:break-all;font-size:13px;"><a href="{confirmationUrl}" style="color:#ff5a1f;">{confirmationUrl}</a></p>
        </td></tr>
        <tr><td style="padding:20px 32px;color:#a1a1aa;font-size:12px;border-top:1px solid #e4e4e7;">Este enlace expira en 24 horas. Si no creaste esta cuenta, ignora este correo.</td></tr>
        </table>
        </td></tr>
        </table>
        </body>
        </html>
        """;

    [LoggerMessage(Level = LogLevel.Warning, Message = "SendGrid no esta configurado (falta ApiKey); no se envio el email.")]
    private static partial void LogApiKeyMissing(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "SendGrid respondio con codigo {StatusCode} al enviar el email.")]
    private static partial void LogSendFailed(ILogger logger, int statusCode);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error al enviar el email via SendGrid.")]
    private static partial void LogSendException(ILogger logger, Exception exception);
}
