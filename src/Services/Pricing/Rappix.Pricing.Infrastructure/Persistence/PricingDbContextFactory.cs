using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Rappix.Pricing.Infrastructure.Persistence;

/// <summary>
/// Fabrica en tiempo de diseno para `dotnet ef`. Usa un publicador no-op para no requerir el contenedor
/// de DI al generar/aplicar migraciones.
/// </summary>
internal sealed class PricingDbContextFactory : IDesignTimeDbContextFactory<PricingDbContext>
{
    public PricingDbContext CreateDbContext(string[] args)
    {
        string connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__PricingDb")
            ?? "Host=localhost;Port=5432;Database=rappix_pricing;Username=rappix;Password=rappix_dev_2026";

        DbContextOptions<PricingDbContext> options = new DbContextOptionsBuilder<PricingDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new PricingDbContext(options, new NoOpPublisher());
    }

    private sealed class NoOpPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification => Task.CompletedTask;
    }
}
