using System.Diagnostics.CodeAnalysis;
using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Notifications.Application.Abstractions;
using Rappix.Notifications.Application.Configuration;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace Rappix.Notifications.Infrastructure.Channels;

/// <summary>
/// Adaptador real de <see cref="INotificationChannel"/> sobre SendGrid. Construido por DI cuando
/// <c>Notifications:Channel=SendGrid</c> en config; el constructor exige <c>ApiKey</c> no vacio
/// (fail-fast — preferible a fallar en el primer envio).
/// </summary>
/// <remarks>
/// Mimetiza el patron del <c>SendGridEmailSender</c> de Identity (mismo SDK + cuenta + env vars
/// compartidas) pero NO comparte tipo — Identity tiene <c>IEmailSender</c> acoplado al caso
/// "confirmation email"; aqui la abstraccion es generica. Lo unico que se reusa son los env vars
/// (<c>SENDGRID_API_KEY</c>, <c>SENDGRID_FROM_EMAIL</c>, <c>SENDGRID_FROM_NAME</c>) que el compose
/// ya provee para ambos servicios.
/// </remarks>
internal sealed partial class SendGridNotificationChannel : INotificationChannel
{
    private readonly NotificationsOptions.SendGridChannelOptions _options;
    private readonly ILogger<SendGridNotificationChannel> _logger;
    private readonly ISendGridClient _client;

    /// <summary>
    /// Constructor de produccion: crea un <see cref="SendGridClient"/> desde el ApiKey de
    /// <paramref name="options"/>. Fail-fast si ApiKey vacio.
    /// </summary>
    public SendGridNotificationChannel(IOptions<NotificationsOptions> options, ILogger<SendGridNotificationChannel> logger)
        : this(BuildClient(options.Value.SendGrid.ApiKey), options.Value.SendGrid, logger)
    {
    }

    /// <summary>
    /// Constructor para tests (inyectable de <see cref="ISendGridClient"/> mockeable + options
    /// concretas). Internal porque solo Tests lo usa (via <c>InternalsVisibleTo</c>).
    /// </summary>
    internal SendGridNotificationChannel(
        ISendGridClient client,
        NotificationsOptions.SendGridChannelOptions sendGridOptions,
        ILogger<SendGridNotificationChannel> logger)
    {
        _client = client;
        _options = sendGridOptions;
        _logger = logger;
    }

    private static SendGridClient BuildClient(string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "Notifications:SendGrid:ApiKey vacio. Configurelo via env SENDGRID_API_KEY o cambie Notifications:Channel=Fake.");
        }

        return new SendGridClient(apiKey);
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Un fallo de envio se persiste como Notification.Failed; no debe relanzar (eso provocaria broker retry y emails duplicados).")]
    public async Task<Result<NotificationSendResult>> SendAsync(
        NotificationMessage message,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        SendGridMessage sgMessage = MailHelper.CreateSingleEmail(
            from: new EmailAddress(_options.FromEmail, _options.FromName),
            to: new EmailAddress(message.ToEmail, message.ToName),
            subject: message.Subject,
            plainTextContent: message.Body,
            htmlContent: null);

        try
        {
            Response response = await _client.SendEmailAsync(sgMessage, cancellationToken).ConfigureAwait(false);
            if ((int)response.StatusCode >= (int)HttpStatusCode.BadRequest)
            {
                int statusCode = (int)response.StatusCode;
                LogSendFailed(_logger, statusCode, message.ToEmail);
                return Result.Failure<NotificationSendResult>(
                    Error.Failure(
                        "Notifications.Channel.SendFailed",
                        $"SendGrid respondio con codigo {statusCode}."));
            }

            string? messageId = ExtractMessageId(response);
            return Result.Success(new NotificationSendResult(messageId));
        }
        catch (Exception exception)
        {
            LogSendException(_logger, message.ToEmail, exception);
            return Result.Failure<NotificationSendResult>(
                Error.Failure(
                    "Notifications.Channel.SendException",
                    $"Excepcion enviando via SendGrid: {exception.Message}"));
        }
    }

    /// <summary>
    /// Extrae el header <c>X-Message-Id</c> de la respuesta (lo devuelve SendGrid para correlacionar
    /// con webhooks de bounces/spam reports). Null si no esta presente.
    /// </summary>
    private static string? ExtractMessageId(Response response)
    {
        if (response.Headers is null)
        {
            return null;
        }

        if (response.Headers.TryGetValues("X-Message-Id", out IEnumerable<string>? values))
        {
            return values.FirstOrDefault();
        }

        return null;
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "SendGrid devolvio codigo {StatusCode} al enviar a {ToEmail}; notificacion quedara Failed.")]
    private static partial void LogSendFailed(ILogger logger, int statusCode, string toEmail);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Excepcion enviando email a {ToEmail} via SendGrid; notificacion quedara Failed.")]
    private static partial void LogSendException(ILogger logger, string toEmail, Exception exception);
}
