using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rappix.Payments.Domain.Payments;

namespace Rappix.Payments.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeo EF Core del aggregate <see cref="Payment"/>. PK = OrderId (Guid plano sin generacion en BD —
/// lo trae el evento <c>PaymentRequestedIntegrationEvent</c>; el PK natural actua como unique
/// constraint para impedir doble-insercion concurrente, Nivel 2 de idempotencia de dinero). Status
/// como texto (legibilidad en BD). Money como OwnsOne (dos columnas en la misma tabla). xmin shadow
/// para concurrencia optimista.
/// </summary>
internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments");

        builder.HasKey(payment => payment.Id);
        builder.Property(payment => payment.Id).ValueGeneratedNever();

        builder.Property(payment => payment.CustomerUserId).IsRequired();

        // Money como OwnsOne: dos columnas (amount/currency) en la misma fila de payments. La
        // convencion global de ConfigureConventions ya impone precision(19, 4) al decimal — el
        // HasPrecision aqui es explicito por documentacion.
        builder.OwnsOne(payment => payment.Amount, money =>
        {
            money.Property(m => m.Amount)
                .HasColumnName("amount")
                .HasPrecision(19, 4)
                .IsRequired();

            money.Property(m => m.Currency)
                .HasColumnName("currency")
                .HasMaxLength(3)
                .IsRequired();
        });

        builder.Property(payment => payment.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        // Limites alineados con los formatos reales: Stripe pi_xxx / re_xxx no exceden 64 chars.
        builder.Property(payment => payment.ProviderPaymentIntentId).HasMaxLength(64);
        builder.Property(payment => payment.ProviderRefundId).HasMaxLength(64);

        // Razon textual para auditoria (Void/Fail/NeedsReview). 500 chars cubre mensajes humanos
        // sin invitar a paginas enteras de log.
        builder.Property(payment => payment.Reason).HasMaxLength(500);

        builder.Property(payment => payment.CreatedAtUtc).IsRequired();
        builder.Property(payment => payment.UpdatedAtUtc).IsRequired();

        // Indice por Status: lookup operacional para revisar pagos en NeedsReview (que requieren
        // intervencion humana) sin escaneo completo. Tambien util para metricas en Fase 9.
        builder.HasIndex(payment => payment.Status);

        // Concurrencia optimista via la columna de sistema xmin de PostgreSQL. Nivel 2 de defensa
        // de doble cobro: si dos consumers de PaymentRequested para el mismo OrderId se cruzan
        // (race extremo que el inbox EF tambien protege), el segundo SaveChanges lanza
        // DbUpdateConcurrencyException que se traduce a ConcurrencyConflictException.
        builder.Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();
    }
}
