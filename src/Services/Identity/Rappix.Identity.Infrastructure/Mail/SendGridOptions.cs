namespace Rappix.Identity.Infrastructure.Mail;

/// <summary>Opciones de configuracion de SendGrid.</summary>
public sealed class SendGridOptions
{
    /// <summary>Nombre de la seccion en la configuracion.</summary>
    public const string SectionName = "SendGrid";

    /// <summary>API key de SendGrid. Nunca se commitea.</summary>
    public string ApiKey { get; init; } = string.Empty;

    /// <summary>Email remitente (debe estar verificado en SendGrid).</summary>
    public string FromEmail { get; init; } = "noreply@rappix.local";

    /// <summary>Nombre remitente.</summary>
    public string FromName { get; init; } = "Rappix";

    /// <summary>Id de plantilla dinamica (opcional; en Fase 1 usamos HTML inline).</summary>
    public string? EmailConfirmationTemplateId { get; init; }
}
