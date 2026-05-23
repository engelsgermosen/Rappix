using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Rappix.Dispatch.Infrastructure.Persistence;

/// <summary>
/// Fabrica en tiempo de diseno para <c>dotnet ef</c>. Usa un publicador no-op para no requerir el
/// contenedor de DI durante la generacion de migrations.
/// </summary>
internal sealed class DispatchDbContextFactory : IDesignTimeDbContextFactory<DispatchDbContext>
{
    public DispatchDbContext CreateDbContext(string[] args)
    {
        string connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__DispatchDb")
            ?? "Host=localhost;Port=5432;Database=rappix_dispatch;Username=rappix;Password=rappix_dev_2026";

        DbContextOptions<DispatchDbContext> options = new DbContextOptionsBuilder<DispatchDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new DispatchDbContext(options, new NoOpPublisher());
    }

    private sealed class NoOpPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification => Task.CompletedTask;
    }
}
