using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Rappix.Merchants.Infrastructure.Persistence;

/// <summary>
/// Fabrica en tiempo de diseno para `dotnet ef`. Habilita NetTopologySuite (sin esto, `migrations add`
/// falla con las propiedades espaciales) y usa un publicador no-op para no requerir el contenedor de DI.
/// </summary>
internal sealed class MerchantsDbContextFactory : IDesignTimeDbContextFactory<MerchantsDbContext>
{
    public MerchantsDbContext CreateDbContext(string[] args)
    {
        string connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__MerchantsDb")
            ?? "Host=localhost;Port=5432;Database=rappix_merchants;Username=rappix;Password=rappix_dev_2026";

        DbContextOptions<MerchantsDbContext> options = new DbContextOptionsBuilder<MerchantsDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.UseNetTopologySuite())
            .Options;

        return new MerchantsDbContext(options, new NoOpPublisher());
    }

    private sealed class NoOpPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification => Task.CompletedTask;
    }
}
