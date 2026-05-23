using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rappix.Notifications.Domain.UserContacts;

namespace Rappix.Notifications.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapeo EF Core de la proyeccion <see cref="UserContact"/>. PK = UserId externo (no se genera en
/// BD). UserType como smallint (mismo numero que Identity para futuro JOIN cross-service via vista).
/// </summary>
internal sealed class UserContactConfiguration : IEntityTypeConfiguration<UserContact>
{
    public void Configure(EntityTypeBuilder<UserContact> builder)
    {
        builder.ToTable("user_contacts");

        builder.HasKey(contact => contact.Id);
        builder.Property(contact => contact.Id).ValueGeneratedNever();

        builder.Property(contact => contact.Email)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(contact => contact.FirstName)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(contact => contact.LastName)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(contact => contact.UserType)
            .HasConversion<short>()
            .IsRequired();

        builder.Property(contact => contact.EmailConfirmed).IsRequired();
        builder.Property(contact => contact.UpdatedAtUtc).IsRequired();

        // Indice unico sobre Email — el modelo de Identity ya lo garantiza, pero defensivo aqui por
        // si llegan dos UserRegistered con el mismo email (no deberia pero el contrato no lo prohibe).
        builder.HasIndex(contact => contact.Email)
            .IsUnique()
            .HasDatabaseName("UX_UserContact_Email");
    }
}
