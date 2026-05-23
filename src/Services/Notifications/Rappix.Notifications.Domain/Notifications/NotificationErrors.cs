using Rappix.BuildingBlocks.Core.Results;

namespace Rappix.Notifications.Domain.Notifications;

/// <summary>Errores del aggregate <see cref="Notification"/>.</summary>
public static class NotificationErrors
{
    /// <summary>Transicion bloqueada: la notificacion ya esta en un estado terminal incompatible.</summary>
    public static readonly Error TerminalState =
        Error.Conflict(
            "Notifications.Notification.TerminalState",
            "La notificacion esta en un estado terminal y no acepta la transicion solicitada.");

    /// <summary>Se intento crear con un destinatario sin email.</summary>
    public static readonly Error MissingRecipientEmail =
        Error.Validation(
            "Notifications.Notification.MissingRecipientEmail",
            "El destinatario debe tener un email no vacio.");

    /// <summary>Se intento crear con asunto vacio.</summary>
    public static readonly Error MissingSubject =
        Error.Validation(
            "Notifications.Notification.MissingSubject",
            "El asunto de la notificacion es requerido.");
}
