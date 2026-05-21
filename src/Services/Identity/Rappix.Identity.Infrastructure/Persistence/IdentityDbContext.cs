using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Rappix.BuildingBlocks.Core.Domain;
using Rappix.Identity.Application.Abstractions;
using Rappix.Identity.Domain.Abstractions;
using Rappix.Identity.Domain.Users;

namespace Rappix.Identity.Infrastructure.Persistence;

/// <summary>
/// Contexto de EF Core del servicio Identity. Actua como unidad de trabajo y, en cada guardado,
/// despacha los eventos de dominio (que la capa de aplicacion traduce a eventos de integracion
/// publicados a traves del outbox de MassTransit, dentro de la misma transaccion).
/// </summary>
public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options, IPublisher publisher)
    : DbContext(options), IUnitOfWork
{
    /// <summary>Usuarios.</summary>
    public DbSet<User> Users => Set<User>();

    /// <summary>Refresh tokens.</summary>
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    /// <summary>Logins externos.</summary>
    public DbSet<ExternalLogin> ExternalLogins => Set<ExternalLogin>();

    /// <summary>Tokens de confirmacion de email.</summary>
    public DbSet<EmailConfirmationToken> EmailConfirmationTokens => Set<EmailConfirmationToken>();

    /// <inheritdoc />
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Publicar antes del guardado garantiza que las filas del outbox entren en la misma transaccion.
        await DispatchDomainEventsAsync(cancellationToken);
        return await base.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("identity");

        // Tablas del outbox transaccional de MassTransit.
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }

    private async Task DispatchDomainEventsAsync(CancellationToken cancellationToken)
    {
        IHasDomainEvents[] aggregates =
        [
            .. ChangeTracker.Entries<IHasDomainEvents>()
                .Where(entry => entry.Entity.DomainEvents.Count > 0)
                .Select(entry => entry.Entity)
        ];

        IDomainEvent[] domainEvents = [.. aggregates.SelectMany(aggregate => aggregate.DomainEvents)];

        foreach (IHasDomainEvents aggregate in aggregates)
        {
            aggregate.ClearDomainEvents();
        }

        foreach (IDomainEvent domainEvent in domainEvents)
        {
            Type notificationType = typeof(DomainEventNotification<>).MakeGenericType(domainEvent.GetType());
            object notification = Activator.CreateInstance(notificationType, domainEvent)!;
            await publisher.Publish(notification, cancellationToken);
        }
    }
}
