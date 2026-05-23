using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rappix.Notifications.Domain.Notifications;

namespace Rappix.Notifications.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeo EF Core del aggregate <see cref="Notification"/>. PK = Id generado en
/// <c>Notification.Create</c> (Guid v7 — varias filas por pedido, una por destinatario x tipo).
/// El UNIQUE PARTIAL INDEX <c>UX_Notification_BusinessKey</c> sobre
/// <c>(RelatedOrderId, RecipientUserId, NotificationType) WHERE RelatedOrderId IS NOT NULL</c> es
/// EL mecanismo de dedup transaccional (ADR-0010 D4 Nivel 2) — su violacion la traduce el
/// <c>NotificationsDbContext.SaveChangesAsync</c> a <c>DuplicateNotificationException</c>.
/// </summary>
internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications");

        builder.HasKey(notification => notification.Id);
        builder.Property(notification => notification.Id).ValueGeneratedNever();

        builder.Property(notification => notification.SourceMessageId)
            .HasMaxLength(64);

        builder.Property(notification => notification.RecipientUserId).IsRequired();

        builder.Property(notification => notification.RecipientEmail)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(notification => notification.RecipientRole)
            .HasConversion<short>()
            .IsRequired();

        builder.Property(notification => notification.NotificationType)
            .HasConversion<short>()
            .IsRequired();

        // RelatedOrderId nullable — soporta notificaciones no-pedido (welcome, password reset) en el
        // futuro. El unique index es PARTIAL (WHERE NOT NULL) para no bloquear esos casos.
        builder.Property(notification => notification.RelatedOrderId);

        builder.Property(notification => notification.Subject)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(notification => notification.BodyPreview)
            .HasMaxLength(Notification.BodyPreviewMaxLength)
            .IsRequired();

        builder.Property(notification => notification.Status)
            .HasConversion<short>()
            .IsRequired();

        builder.Property(notification => notification.ProviderMessageId)
            .HasMaxLength(128);

        builder.Property(notification => notification.ErrorReason)
            .HasMaxLength(512);

        builder.Property(notification => notification.CreatedAtUtc).IsRequired();
        builder.Property(notification => notification.SentAtUtc);

        // UNIQUE PARTIAL INDEX por clave de negocio — corazon del dedup transaccional. Solo aplica
        // cuando RelatedOrderId no es null (que en Fase 9 es siempre — pero el predicado preserva
        // la opcion para notificaciones no-pedido sin tocar la migracion).
        builder.HasIndex(
                notification => new
                {
                    notification.RelatedOrderId,
                    notification.RecipientUserId,
                    notification.NotificationType,
                })
            .HasFilter("\"RelatedOrderId\" IS NOT NULL")
            .IsUnique()
            .HasDatabaseName(NotificationsDbContext.NotificationBusinessKeyConstraintName);

        // Indice de consulta para el endpoint /api/v1/notifications/me — historial reciente por
        // usuario en orden cronologico descendente.
        builder.HasIndex(notification => new { notification.RecipientUserId, notification.CreatedAtUtc })
            .IsDescending(false, true)
            .HasDatabaseName("IX_Notification_Recipient_CreatedAt");
    }
}
