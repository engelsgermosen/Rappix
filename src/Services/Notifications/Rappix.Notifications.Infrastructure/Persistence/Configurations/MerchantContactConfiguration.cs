using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rappix.Notifications.Domain.MerchantContacts;

namespace Rappix.Notifications.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeo EF Core de la proyeccion <see cref="MerchantContact"/>. PK = MerchantId externo. Indice
/// secundario por OwnerUserId habilita la consulta "merchants que tiene este usuario" — util si en
/// el futuro un user es dueno de varios merchants (hoy es 1:1 pero el contrato no lo prohibe).
/// </summary>
internal sealed class MerchantContactConfiguration : IEntityTypeConfiguration<MerchantContact>
{
    public void Configure(EntityTypeBuilder<MerchantContact> builder)
    {
        builder.ToTable("merchant_contacts");

        builder.HasKey(contact => contact.Id);
        builder.Property(contact => contact.Id).ValueGeneratedNever();

        builder.Property(contact => contact.OwnerUserId).IsRequired();
        builder.Property(contact => contact.UpdatedAtUtc).IsRequired();

        builder.HasIndex(contact => contact.OwnerUserId)
            .HasDatabaseName("IX_MerchantContact_OwnerUserId");
    }
}
