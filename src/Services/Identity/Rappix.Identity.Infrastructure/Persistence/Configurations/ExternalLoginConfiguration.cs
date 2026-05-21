using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Rappix.Identity.Domain.Users;

namespace Rappix.Identity.Infrastructure.Persistence.Configurations;

/// <summary>Mapeo EF Core de ExternalLogin.</summary>
internal sealed class ExternalLoginConfiguration : IEntityTypeConfiguration<ExternalLogin>
{
    public void Configure(EntityTypeBuilder<ExternalLogin> builder)
    {
        builder.ToTable("external_logins");
        builder.HasKey(login => login.Id);
        builder.Property(login => login.Id).ValueGeneratedNever();

        builder.Property(login => login.UserId)
            .HasConversion(id => id.Value, value => new UserId(value))
            .IsRequired();

        builder.Property(login => login.Provider).HasMaxLength(50).IsRequired();
        builder.Property(login => login.ProviderKey).HasMaxLength(256).IsRequired();
        builder.Property(login => login.Email).HasMaxLength(256).IsRequired();
        builder.Property(login => login.LinkedAtUtc).IsRequired();

        builder.HasIndex(login => new { login.Provider, login.ProviderKey }).IsUnique();
        builder.HasIndex(login => login.UserId);
    }
}
