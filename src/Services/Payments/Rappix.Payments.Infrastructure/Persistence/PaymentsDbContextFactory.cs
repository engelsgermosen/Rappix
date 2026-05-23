using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Rappix.Payments.Infrastructure.Persistence;

/// <summary>
/// Fabrica en tiempo de diseno para <c>dotnet ef</c> (migrations add/script). Usa un publicador
/// no-op para no requerir el contenedor de DI durante la generacion. Mirror de
/// <c>TrackingDbContextFactory</c>.
/// </summary>
internal sealed class PaymentsDbContextFactory : IDesignTimeDbContextFactory<PaymentsDbContext>
{
    public PaymentsDbContext CreateDbContext(string[] args)
    {
        string connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__PaymentsDb")
            ?? "Host=localhost;Port=5432;Database=rappix_payments;Username=rappix;Password=rappix_dev_2026";

        DbContextOptions<PaymentsDbContext> options = new DbContextOptionsBuilder<PaymentsDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new PaymentsDbContext(options, new NoOpPublisher());
    }

    private sealed class NoOpPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification => Task.CompletedTask;
    }
}
