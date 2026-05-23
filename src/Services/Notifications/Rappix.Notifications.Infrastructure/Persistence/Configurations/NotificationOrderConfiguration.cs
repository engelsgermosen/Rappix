using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rappix.Notifications.Domain.NotificationOrders;

namespace Rappix.Notifications.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeo EF Core de la proyeccion <see cref="NotificationOrder"/>. PK = OrderId externo. Los
/// consumers de OrderAccepted/Cancelled/Failed/Delivered hacen lookup por aqui para resolver
/// CustomerUserId + MerchantId que sus eventos NO llevan.
/// </summary>
internal sealed class NotificationOrderConfiguration : IEntityTypeConfiguration<NotificationOrder>
{
    public void Configure(EntityTypeBuilder<NotificationOrder> builder)
    {
        builder.ToTable("notification_orders");

        builder.HasKey(order => order.Id);
        builder.Property(order => order.Id).ValueGeneratedNever();

        builder.Property(order => order.CustomerUserId).IsRequired();
        builder.Property(order => order.MerchantId).IsRequired();
        builder.Property(order => order.CourierUserId);
        builder.Property(order => order.CreatedAtUtc).IsRequired();
        builder.Property(order => order.UpdatedAtUtc).IsRequired();

        builder.HasIndex(order => order.MerchantId)
            .HasDatabaseName("IX_NotificationOrder_MerchantId");

        builder.HasIndex(order => order.CustomerUserId)
            .HasDatabaseName("IX_NotificationOrder_CustomerUserId");
    }
}
