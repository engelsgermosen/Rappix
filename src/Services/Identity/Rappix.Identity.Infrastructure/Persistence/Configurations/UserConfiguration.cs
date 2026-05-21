using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Rappix.Identity.Domain.Users;

namespace Rappix.Identity.Infrastructure.Persistence.Configurations;

/// <summary>Mapeo EF Core del agregado User.</summary>
internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(user => user.Id);

        builder.Property(user => user.Id)
            .HasConversion(id => id.Value, value => new UserId(value))
            .ValueGeneratedNever();

        builder.Property(user => user.Email)
            .HasConversion(email => email.Value, value => Email.FromTrusted(value))
            .HasMaxLength(256)
            .IsRequired();
        builder.HasIndex(user => user.Email).IsUnique();

        // Tipado como ValueConverter base para usar el overload no generico y permitir aplicar
        // un convertidor de PhoneNumber (no nulo) a una propiedad PhoneNumber? (EF maneja el null).
        ValueConverter phoneNumberConverter = new ValueConverter<PhoneNumber, string>(
            phone => phone.Value,
            value => PhoneNumber.FromTrusted(value));
        builder.Property(user => user.PhoneNumber)
            .HasConversion(phoneNumberConverter)
            .HasMaxLength(20);
        builder.HasIndex(user => user.PhoneNumber).IsUnique();

        builder.Property(user => user.PasswordHash).HasMaxLength(255);
        builder.Property(user => user.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(user => user.LastName).HasMaxLength(100).IsRequired();

        builder.Property(user => user.UserType)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(user => user.EmailConfirmed).IsRequired();
        builder.Property(user => user.PhoneConfirmed).IsRequired();
        builder.Property(user => user.IsActive).IsRequired();
        builder.Property(user => user.CreatedAtUtc).IsRequired();

        builder.HasMany(user => user.RefreshTokens)
            .WithOne()
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(user => user.ExternalLogins)
            .WithOne()
            .HasForeignKey(login => login.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(user => user.EmailConfirmationTokens)
            .WithOne()
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata.FindNavigation(nameof(User.RefreshTokens))!.SetPropertyAccessMode(PropertyAccessMode.Field);
        builder.Metadata.FindNavigation(nameof(User.ExternalLogins))!.SetPropertyAccessMode(PropertyAccessMode.Field);
        builder.Metadata.FindNavigation(nameof(User.EmailConfirmationTokens))!.SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
