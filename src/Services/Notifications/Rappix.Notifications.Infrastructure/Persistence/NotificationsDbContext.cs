using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Rappix.BuildingBlocks.Core.Domain;
using Rappix.Notifications.Application.Abstractions;
using Rappix.Notifications.Domain.Abstractions;
using Rappix.Notifications.Domain.MerchantContacts;
using Rappix.Notifications.Domain.NotificationOrders;
using Rappix.Notifications.Domain.Notifications;
using Rappix.Notifications.Domain.UserContacts;

namespace Rappix.Notifications.Infrastructure.Persistence;

/// <summary>
/// Contexto EF Core del servicio Notifications. Actua como unidad de trabajo. Ningun aggregate
/// hoy emite domain events (Notifications es projector + emisor, no orquestador), por lo que
/// <c>DispatchDomainEventsAsync</c> es efectivamente no-op; se mantiene el wiring por consistencia
/// con el resto del repo y por si en el futuro algun aggregate eleva eventos internos (e.g.
/// <c>NotificationFailedDomainEvent</c> para reintentos asincronos).
/// </summary>
public sealed class NotificationsDbContext(DbContextOptions<NotificationsDbContext> options, IPublisher publisher)
    : DbContext(options), IUnitOfWork
{
    /// <summary>Constraint del unique partial index sobre (RelatedOrderId, RecipientUserId, NotificationType).</summary>
    internal const string NotificationBusinessKeyConstraintName = "UX_Notification_BusinessKey";

    /// <summary>Historial auditable de notificaciones (1 fila por destinatario x tipo x pedido).</summary>
    public DbSet<Notification> Notifications => Set<Notification>();

    /// <summary>Proyeccion local userId -&gt; email + nombre + rol.</summary>
    public DbSet<UserContact> UserContacts => Set<UserContact>();

    /// <summary>Proyeccion local merchantId -&gt; ownerUserId.</summary>
    public DbSet<MerchantContact> MerchantContacts => Set<MerchantContact>();

    /// <summary>Proyeccion local orderId -&gt; (customerUserId, merchantId, courierUserId?).</summary>
    public DbSet<NotificationOrder> NotificationOrders => Set<NotificationOrder>();

    /// <inheritdoc />
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Publicar antes del guardado garantiza que si en el futuro algun aggregate emite eventos,
        // las filas del outbox EF entren en la misma transaccion (patron Tracking/Dispatch/Payments).
        await DispatchDomainEventsAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            return await base.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (PostgresUniqueViolation.Is(ex, NotificationBusinessKeyConstraintName))
        {
            // CRITICO (ADR-0010 D4 Nivel 2): traducir SOLO el constraint del dedup de negocio.
            // Cualquier otra violacion (FK, NOT NULL, otra constraint) propaga la DbUpdateException
            // original al pipeline de MassTransit para retry exponencial. Tragarse mas excepciones
            // como "ya existe" enmascararia bugs reales.
            throw new DuplicateNotificationException(
                "Ya existe una notificacion para esta (RelatedOrderId, RecipientUserId, NotificationType).", ex);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException(
                "Otra operacion concurrente modifico el registro. Reintente.", ex);
        }
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("notifications");

        // Tablas del outbox+inbox transaccional de MassTransit. Notifications NO publica integration
        // events propios (no produce mensajes al bus), pero AddConfigureEndpointsCallback +
        // UseEntityFrameworkOutbox aplica el INBOX (dedupe por MessageId del broker, Nivel 1 de
        // idempotencia) — leccion de Payments Fase 8: el outbox callback necesita las 3 tablas para
        // que el filtro pueda envolver cada consume en una tx + SaveChanges.
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotificationsDbContext).Assembly);

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
            await publisher.Publish(notification, cancellationToken).ConfigureAwait(false);
        }
    }
}
