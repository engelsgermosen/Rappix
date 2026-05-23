namespace Rappix.Notifications.Application.Configuration;

/// <summary>Opciones del servicio Notifications (seccion <c>Notifications</c> en appsettings/env).</summary>
public sealed class NotificationsOptions
{
    /// <summary>Nombre de la seccion en appsettings.</summary>
    public const string SectionName = "Notifications";

    /// <summary>
    /// Selector de canal: <c>"Fake"</c> (default; log-only, sin red, smoke E2E sin SendGrid) o
    /// <c>"SendGrid"</c> (envia via SendGrid real, requiere <c>SendGrid.ApiKey</c>).
    /// Documentado en ADR-0010 D2.
    /// </summary>
    public string Channel { get; set; } = "Fake";

    /// <summary>Configuracion del canal SendGrid (solo aplicable si <see cref="Channel"/> = "SendGrid").</summary>
    public SendGridChannelOptions SendGrid { get; set; } = new();

    /// <summary>Configuracion del canal SendGrid.</summary>
    public sealed class SendGridChannelOptions
    {
        /// <summary>API Key de SendGrid (<c>SG.xxx</c>). Lee de env <c>SENDGRID_API_KEY</c>.</summary>
        public string? ApiKey { get; set; }

        /// <summary>Email del remitente — debe estar verificado en SendGrid o el envio falla. Lee de env <c>SENDGRID_FROM_EMAIL</c>.</summary>
        public string FromEmail { get; set; } = "noreply@rappix.local";

        /// <summary>Nombre visible del remitente. Lee de env <c>SENDGRID_FROM_NAME</c>.</summary>
        public string FromName { get; set; } = "Rappix";
    }
}
