using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Notifications.Application.Abstractions;
using Rappix.Notifications.Application.Configuration;
using Rappix.Notifications.Infrastructure.Channels;
using Xunit;

namespace Rappix.Notifications.Tests.Integration;

/// <summary>
/// Pruebas opt-in del <see cref="SendGridNotificationChannel"/> contra la API real de SendGrid. NO
/// corren en CI — la <see cref="SkippableFactAttribute"/> las salta si las variables de entorno
/// requeridas no estan presentes. Permite validacion manual antes de releases ("¿cambio la API
/// publica de SendGrid?"; "¿la cuenta sigue activa con la ApiKey correcta?").
/// </summary>
/// <remarks>
/// Variables de entorno requeridas para que el test corra:
/// <list type="bullet">
/// <item><c>NOTIFICATIONS_SENDGRID_APIKEY</c> = <c>SG.xxx</c> (test key de SendGrid).</item>
/// <item><c>NOTIFICATIONS_SENDGRID_FROMEMAIL</c> = email VERIFICADO en SendGrid (sender authentication).</item>
/// <item><c>NOTIFICATIONS_SENDGRID_TOEMAIL</c> = email destino real (recibe el smoke email).</item>
/// </list>
/// La idea es que un dev las exporte localmente cuando quiera probar el canal contra Stripe-test-mode
/// equivalente; en CI nunca estan presentes -> los tests se saltan limpiamente. Mismo patron que
/// <c>StripePaymentGatewayTests</c> en Payments.
/// </remarks>
public sealed class SendGridChannelSkippableTests
{
    [SkippableFact]
    public async Task SendAsync_AgainstRealSendGrid_DeliversEmail_WhenConfigured()
    {
        string? apiKey = Environment.GetEnvironmentVariable("NOTIFICATIONS_SENDGRID_APIKEY");
        string? fromEmail = Environment.GetEnvironmentVariable("NOTIFICATIONS_SENDGRID_FROMEMAIL");
        string? toEmail = Environment.GetEnvironmentVariable("NOTIFICATIONS_SENDGRID_TOEMAIL");

        Skip.If(string.IsNullOrWhiteSpace(apiKey), "NOTIFICATIONS_SENDGRID_APIKEY no esta seteado; test opt-in saltado.");
        Skip.If(string.IsNullOrWhiteSpace(fromEmail), "NOTIFICATIONS_SENDGRID_FROMEMAIL no esta seteado; test opt-in saltado.");
        Skip.If(string.IsNullOrWhiteSpace(toEmail), "NOTIFICATIONS_SENDGRID_TOEMAIL no esta seteado; test opt-in saltado.");

        var options = Options.Create(new NotificationsOptions
        {
            Channel = "SendGrid",
            SendGrid = new NotificationsOptions.SendGridChannelOptions
            {
                ApiKey = apiKey,
                FromEmail = fromEmail!,
                FromName = "Rappix Tests",
            },
        });
        var channel = new SendGridNotificationChannel(options, NullLogger<SendGridNotificationChannel>.Instance);

        var message = new NotificationMessage(
            ToEmail: toEmail!,
            ToName: "Rappix Tester",
            Subject: $"[Rappix smoke] SendGrid channel test {Guid.NewGuid():N}",
            Body: "Si lees esto, el canal SendGrid de Notifications funciona contra la cuenta real.");

        Result<NotificationSendResult> result = await channel.SendAsync(message, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(because: result.IsFailure ? result.Error.Description : "");
        // ProviderMessageId puede o no venir segun la version del SDK / configuracion de cuenta.
    }
}
