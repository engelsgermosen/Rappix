using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Rappix.Tracking.Infrastructure.Persistence;

/// <summary>
/// Fabrica en tiempo de diseno para <c>dotnet ef</c>. Usa un publicador no-op para no requerir el
/// contenedor de DI durante la generacion de migrations.
/// </summary>
internal sealed class TrackingDbContextFactory : IDesignTimeDbContextFactory<TrackingDbContext>
{
    public TrackingDbContext CreateDbContext(string[] args)
    {
        string connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__TrackingDb")
            ?? "Host=localhost;Port=5432;Database=rappix_tracking;Username=rappix;Password=rappix_dev_2026";

        DbContextOptions<TrackingDbContext> options = new DbContextOptionsBuilder<TrackingDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new TrackingDbContext(options, new NoOpPublisher());
    }

    private sealed class NoOpPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification => Task.CompletedTask;
    }
}
