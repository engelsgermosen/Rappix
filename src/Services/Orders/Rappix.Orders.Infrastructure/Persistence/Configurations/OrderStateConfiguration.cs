using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rappix.Orders.Application.Sagas;

namespace Rappix.Orders.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeo EF Core de la instancia de saga <see cref="OrderState"/> (repositorio de saga de MassTransit).
/// PK = CorrelationId (= OrderId), estado como texto, y xmin como token de concurrencia optimista para que
/// eventos concurrentes del mismo pedido (p. ej. cancel + timeout) se resuelvan con reintento del bus.
/// </summary>
internal sealed class OrderStateConfiguration : IEntityTypeConfiguration<OrderState>
{
    public void Configure(EntityTypeBuilder<OrderState> builder)
    {
        builder.ToTable("order_states");

        builder.HasKey(state => state.CorrelationId);
        builder.Property(state => state.CorrelationId).ValueGeneratedNever();

        builder.Property(state => state.CurrentState).HasMaxLength(64).IsRequired();

        builder.Property(state => state.QuoteId);
        builder.Property(state => state.CustomerUserId);
        builder.Property(state => state.MerchantId);
        builder.Property(state => state.TotalAmount);
        builder.Property(state => state.Currency).HasMaxLength(3);
        builder.Property(state => state.DeliveryLatitude);
        builder.Property(state => state.DeliveryLongitude);
        builder.Property(state => state.PickupLatitude);
        builder.Property(state => state.PickupLongitude);

        builder.Property(state => state.PaymentCaptured);
        builder.Property(state => state.PaymentId);
        builder.Property(state => state.CourierId);
        builder.Property(state => state.CompensationTerminal).HasConversion<string>().HasMaxLength(20);
        builder.Property(state => state.Reason).HasMaxLength(300);

        builder.Property(state => state.MerchantTimeoutTokenId);
        builder.Property(state => state.PaymentTimeoutTokenId);
        builder.Property(state => state.CourierTimeoutTokenId);

        // Concurrencia optimista via la columna de sistema xmin de PostgreSQL.
        builder.Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();
    }
}
