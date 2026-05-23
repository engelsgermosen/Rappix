using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Rappix.Notifications.Infrastructure.Persistence;

/// <summary>
/// Fabrica en tiempo de diseno para <c>dotnet ef</c> (migrations add/script). Usa un publicador
/// no-op para no requerir el contenedor de DI durante la generacion. Mirror de
/// <c>PaymentsDbContextFactory</c>.
/// </summary>
internal sealed class NotificationsDbContextFactory : IDesignTimeDbContextFactory<NotificationsDbContext>
{
    public NotificationsDbContext CreateDbContext(string[] args)
    {
        string connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__NotificationsDb")
            ?? "Host=localhost;Port=5432;Database=rappix_notifications;Username=rappix;Password=rappix_dev_2026";

        DbContextOptions<NotificationsDbContext> options = new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new NotificationsDbContext(options, new NoOpPublisher());
    }

    private sealed class NoOpPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification => Task.CompletedTask;
    }
}
